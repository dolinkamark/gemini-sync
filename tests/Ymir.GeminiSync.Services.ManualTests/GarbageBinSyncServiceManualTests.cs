using Microsoft.Extensions.Options;
using NSubstitute;
using Ymir.GeminiSync.Common;
using Ymir.GeminiSync.Domain;
using Ymir.GeminiSync.Domain.Repositories;
using Ymir.GeminiSync.Services.Settings;

namespace Ymir.GeminiSync.Services.ManualTests;

public class GarbageBinSyncServiceManualTests
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

    public GarbageBinSyncServiceManualTests()
    {
        _httpClientFactory
            .CreateClient(Arg.Any<string>())
            .Returns(_ => new HttpClient());

        _syncReportOptions.Value.Returns(_reportOptions);
        _syncReportRepository = new SyncReportFileRepository(_syncReportOptions);
    }

    [Fact(Skip = "Manual test only")]
    public async Task SyncGarbageBinCollections()
    {
        //Arrange
        const string previousFilePath = "E:\\Temp\\Ymir_Sync\\garbage_bins_20260813_01\\garbage_bins_Spann_20260813.json";
        const string filePath = "E:\\Temp\\Ymir_Sync\\sync_20260902\\garbage_bins\\garbage_bins_Spann_20260902.json";
        const int customerId = 1;
        const string placeType = "Spann";

        var collectionLines = await FileUtils.ReadFileContent<List<GarbageBinCollectionLine>>(filePath);
        var prevCollectionLines = await FileUtils.ReadFileContent<List<GarbageBinCollectionLine>>(previousFilePath);

        var garbageBinRepository = Substitute.For<IGarbageBinCollectionRepository>();
        garbageBinRepository.GetGarbageBinCollections(Arg.Any<int>(), Arg.Any<string>())
            .Returns(Task.FromResult(collectionLines));

        var garbageBinService = new GarbageBinService();
        var testGeminiClient = new GeminiClient(_settings, _httpClientFactory);

        var testGeminiSyncService = new GarbageBinSyncService(
            garbageBinRepository, garbageBinService, _syncReportRepository, testGeminiClient
        );

        //Act
        var syncReport = await testGeminiSyncService.SyncGarbageBinCollections(customerId, placeType, previousCollection: prevCollectionLines);

        //Assert
        Assert.Fail("Manual test only");
    }

    [Fact(Skip = "Manual test only")]
    public async Task CleanGarbageBinCollections()
    {
        //Arrange
        const string filePath = "E:\\Temp\\Ymir_Compare\\GarbageBins\\garbage_bins_customerid1_20260806\\garbage_bins_Bruksdel nedgravd_20260806.json";

        var collectionLines = await FileUtils.ReadFileContent<List<GarbageBinCollectionLine>>(filePath);
        var placeNrList = collectionLines
            .Select(l => l.PlaceNr)
            .Distinct()
            .OrderBy(l => l)
            .ToList();

        var testGeminiClient = new GeminiClient(_settings, _httpClientFactory);

        //Act
        int updateCount = 0;

        foreach(var placeNr in placeNrList)
        {
            if(placeNr != null)
            {
                var isSuccessful = await testGeminiClient.DeleteGarbageBinCollection(placeNr.Value);
                if (isSuccessful)
                {
                    updateCount++;
                }
            }
        }

        //Assert
        Assert.Fail("Manual test only");
    }
}
