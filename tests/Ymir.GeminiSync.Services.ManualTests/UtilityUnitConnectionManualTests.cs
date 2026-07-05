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
    private readonly IUtilityConnectionsService _utilityConnectionService = Substitute.For<IUtilityConnectionsService>();
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
    }

    [Fact]
    public async Task UptadeUtilityConnections()
    {
        //Arrange
        const string filePath = "E:\\Temp\\Ymir\\202607\\agreement_places_Hyttecontainer_20260703.json";
        const string agreementExemptionsFilePath = "E:\\Temp\\Ymir\\202607\\agreement_exemptions_Spann_20260702.json";
        const int testCustomerId = 2;
        const string placeTypes = "Sekk i spann";

        var testGeminiClient = new GeminiClient(_settings, _httpClientFactory);

        var connectionLines = await FileUtils.ReadFileContent<List<AgreementPlaceConnectionLine>>(filePath);
        _agreementPlacesRepository
            .GetUtilityUnitConnections(Arg.Any<int>(), Arg.Any<string>())
            .Returns(Task.FromResult(connectionLines));

        var utilitySyncService = new UtilityConnectionsSyncService(
            _agreementPlacesRepository,
            _agreementExcemptionsRepository,
            _utilityConnectionService,
            _syncReportRepository,
            testGeminiClient
        );

        //Act
        var syncReport = utilitySyncService.SyncUtilityUnitConnections(testCustomerId, placeTypes);

        //Assert
        Assert.Fail("Manual test only");
    }

    //Exemptions
    [Fact]
    public async Task UptadeUtilityConnectionsWithCompost()
    {
        //Arrange
        const string utilityConnectionsFilePath = "E:\\Temp\\Ymir\\202607\\agreement_places_Spann_20260702.json";
        const string agreementExemptionsFilePath = "E:\\Temp\\Ymir\\202607\\agreement_exemptions_Spann_20260702.json";
        const string publicContainerName = "Bruksdel nedgravd";

        var errorCollection = new List<(long, string)>();
        var updateCount = 0;

        var testGeminiClient = new GeminiClient(_settings, _httpClientFactory);

        var connectionLines = await FileUtils.ReadFileContent<List<AgreementPlaceConnectionLine>>(utilityConnectionsFilePath);
        var exemptions = await FileUtils.ReadFileContent<List<AgreementExcemption>>(agreementExemptionsFilePath);

        var relatedExemptions = exemptions
            .Where(e => e.ExcemptionType == 2 || e.ExcemptionType == 4)
            .ToList();

        var agreementsWithExemption = connectionLines
            .Where(l => relatedExemptions.Any(e => e.AgreementId == l.AgreementId))
            .ToList();

        var agreementGroups = agreementsWithExemption
            .Where(l => !String.IsNullOrWhiteSpace(l.ExternalAgreementId))
            .GroupBy(l => l.AgreementId)
            .ToList();

        foreach (var agreementGroup in agreementGroups)
        {
            var timelines = new List<ConnectionTimelineDto>();
            var currentLines = agreementGroup
                .OrderBy(l => l.FromDate)
                .ToList();

            var agreementExemptions = relatedExemptions
                .Where(e => e.AgreementId == agreementGroup.Key && e.ToDate == null)
                .ToList();

            //Closed intervals
            for (int i = 0; i < currentLines.Count - 1; i++)
            {
                bool isPublicContainer = currentLines[i].PlaceType.ToLower() == publicContainerName.ToLower();
                bool isCabin = !string.IsNullOrWhiteSpace(currentLines[i].BuildingType) && currentLines[i].BuildingType.StartsWith("16");

                timelines.Add(new ConnectionTimelineDto
                {
                    AgreementId = Int32.Parse(currentLines[i].ExternalAgreementId),
                    IsConnectedToGarbagePickupSystem = true,
                    IsConnectedToPublicContainer = isPublicContainer,
                    DateFrom = currentLines[i].FromDate.AddHours(12),
                    DateTo = currentLines[i + 1].FromDate.AddHours(-12),
                    UtilityUnitConnectionType = isCabin ? UtilityUnitConnectionType.Cabin : UtilityUnitConnectionType.Housing
                });
            }

            //Last interval
            var compostType = agreementExemptions.Any(e => e.ExcemptionType == 4) ? CompostType.GardenAndFood : CompostType.Food;

            var lastInterval = currentLines[^1];
            bool isLastCabin = !string.IsNullOrWhiteSpace(lastInterval.BuildingType) && lastInterval.BuildingType.StartsWith("16");

            timelines.Add(new ConnectionTimelineDto
            {
                AgreementId = Int32.Parse(lastInterval.ExternalAgreementId),
                IsConnectedToGarbagePickupSystem = true,
                IsConnectedToPublicContainer = lastInterval.PlaceType?.ToLower() == publicContainerName.ToLower(),
                DateFrom = lastInterval.FromDate.AddHours(12),
                DateTo = lastInterval.ToDate?.AddHours(12),
                CompostType = compostType,
                UtilityUnitConnectionType = isLastCabin ? UtilityUnitConnectionType.Cabin : UtilityUnitConnectionType.Housing
            });

            var updateDto = new UtilityUnitConnectionUpdateDto
            {
                ConnectionsInTime = timelines
            };

            try
            {
                var isSuccessful = await testGeminiClient.UpdateUtilityConnectionTimeline((int)agreementGroup.Key, updateDto);

                if (isSuccessful)
                {
                    updateCount++;
                }
                else
                {
                    errorCollection.Add((agreementGroup.Key, $"Update failed for agreementId: {agreementGroup.Key}"));
                }

            }
            catch (Exception ex)
            {
                errorCollection.Add((agreementGroup.Key, ex.ToString()));
                Console.WriteLine($"Ooops error at {lastInterval.ExternalAgreementId}");
            }
        }

        //Assert
        Assert.Fail("Manual test only");
    }
}
