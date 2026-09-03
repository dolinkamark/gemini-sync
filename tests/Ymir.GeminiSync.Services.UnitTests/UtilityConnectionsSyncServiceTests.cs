using Microsoft.Extensions.Options;
using NSubstitute;
using Ymir.GeminiSync.Domain;
using Ymir.GeminiSync.Domain.Repositories;
using Ymir.GeminiSync.Services.Abstract;
using Ymir.GeminiSync.Services.Models;

namespace Ymir.GeminiSync.Services.UnitTests;

public class UtilityConnectionsSyncServiceTests
{
    private const int CustomerId = 1;
    private const long UnchangedAgreementId = 100;
    private const long ChangedAgreementId = 200;

    private readonly IAgreementPlacesRepository _agreementPlacesRepository = Substitute.For<IAgreementPlacesRepository>();
    private readonly IAgreementExcemptionRepository _agreementExcemptionRepository = Substitute.For<IAgreementExcemptionRepository>();
    private readonly ISyncReportRepository _reportRepository = Substitute.For<ISyncReportRepository>();
    private readonly IGeminiClient _geminiClient = Substitute.For<IGeminiClient>();
    private readonly UtilityConnectionsService _utilityConnectionService;
    private readonly UtilityConnectionsSyncService _sut;

    public UtilityConnectionsSyncServiceTests()
    {
        var serviceOptions = Substitute.For<IOptions<UtilityConnectionsServiceOptions>>();
        serviceOptions.Value.Returns(new UtilityConnectionsServiceOptions
        {
            PublicContainerNames = new List<string> { "Bruksdel nedgravd", "Hyttecontainer" },
            NotConnectedToPickupSystem = new List<string> { "Hyttecontainer" },
            ExemptionMaps = new List<ExemptionMap>
            {
                new() { Id = 5, IsFullExemption = true },
                new() { Id = 6, CompostType = CompostType.Food },
                new() { Id = 7, CompostType = CompostType.GardenAndFood },
            }
        });

        _utilityConnectionService = new UtilityConnectionsService(serviceOptions);

        _geminiClient.UpdateUtilityConnectionTimeline(Arg.Any<long>(), Arg.Any<UtilityUnitConnectionUpdateDto>())
            .Returns(Task.FromResult(true));
        _agreementExcemptionRepository.GetAllAgreementExcemptions(CustomerId)
            .Returns(Task.FromResult(new List<AgreementExcemption>()));

        _sut = new UtilityConnectionsSyncService(
            _agreementPlacesRepository,
            _agreementExcemptionRepository,
            _utilityConnectionService,
            _reportRepository,
            _geminiClient);
    }

    [Fact]
    public async Task SyncUtilityUnitConnections_WhenPreviousSnapshotMatches_DoesNotUpdate()
    {
        var currentLines = new List<AgreementPlaceConnectionLine>
        {
            CreateLine(UnchangedAgreementId)
        };
        var previousLines = new List<AgreementPlaceConnectionLine>
        {
            CreateLine(UnchangedAgreementId)
        };

        _agreementPlacesRepository.GetAllUtilityUnitConnections(CustomerId)
            .Returns(Task.FromResult(currentLines));

        var report = await _sut.SyncUtilityUnitConnections(
            CustomerId,
            previousConnections: previousLines,
            previousExemptions: new List<AgreementExcemption>());

        Assert.Equal(1, report.TotalCount);
        Assert.Equal(0, report.UpdatedCount);
        await _geminiClient.DidNotReceive().UpdateUtilityConnectionTimeline(
            Arg.Any<long>(), Arg.Any<UtilityUnitConnectionUpdateDto>());
        await _geminiClient.DidNotReceive().GetUtilityConnectionTimeline(Arg.Any<long>());
        await _reportRepository.Received(1).SaveReport(report);
    }

    [Fact]
    public async Task SyncUtilityUnitConnections_WhenPreviousSnapshotDiffersOnOneAgreement_UpdatesOnlyChangedAgreement()
    {
        var currentLines = new List<AgreementPlaceConnectionLine>
        {
            CreateLine(UnchangedAgreementId, occupancyUnits: 1),
            CreateLine(ChangedAgreementId, occupancyUnits: 2)
        };
        var previousLines = new List<AgreementPlaceConnectionLine>
        {
            CreateLine(UnchangedAgreementId, occupancyUnits: 1),
            CreateLine(ChangedAgreementId, occupancyUnits: 1)
        };

        _agreementPlacesRepository.GetAllUtilityUnitConnections(CustomerId)
            .Returns(Task.FromResult(currentLines));

        var report = await _sut.SyncUtilityUnitConnections(
            CustomerId,
            previousConnections: previousLines,
            previousExemptions: new List<AgreementExcemption>());

        Assert.Equal(2, report.TotalCount);
        Assert.Equal(1, report.UpdatedCount);
        await _geminiClient.Received(1).UpdateUtilityConnectionTimeline(
            ChangedAgreementId, Arg.Any<UtilityUnitConnectionUpdateDto>());
        await _geminiClient.DidNotReceive().UpdateUtilityConnectionTimeline(
            UnchangedAgreementId, Arg.Any<UtilityUnitConnectionUpdateDto>());
        await _geminiClient.DidNotReceive().GetUtilityConnectionTimeline(Arg.Any<long>());
    }

