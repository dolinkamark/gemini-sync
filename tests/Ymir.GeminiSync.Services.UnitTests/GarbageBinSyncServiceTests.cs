using NSubstitute;
using Ymir.GeminiSync.Domain;
using Ymir.GeminiSync.Domain.Repositories;
using Ymir.GeminiSync.Services.Abstract;
using Ymir.GeminiSync.Services.Models;

namespace Ymir.GeminiSync.Services.UnitTests;

public class GarbageBinSyncServiceTests
{
    private const int CustomerId = 1;
    private const string PlaceType = "Spann";
    private const int UnchangedPlaceNr = 100;
    private const int ChangedPlaceNr = 200;

    private readonly IGarbageBinCollectionRepository _garbageBinRepository = Substitute.For<IGarbageBinCollectionRepository>();
    private readonly ISyncReportRepository _reportRepository = Substitute.For<ISyncReportRepository>();
    private readonly IGeminiClient _geminiClient = Substitute.For<IGeminiClient>();
    private readonly GarbageBinService _garbageBinService = new();
    private readonly GarbageBinSyncService _sut;

    public GarbageBinSyncServiceTests()
    {
        _geminiClient.UpdateGarbageBinCollection(Arg.Any<GarbageBinsStateInTimeDto>())
            .Returns(Task.FromResult(true));

        _sut = new GarbageBinSyncService(
            _garbageBinRepository,
            _garbageBinService,
            _reportRepository,
            _geminiClient);
    }

    [Fact]
    public async Task SyncGarbageBinCollections_WhenPreviousCollectionMatches_DoesNotUpdate()
    {
        var currentLines = new List<GarbageBinCollectionLine>
        {
            CreateLine(UnchangedPlaceNr, agreementLineId: 1)
        };
        var previousLines = new List<GarbageBinCollectionLine>
        {
            CreateLine(UnchangedPlaceNr, agreementLineId: 1)
        };

        _garbageBinRepository.GetGarbageBinCollections(CustomerId, PlaceType)
            .Returns(Task.FromResult(currentLines));

        var report = await _sut.SyncGarbageBinCollections(CustomerId, PlaceType, previousCollection: previousLines);

        Assert.Equal(1, report.TotalCount);
        Assert.Equal(0, report.UpdatedCount);
        await _geminiClient.DidNotReceive().UpdateGarbageBinCollection(Arg.Any<GarbageBinsStateInTimeDto>());
        await _geminiClient.DidNotReceive().GetGarbageBinCollection(Arg.Any<int>());
        await _reportRepository.Received(1).SaveReport(report);
    }

    [Fact]
    public async Task SyncGarbageBinCollections_WhenPreviousCollectionDiffersOnOnePlace_UpdatesOnlyChangedCollection()
    {
        var currentLines = new List<GarbageBinCollectionLine>
        {
            CreateLine(UnchangedPlaceNr, agreementLineId: 1, shortName: 120),
            CreateLine(ChangedPlaceNr, agreementLineId: 2, shortName: 240)
        };
        var previousLines = new List<GarbageBinCollectionLine>
        {
            CreateLine(UnchangedPlaceNr, agreementLineId: 1, shortName: 120),
            CreateLine(ChangedPlaceNr, agreementLineId: 2, shortName: 120)
        };

        _garbageBinRepository.GetGarbageBinCollections(CustomerId, PlaceType)
            .Returns(Task.FromResult(currentLines));

        var report = await _sut.SyncGarbageBinCollections(CustomerId, PlaceType, previousCollection: previousLines);

        Assert.Equal(2, report.TotalCount);
        Assert.Equal(1, report.UpdatedCount);
        await _geminiClient.Received(1).UpdateGarbageBinCollection(
            Arg.Is<GarbageBinsStateInTimeDto>(dto =>
                dto.StateInTime[0].GarbageBinCollectionId == ChangedPlaceNr));
        await _geminiClient.DidNotReceive().UpdateGarbageBinCollection(
            Arg.Is<GarbageBinsStateInTimeDto>(dto =>
                dto.StateInTime[0].GarbageBinCollectionId == UnchangedPlaceNr));
        await _geminiClient.DidNotReceive().GetGarbageBinCollection(Arg.Any<int>());
    }

