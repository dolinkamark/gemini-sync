using NSubstitute;
using System.Text.Json;
using Ymir.GeminiSync.Domain;
using Ymir.GeminiSync.Domain.Repositories;
using Ymir.GeminiSync.Services.Models;
using Ymir.GeminiSync.Services.Settings;

namespace Ymir.GeminiSync.Services.ManualTests;

public class FractionsInTimeManualTests
{
    private readonly IHttpClientFactory _httpClientFactory = Substitute.For<IHttpClientFactory>();
    private readonly IAgreementPlacesRepository _agreementPlacesRepository = Substitute.For<IAgreementPlacesRepository>();

    private readonly GeminiSettings _settings = new GeminiSettings
    {
        BaseUrl = "https://powelqapfpublicapi.azure-api.net/public",
        MunicipalityNo = "stavangerkundetest",
        SubscriptionKey = "3d8d028ee9be4cc9a9e4ac0a92068966"
    };

    public FractionsInTimeManualTests()
    {
        _httpClientFactory
            .CreateClient(Arg.Any<string>())
            .Returns(_ => new HttpClient());
    }

    [Fact(Skip = "Manual test only")]
    public async Task UpdateFractionsInTime()
    {
        //Arrange
        const string filePath = "E:\\Temp\\Ymir\\202607\\agreement_place_history_lines_Sekk i spann_20260706.json";

        var placeLines = await FileUtils.ReadFileContent<List<AgreementPlaceHistoryLine>>(filePath);
        placeLines = placeLines
            .Where(p => !String.IsNullOrWhiteSpace(p.ExternalAgreementId))
            .ToList();

        var testGeminiClient = new GeminiClient(_settings, _httpClientFactory);
        var fractionService = new FractionService();
        var fractionsSyncService = new FractionsSyncService(_agreementPlacesRepository, testGeminiClient);

        var testLines = placeLines.Where(p => p.PlaceNr == 1185842).ToList();
        var testContent = JsonSerializer.Serialize(testLines);

        //Act
        var intervals = fractionService.BuildFractionIntervalsByDate(placeLines);
        var timelines = fractionService.CreateFractionTimelines(intervals);

        var updatedCount = 0;
        var syncReport = new SyncReport();

        foreach(var currentTimeline in timelines)
        {
            try
            {
                //Adjust hours to avoid dayshift by timezone
                currentTimeline.Item2.ForEach(t => t.FractionsInTime.ForEach(f =>
                {
                    f.DateFrom = f.DateFrom.AddHours(12);
                    f.DateTo = f.DateTo?.AddHours(12);
                }));

                var isSuccessful = await testGeminiClient.UpdateFractionsInTime(currentTimeline.Item1, currentTimeline.Item2);
                if (!isSuccessful)
                {
                    syncReport.Errors.Add(new SyncError
                    {
                        PlaceNr = currentTimeline.Item1,
                        Description = "Gemini client Fractions update call failed"
                    });
                }
                else
                {
                    updatedCount++;
                }
            }
            catch (Exception ex)
            {
                syncReport.Errors.Add(new SyncError
                {
                    PlaceNr = currentTimeline.Item1,
                    Description = ex.ToString()
                });
            }
        }

        syncReport.TotalCount = timelines.Count;
        syncReport.UpdatedCount = updatedCount;

        //Assert
        Assert.Fail("Manual test only");
    }
}