    [Fact]
    public async Task SyncUtilityUnitConnections_WhenPreviousConnectionsIsNull_UpdatesAllAgreements()
    {
        var currentLines = new List<AgreementPlaceConnectionLine>
        {
            CreateLine(UnchangedAgreementId),
            CreateLine(ChangedAgreementId)
        };

        _agreementPlacesRepository.GetAllUtilityUnitConnections(CustomerId)
            .Returns(Task.FromResult(currentLines));

        var report = await _sut.SyncUtilityUnitConnections(
            CustomerId,
            previousConnections: null);

        Assert.Equal(2, report.TotalCount);
        Assert.Equal(2, report.UpdatedCount);
        await _geminiClient.Received(2).UpdateUtilityConnectionTimeline(
            Arg.Any<long>(), Arg.Any<UtilityUnitConnectionUpdateDto>());
        await _geminiClient.DidNotReceive().GetUtilityConnectionTimeline(Arg.Any<long>());
    }

    [Fact]
    public async Task SyncUtilityUnitConnections_WhenPreviousConnectionsIsNullAndCheckDifferenceMatchesGemini_DoesNotUpdate()
    {
        var currentLines = new List<AgreementPlaceConnectionLine>
        {
            CreateLine(UnchangedAgreementId)
        };
        var geminiTimeline = _utilityConnectionService
            .CreateUtilityUnitTimelines(
                new List<AgreementPlaceConnectionLine> { CreateLine(UnchangedAgreementId) },
                new List<AgreementExcemption>())[0]
            .Item2.ConnectionsInTime;

        _agreementPlacesRepository.GetAllUtilityUnitConnections(CustomerId)
            .Returns(Task.FromResult(currentLines));
        _geminiClient.GetUtilityConnectionTimeline(UnchangedAgreementId)
            .Returns(Task.FromResult(geminiTimeline));

        var report = await _sut.SyncUtilityUnitConnections(
            CustomerId,
            checkDifference: true,
            previousConnections: null);

        Assert.Equal(1, report.TotalCount);
        Assert.Equal(0, report.UpdatedCount);
        await _geminiClient.Received(1).GetUtilityConnectionTimeline(UnchangedAgreementId);
        await _geminiClient.DidNotReceive().UpdateUtilityConnectionTimeline(
            Arg.Any<long>(), Arg.Any<UtilityUnitConnectionUpdateDto>());
    }

    [Fact]
    public async Task SyncUtilityUnitConnections_WhenPreviousSnapshotMatchesAndCheckDifferenceIsTrue_DoesNotCallGemini()
    {
        var currentLines = new List<AgreementPlaceConnectionLine>
        {
            CreateLine(UnchangedAgreementId)
        };
        var previousLines = new List<AgreementPlaceConnectionLine>
        {
            CreateLine(UnchangedAgreementId)
        };

        _agreementPlacesRepository.GetAllUtilityUnitConnections(CustomerId)
            .Returns(Task.FromResult(currentLines));

        var report = await _sut.SyncUtilityUnitConnections(
            CustomerId,
            checkDifference: true,
            previousConnections: previousLines,
            previousExemptions: new List<AgreementExcemption>());

        Assert.Equal(0, report.UpdatedCount);
        await _geminiClient.DidNotReceive().GetUtilityConnectionTimeline(Arg.Any<long>());
        await _geminiClient.DidNotReceive().UpdateUtilityConnectionTimeline(
            Arg.Any<long>(), Arg.Any<UtilityUnitConnectionUpdateDto>());
    }

    private static AgreementPlaceConnectionLine CreateLine(
        long agreementId,
        int occupancyUnits = 1)
    {
        return new AgreementPlaceConnectionLine
        {
            CustomerId = CustomerId,
            AgreementId = agreementId,
            ExternalAgreementId = agreementId.ToString(),
            PlaceNr = (int)agreementId,
            PlaceType = "Spann",
            BuildingType = "01",
            Bid = $"bid-{agreementId}",
            NrOfOccupancyUnits = occupancyUnits,
            FromDate = new DateTime(2020, 1, 1),
            ToDate = null
        };
    }
}
