using Microsoft.Extensions.Options;
using NSubstitute;
using Ymir.GeminiSync.Services.Models;

namespace Ymir.GeminiSync.Services.UnitTests;

public class UtilityConnectionsServiceTests
{
    private readonly IOptions<UtilityConnectionsServiceOptions> _testServiceOptions = Substitute.For<IOptions<UtilityConnectionsServiceOptions>>();
    private readonly UtilityConnectionsServiceOptions _testOptions = new UtilityConnectionsServiceOptions
    {
        PublicContainerNames = new List<string> { "Bruksdel nedgravd", "Hyttecontainer" },
        NotConnectedToPickupSystem = new List<string> { "Hyttecontainer" },
    };

    private readonly UtilityConnectionsService _sut;

    public UtilityConnectionsServiceTests()
    {
        _testServiceOptions.Value.Returns(_testOptions);

        _sut = new UtilityConnectionsService(_testServiceOptions);
    }

    [Fact]
    public void AreTimelinesEqual_WhenBothTimelinesAreEmpty_ReturnsTrue()
    {
        // Arrange
        var firstTimeline = new List<ConnectionTimelineDto>();
        var secondTimeline = new List<ConnectionTimelineDto>();

        var utilityConnectionService = new UtilityConnectionsService(_testServiceOptions);

        // Act
        var result = _sut.AreTimelinesEqual(firstTimeline, secondTimeline);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void AreTimelinesEqual_WhenTimelinesContainEqualItemsInSameOrder_ReturnsTrue()
    {
        // Arrange
        var firstTimeline = new List<ConnectionTimelineDto>
        {
            CreateTimelineEntry(
                agreementId: 1,
                dateFrom: new DateTime(2026, 1, 1),
                dateTo: new DateTime(2026, 3, 31)),
            CreateTimelineEntry(
                agreementId: 1,
                dateFrom: new DateTime(2026, 4, 1),
                dateTo: null)
        };

        var secondTimeline = new List<ConnectionTimelineDto>
        {
            CreateTimelineEntry(
                agreementId: 1,
                dateFrom: new DateTime(2026, 1, 1),
                dateTo: new DateTime(2026, 3, 31)),
            CreateTimelineEntry(
                agreementId: 1,
                dateFrom: new DateTime(2026, 4, 1),
                dateTo: null)
        };

        // Act
        var result = _sut.AreTimelinesEqual(firstTimeline, secondTimeline);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void AreTimelinesEqual_WhenTimelinesHaveDifferentCounts_ReturnsFalse()
    {
        // Arrange
        var firstTimeline = new List<ConnectionTimelineDto>
        {
            CreateTimelineEntry(agreementId: 1)
        };

        var secondTimeline = new List<ConnectionTimelineDto>
        {
            CreateTimelineEntry(agreementId: 1),
            CreateTimelineEntry(agreementId: 2)
        };

        // Act
        var result = _sut.AreTimelinesEqual(firstTimeline, secondTimeline);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void AreTimelinesEqual_WhenAnItemIsDifferent_ReturnsFalse()
    {
        // Arrange
        var firstTimeline = new List<ConnectionTimelineDto>
        {
            CreateTimelineEntry(
                agreementId: 1,
                isConnectedToPublicContainer: false)
        };

        var secondTimeline = new List<ConnectionTimelineDto>
        {
            CreateTimelineEntry(
                agreementId: 1,
                isConnectedToPublicContainer: true)
        };

        // Act
        var result = _sut.AreTimelinesEqual(firstTimeline, secondTimeline);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void AreTimelinesEqual_WhenEqualItemsAreInDifferentOrder_ReturnsFalse()
    {
        // Arrange
        var firstEntry = CreateTimelineEntry(
            agreementId: 1,
            dateFrom: new DateTime(2026, 1, 1));

        var secondEntry = CreateTimelineEntry(
            agreementId: 1,
            dateFrom: new DateTime(2026, 2, 1));

        var firstTimeline = new List<ConnectionTimelineDto>
        {
            firstEntry,
            secondEntry
        };

        var secondTimeline = new List<ConnectionTimelineDto>
        {
            secondEntry,
            firstEntry
        };

        // Act
        var result = _sut.AreTimelinesEqual(firstTimeline, secondTimeline);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void AreTimelinesEqual_WhenBothListsContainSameObjectReferences_ReturnsTrue()
    {
        // Arrange
        var entry = CreateTimelineEntry(agreementId: 1);

        var firstTimeline = new List<ConnectionTimelineDto> { entry };
        var secondTimeline = new List<ConnectionTimelineDto> { entry };

        // Act
        var result = _sut.AreTimelinesEqual(firstTimeline, secondTimeline);

        // Assert
        Assert.True(result);
    }

    private static ConnectionTimelineDto CreateTimelineEntry(
        int agreementId = 1,
        bool isConnectedToGarbagePickupSystem = true,
        bool isConnectedToPublicContainer = false,
        int? includedUtilityUnitsCount = 2,
        UtilityUnitConnectionType utilityUnitConnectionType = default,
        CompostType? compostType = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null)
    {
        return new ConnectionTimelineDto
        {
            AgreementId = agreementId,
            IsConnectedToGarbagePickupSystem = isConnectedToGarbagePickupSystem,
            IsConnectedToPublicContainer = isConnectedToPublicContainer,
            IncludedUtilityUnitsCount = includedUtilityUnitsCount,
            UtilityUnitConnectionType = utilityUnitConnectionType,
            CompostType = compostType,
            DateFrom = dateFrom ?? new DateTime(2026, 1, 1),
            DateTo = dateTo
        };
    }
}
