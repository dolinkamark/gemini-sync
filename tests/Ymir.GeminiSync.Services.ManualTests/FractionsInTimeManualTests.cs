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
        BaseUrl = "https://pfpublicapi.geminisuite.com/public",
        MunicipalityNo = "stavanger",
        SubscriptionKey = "f714fceb470744ffa6017cfb050ffcbb"
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
        const string previousFilePath = "E:\\Temp\\Ymir_Sync\\fractions_20260813_01\\agreement_place_history_lines_Hyttecontainer_20260813.json";
        const string filePath = "E:\\Temp\\Ymir_Sync\\sync_20260902\\fractions\\agreement_place_history_lines_Hyttecontainer_20260902.json";

        var placeLines = await FileUtils.ReadFileContent<List<AgreementPlaceHistoryLine>>(filePath);
        placeLines = placeLines
            .Where(p => !String.IsNullOrWhiteSpace(p.ExternalAgreementId))
            .ToList();

        var previousPlaceLines = await FileUtils.ReadFileContent<List<AgreementPlaceHistoryLine>>(previousFilePath);
        previousPlaceLines = previousPlaceLines
            .Where(p => !String.IsNullOrWhiteSpace(p.ExternalAgreementId))
            .ToList();

        var testGeminiClient = new GeminiClient(_settings, _httpClientFactory);
        var fractionService = new FractionService();
        var fractionsSyncService = new FractionsSyncService(_agreementPlacesRepository, testGeminiClient);

        //Act
        var intervals = fractionService.BuildFractionIntervalsByDate(placeLines);
        var timelines = fractionService.CreateFractionTimelines(intervals);

        var previousTimelines = fractionService.CreateFractionTimelines(
            fractionService.BuildFractionIntervalsByDate(previousPlaceLines)
        );

        var totalCount = timelines.Count;
        timelines = fractionService.GetChangedTimelines(timelines, previousTimelines);

        var updatedCount = 0;
        var syncReport = new SyncReport();

        foreach (var currentTimeline in timelines)
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

        syncReport.TotalCount = totalCount;
        syncReport.UpdatedCount = updatedCount;

        var errorContent = JsonSerializer.Serialize(syncReport.Errors);

        //Assert
        Assert.Fail("Manual test only");
    }
}
