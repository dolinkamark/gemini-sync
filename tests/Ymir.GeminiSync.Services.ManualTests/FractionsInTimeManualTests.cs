using Microsoft.Extensions.Options;
using NSubstitute;
using Ymir.GeminiSync.Common;
using Ymir.GeminiSync.Domain;
using Ymir.GeminiSync.Domain.Repositories;
using Ymir.GeminiSync.Services.Settings;

namespace Ymir.GeminiSync.Services.ManualTests;

public class FractionsInTimeManualTests
{
    private readonly IHttpClientFactory _httpClientFactory = Substitute.For<IHttpClientFactory>();
    private readonly IOptions<SyncReportOptions> _syncReportOptions = Substitute.For<IOptions<SyncReportOptions>>();

    private readonly ISyncReportRepository _syncReportRepository;

    private readonly GeminiSettings _settings = new GeminiSettings
    {
        BaseUrl = "https://pfpublicapi.geminisuite.com/public",
        MunicipalityNo = "stavanger",
        SubscriptionKey = "f714fceb470744ffa6017cfb050ffcbb"
    };

    private readonly SyncReportOptions _reportOptions = new SyncReportOptions
    {
        FilePath = "E:\\Temp\\Ymir"
    };

    public FractionsInTimeManualTests()
    {
        _httpClientFactory
            .CreateClient(Arg.Any<string>())
            .Returns(_ => new HttpClient());

        _syncReportOptions.Value.Returns(_reportOptions);
        _syncReportRepository = new SyncReportFileRepository(_syncReportOptions);
    }

    [Fact(Skip = "Manual test only")]
    public async Task UpdateFractionsInTime()
    {
        //Arrange
        const string previousFilePath = "E:\\Temp\\Ymir_Sync\\sync_20260902\\fractions\\agreement_place_history_lines_Bruksdel_nedgravd_20260902.json";
        const string filePath = "E:\\Temp\\Ymir_Sync\\sync_20260915\\fractions\\FractionsHistory_Bruksdel_nedgravd_20260915.json";
        const int customerId = 1;
        const string placeType = "Bruksdel nedgravd";

        var placeLines = await FileUtils.ReadFileContent<List<AgreementPlaceHistoryLine>>(filePath);
        var previousPlaceLines = await FileUtils.ReadFileContent<List<AgreementPlaceHistoryLine>>(previousFilePath);

        var agreementPlacesRepository = Substitute.For<IAgreementPlacesRepository>();
        agreementPlacesRepository.GetFractionsHistory(Arg.Any<int>(), Arg.Any<string>())
            .Returns(Task.FromResult(placeLines));

        var fractionService = new FractionService();
        var testGeminiClient = new GeminiClient(_settings, _httpClientFactory);

        var fractionsSyncService = new FractionsSyncService(
            agreementPlacesRepository, fractionService, _syncReportRepository, testGeminiClient
        );

        //Act
        var syncReport = await fractionsSyncService.SyncFractionsInTime(customerId, placeType, previousPlaceLines);

        //Assert
        Assert.Fail("Manual test only");
    }
}
