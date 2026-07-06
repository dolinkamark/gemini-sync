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

    [Fact]
    public async Task UpdateFractionsInTime()
    {
        //Arrange
        const string filePath = "E:\\Temp\\Ymir\\202607\\agreement_place_history_lines_Hyttecontainer_20260706.json";

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

        var intervalGroups = intervals
            .GroupBy(i => i.PlaceNr)
            .ToList();

        var updatedCount = 0;
        var syncReport = new SyncReport();

        var fractionsInTimeList = new List<(int, List<AgreementFractionTimeline>)>();

        foreach (var intervalGroup in intervalGroups)
        {
            if (intervalGroup.Key != 1185842) continue;

            var currentIntervals = intervalGroup.ToList();
            var fractionsInTime = fractionService.CreateFractionsInTime(currentIntervals);

            //Fix incorrect date intervals if DateFrom and ToDate overlaps
            for (int i = 0; i < fractionsInTime.Count - 1; ++i)
            {
                if (fractionsInTime[i].DateFrom.Date == fractionsInTime[i].DateTo?.Date)
                {
                    fractionsInTime[i].DateFrom = fractionsInTime[i].DateFrom.AddDays(-1);
                }
            }

            fractionsInTime.ForEach(f =>
            {
                f.DateFrom = f.DateFrom.AddHours(12);

                if (f.DateTo != null)
                {
                    f.DateTo = f.DateTo.Value.AddHours(12);
                }
            });

            var timelines = fractionService.CreateFractionTimelines(fractionsInTime);

            fractionsInTimeList.Add((intervalGroup.Key, timelines));
        }

        foreach (var fractionsInTime in fractionsInTimeList)
        {
            try
            {
                var isSuccessful = await testGeminiClient.UpdateFractionsInTime(fractionsInTime.Item1, fractionsInTime.Item2);
                if (!isSuccessful)
                {
                    syncReport.Errors.Add(new SyncError
                    {
                        PlaceNr = fractionsInTime.Item1,
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
                    PlaceNr = fractionsInTime.Item1,
                    Description = ex.ToString()
                });
            }
        }

        syncReport.UpdatedCount = updatedCount;

        //Assert
        Assert.Fail("Manual test only");
    }
}
