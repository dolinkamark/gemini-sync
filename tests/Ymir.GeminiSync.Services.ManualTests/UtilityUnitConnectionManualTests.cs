using Microsoft.Extensions.Options;
using NSubstitute;
using System.Text.Json;
using Ymir.GeminiSync.Common;
using Ymir.GeminiSync.Domain;
using Ymir.GeminiSync.Domain.Repositories;
using Ymir.GeminiSync.Services.Abstract;
using Ymir.GeminiSync.Services.Models;
using Ymir.GeminiSync.Services.Settings;

namespace Ymir.GeminiSync.Services.ManualTests;

public class UtilityUnitConnectionManualTests
{
    private readonly IHttpClientFactory _httpClientFactory = Substitute.For<IHttpClientFactory>();
    private readonly IOptions<SyncReportOptions> _syncReportOptions = Substitute.For<IOptions<SyncReportOptions>>();
    private readonly IOptions<UtilityConnectionsServiceOptions> _serviceOptions = Substitute.For<IOptions<UtilityConnectionsServiceOptions>>();

    private readonly IAgreementPlacesRepository _agreementPlacesRepository = Substitute.For<IAgreementPlacesRepository>();
    private readonly IAgreementExcemptionRepository _agreementExcemptionsRepository = Substitute.For<IAgreementExcemptionRepository>();
    private readonly IUtilityConnectionsService _utilityConnectionService;
    private readonly ISyncReportRepository _syncReportRepository;

    private readonly UtilityConnectionsServiceOptions _testOptions = new UtilityConnectionsServiceOptions
    {
        PublicContainerNames = new List<string> { "Bruksdel nedgravd", "Hyttecontainer" },
        NotConnectedToPickupSystem = new List<string> { "Hyttecontainer" },
    };

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

    public UtilityUnitConnectionManualTests()
    {
        _httpClientFactory
            .CreateClient(Arg.Any<string>())
            .Returns(_ => new HttpClient());

        _serviceOptions.Value.Returns(_testOptions);
        _syncReportOptions.Value.Returns(_reportOptions);

        _syncReportRepository = new SyncReportFileRepository(_syncReportOptions);
        _utilityConnectionService =  new UtilityConnectionsService(_serviceOptions);
    }

    [Fact]
    public async Task UptadeUtilityConnections()
    {
        //Arrange
        const string filePath = "E:\\Temp\\Ymir\\202607\\agreement_places_Spann_20260702.json";
        const string agreementExemptionsFilePath = "E:\\Temp\\Ymir\\202607\\agreement_exemptions_Spann_20260702.json";
        const int testCustomerId = 2;
        const string placeTypes = "Sekk i spann";

        var testGeminiClient = new GeminiClient(_settings, _httpClientFactory);

        var connectionLines = await FileUtils.ReadFileContent<List<AgreementPlaceConnectionLine>>(filePath);
        _agreementPlacesRepository
            .GetUtilityUnitConnections(Arg.Any<int>(), Arg.Any<string>())
            .Returns(Task.FromResult(connectionLines));

        var exemptions = await FileUtils.ReadFileContent<List<AgreementExcemption>>(agreementExemptionsFilePath);
        _agreementExcemptionsRepository
            .GetAllAgreementExcemptions(Arg.Any<int>())
            .Returns(Task.FromResult(exemptions));

        var utilitySyncService = new UtilityConnectionsSyncService(
            _agreementPlacesRepository,
            _agreementExcemptionsRepository,
            _utilityConnectionService,
            _syncReportRepository,
            testGeminiClient
        );

        //Act
        var syncReport = await utilitySyncService.SyncUtilityUnitConnections(testCustomerId, placeTypes);

        //Assert
        Assert.Fail("Manual test only");
    }
}
