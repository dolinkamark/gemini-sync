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
        ExemptionMaps = new List<ExemptionMap>
        {
            new ExemptionMap { Id = 5, IsFullExemption = true },
            new ExemptionMap { Id = 6, CompostType = CompostType.Food, },
            new ExemptionMap { Id = 7, CompostType = CompostType.GardenAndFood, },
        }
    };

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
    public async Task UptadeAllUtilityConnections()
    {
        //Arrange
        const string basePath = "E:\\Temp\\Ymir_Sync\\sync_20260902\\utility_connections_20260902";
        const string previousBasePath = "E:\\Temp\\Ymir_Sync\\utilityunits_20260813_01";

        const string filePath = "agreement_places_20260902.json";
        const string agreementExemptionsFilePath = "agreement_exemptions_20260902.json";
        const string previousFilePath = "agreement_places_20260813.json";
        const string previousExemptionsFilePath = "agreement_exemptions_20260813.json";

        const int testCustomerId = 1;

        var testGeminiClient = new GeminiClient(_settings, _httpClientFactory);

        var connectionLines = await FileUtils.ReadFileContent<List<AgreementPlaceConnectionLine>>(Path.Join(basePath, filePath));

        _agreementPlacesRepository
            .GetAllUtilityUnitConnections(Arg.Any<int>())
            .Returns(Task.FromResult(connectionLines));

        var exemptions = await FileUtils.ReadFileContent<List<AgreementExcemption>>(Path.Join(basePath, agreementExemptionsFilePath));
        _agreementExcemptionsRepository
            .GetAllAgreementExcemptions(Arg.Any<int>())
            .Returns(Task.FromResult(exemptions));

        var previousConnectionLines = await FileUtils.ReadFileContent<List<AgreementPlaceConnectionLine>>(
            Path.Join(previousBasePath, previousFilePath));
        var previousExemptions = await FileUtils.ReadFileContent<List<AgreementExcemption>>(
            Path.Join(previousBasePath, previousExemptionsFilePath));

        var utilitySyncService = new UtilityConnectionsSyncService(
            _agreementPlacesRepository,
            _agreementExcemptionsRepository,
            _utilityConnectionService,
            _syncReportRepository,
            testGeminiClient
        );

        //Act
        var syncReport = await utilitySyncService.SyncUtilityUnitConnections(
            testCustomerId,
            checkDifference: false,
            previousConnections: previousConnectionLines,
            previousExemptions: previousExemptions);

        //Assert
        Assert.Fail("Manual test only");
    }

    [Fact(Skip = "Manual test only")]
    public async Task UptadeUtilityConnectionsByPlace()
    {
        //Arrange
        const string basePath = "E:\\Temp\\Ymir\\utility_unit_connections_all_20260716";

        const string filePath = "agreement_places_20260716.json";
        const string agreementExemptionsFilePath = "agreement_exemptions_20260716.json";

        const int testCustomerId = 2;
        const string placeTypes = "Nedgravd privat";

        var testGeminiClient = new GeminiClient(_settings, _httpClientFactory);

        var connectionLines = await FileUtils.ReadFileContent<List<AgreementPlaceConnectionLine>>(Path.Join(basePath, filePath));
        _agreementPlacesRepository
            .GetUtilityUnitConnections(Arg.Any<int>(), Arg.Any<string>())
            .Returns(Task.FromResult(connectionLines));

        var exemptions = await FileUtils.ReadFileContent<List<AgreementExcemption>>(Path.Join(basePath, agreementExemptionsFilePath));
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
        var syncReport = await utilitySyncService.SyncUtilityUnitConnectionsByPlace(testCustomerId, placeTypes, true);

        //Assert
        Assert.Fail("Manual test only");
    }
}
