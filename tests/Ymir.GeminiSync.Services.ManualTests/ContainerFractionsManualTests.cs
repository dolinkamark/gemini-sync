using NSubstitute;
using Ymir.GeminiSync.Domain;
using Ymir.GeminiSync.Domain.Repositories;
using Ymir.GeminiSync.Services.Models.Containers;
using Ymir.GeminiSync.Services.Settings;

namespace Ymir.GeminiSync.Services.ManualTests;

public class ContainerFractionsManualTests
{
    private readonly IHttpClientFactory _httpClientFactory = Substitute.For<IHttpClientFactory>();
    private readonly IAgreementPlacesRepository _agreementPlacesRepository = Substitute.For<IAgreementPlacesRepository>();

    private readonly GeminiSettings _settings = new GeminiSettings
    {
        BaseUrl = "https://pfpublicapi.geminisuite.com/public",
        MunicipalityNo = "stavanger",
        SubscriptionKey = "f714fceb470744ffa6017cfb050ffcbb"
    };

    public ContainerFractionsManualTests()
    {
        _httpClientFactory
            .CreateClient(Arg.Any<string>())
            .Returns(_ => new HttpClient());
    }

    [Fact(Skip = "Manual test only")]
    public async Task UpdateFractionsInTime()
    {
        //Arrange
        const string filePath = "E:\\Temp\\Ymir_Sync\\container_fractions_20260827\\agreement_place_history_lines_Nedgravd_privat_20260828.json";

        var placeLines = await FileUtils.ReadFileContent<List<AgreementPlaceHistoryLine>>(filePath);
        placeLines = placeLines
            .Where(p => !String.IsNullOrWhiteSpace(p.ExternalAgreementId))
            .ToList();

        var testGeminiClient = new GeminiClient(_settings, _httpClientFactory);
        var fractionService = new FractionService();

        //Act
        var intervals = fractionService.BuildFractionIntervalsByDate(placeLines);
        var timelines = fractionService.CreateFractionTimelines(intervals);

        var updatedCount = 0;
        var syncReport = new SyncReport();

        foreach (var currentTimeline in timelines)
        {
            try
            {
                currentTimeline.Item2.ForEach(t => t.FractionsInTime.ForEach(f =>
                {
                    f.DateFrom = f.DateFrom.AddHours(12);
                    f.DateTo = f.DateTo?.AddHours(12);
                }));

                var agreementFractions = new List<PrivateContainerGroupAgreementFractions>();
                foreach (var fraction in currentTimeline.Item2)
                {
                    agreementFractions.Add(new PrivateContainerGroupAgreementFractions
                    {
                        AgreementId = fraction.AgreementId,
                        FractionsInTime = fraction.FractionsInTime.Select(f => new PrivateContainerGroupFractionInTime()
                        {
                            DateFrom = f.DateFrom,
                            DateTo = f.DateTo,
                            FractionNumerator = f.FractionNumerator,
                            FractionDenominator = f.FractionDenominator
                        }).ToList()
                    });
                }

                var isSuccessful = await testGeminiClient.UpdatePrivateContainerGroupFractions(currentTimeline.Item1, agreementFractions);
                if (isSuccessful)
                {
                    updatedCount++;
                }
                else
                {
                    syncReport.Errors.Add(new SyncError
                    {
                        PlaceNr = currentTimeline.Item1,
                        Description = "Gemini client Fractions update call failed"
                    });
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