    [Fact]
    public async Task SyncGarbageBinCollections_WhenPreviousCollectionIsNull_UpdatesAllCollections()
    {
        var currentLines = new List<GarbageBinCollectionLine>
        {
            CreateLine(UnchangedPlaceNr, agreementLineId: 1),
            CreateLine(ChangedPlaceNr, agreementLineId: 2)
        };

        _garbageBinRepository.GetGarbageBinCollections(CustomerId, PlaceType)
            .Returns(Task.FromResult(currentLines));

        var report = await _sut.SyncGarbageBinCollections(CustomerId, PlaceType, previousCollection: null);

        Assert.Equal(2, report.TotalCount);
        Assert.Equal(2, report.UpdatedCount);
        await _geminiClient.Received(2).UpdateGarbageBinCollection(Arg.Any<GarbageBinsStateInTimeDto>());
        await _geminiClient.DidNotReceive().GetGarbageBinCollection(Arg.Any<int>());
    }

    [Fact]
    public async Task SyncGarbageBinCollections_WhenPreviousCollectionIsNullAndCheckDifferenceMatchesGemini_DoesNotUpdate()
    {
        var currentLines = new List<GarbageBinCollectionLine>
        {
            CreateLine(UnchangedPlaceNr, agreementLineId: 1)
        };
        var geminiState = _garbageBinService
            .CreateGarbageBinsStateInTimeList(currentLines, PlaceType)[0]
            .StateInTime;

        _garbageBinRepository.GetGarbageBinCollections(CustomerId, PlaceType)
            .Returns(Task.FromResult(currentLines));
        _geminiClient.GetGarbageBinCollection(UnchangedPlaceNr)
            .Returns(Task.FromResult(geminiState));

        var report = await _sut.SyncGarbageBinCollections(
            CustomerId,
            PlaceType,
            checkDifference: true,
            previousCollection: null);

        Assert.Equal(1, report.TotalCount);
        Assert.Equal(0, report.UpdatedCount);
        await _geminiClient.Received(1).GetGarbageBinCollection(UnchangedPlaceNr);
        await _geminiClient.DidNotReceive().UpdateGarbageBinCollection(Arg.Any<GarbageBinsStateInTimeDto>());
    }

    [Fact]
    public async Task SyncGarbageBinCollections_WhenPreviousCollectionMatchesAndCheckDifferenceIsTrue_DoesNotCallGemini()
    {
        var currentLines = new List<GarbageBinCollectionLine>
        {
            CreateLine(UnchangedPlaceNr, agreementLineId: 1)
        };
        var previousLines = new List<GarbageBinCollectionLine>
        {
            CreateLine(UnchangedPlaceNr, agreementLineId: 1)
        };

        _garbageBinRepository.GetGarbageBinCollections(CustomerId, PlaceType)
            .Returns(Task.FromResult(currentLines));

        var report = await _sut.SyncGarbageBinCollections(
            CustomerId,
            PlaceType,
            checkDifference: true,
            previousCollection: previousLines);

        Assert.Equal(0, report.UpdatedCount);
        await _geminiClient.DidNotReceive().GetGarbageBinCollection(Arg.Any<int>());
        await _geminiClient.DidNotReceive().UpdateGarbageBinCollection(Arg.Any<GarbageBinsStateInTimeDto>());
    }

    private static GarbageBinCollectionLine CreateLine(
        int placeNr,
        long agreementLineId,
        int shortName = 120)
    {
        return new GarbageBinCollectionLine
        {
            CustomerId = CustomerId,
            PlaceNr = placeNr,
            AgreementLineId = agreementLineId,
            ShortName = shortName,
            FractionName = "Restavfall",
            Frequence = 1,
            HasLock = false,
            FromDate = new DateTime(2020, 1, 1),
            ToDate = new DateTime(1900, 1, 1),
            BuildingType = "01"
        };
    }
}
