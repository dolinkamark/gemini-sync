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
        BaseUrl = "https://powelqapfpublicapi.azure-api.net/public",
        MunicipalityNo = "stavangerkundetest",
        SubscriptionKey = "3d8d028ee9be4cc9a9e4ac0a92068966"
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

    [Fact]
    public async Task SyncGarbageBinCollections_HasFiles()
    {
        //Arrange
        const string filePath = "E:\\Temp\\Ymir\\202607\\garbage_bins_Spann_20260706.json";
        const int customerId = 2;
        const string placeType = "Spann";

        var collectionLines = await FileUtils.ReadFileContent<List<GarbageBinCollectionLine>>(filePath);
        var garbageBinRepository = Substitute.For<IGarbageBinCollectionRepository>();
        garbageBinRepository.GetGarbageBinCollections(Arg.Any<int>(), Arg.Any<string>())
            .Returns(Task.FromResult(collectionLines));

        var garbageBinService = new GarbageBinService();
        var testGeminiClient = new GeminiClient(_settings, _httpClientFactory);

        var testGeminiSyncService = new GarbageBinSyncService(
            garbageBinRepository, garbageBinService, _syncReportRepository, testGeminiClient
        );

        //Act
        var syncReport = await testGeminiSyncService.SyncGarbageBinCollections(customerId, placeType);

        //Assert
        Assert.Fail("Manual test only");
    }
}
