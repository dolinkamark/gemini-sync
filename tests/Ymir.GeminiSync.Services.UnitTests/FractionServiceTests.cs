using System.Text.Json;
using Ymir.GeminiSync.Domain;
using Ymir.GeminiSync.Services.Abstract;
using Ymir.GeminiSync.Services.Models;

namespace Ymir.GeminiSync.Services.UnitTests;

public class FractionServiceTests
{
    [Fact]
    public async Task BuildFractionIntervalsByDate_HasTwoIntervals_ReturnsTwoIntervals()
    {
        //Arrange
        const string testFilePath = "Data\\Fractions_1185842.json";
        var placeLines = await FileUtils.ReadFileContent<List<AgreementPlaceHistoryLine>>(testFilePath);

        var fractionService = new FractionService();

        //Act
        var fractionsByInterval = fractionService.BuildFractionIntervalsByDate(placeLines);

        //Assert
        Assert.Equal(2, fractionsByInterval.Count);
    }

    [Fact]
    public async Task BuildFractionIntervalsByDate_HasFiveAgreements_ContainsAgreements()
    {
        //Arrange
        const string testFilePath = "Data\\Fractions_1185842.json";

        var expectedAgreements = new List<int> { 51148, 51300, 56504, 56505, 64108 };
        var placeLines = await FileUtils.ReadFileContent<List<AgreementPlaceHistoryLine>>(testFilePath);

        var fractionService = new FractionService();

        //Act
        var fractionsByInterval = fractionService.BuildFractionIntervalsByDate(placeLines);

        var testContent = JsonSerializer.Serialize(fractionsByInterval);

        //Assert
        foreach (var agreementId in expectedAgreements)
        {
            Assert.True(fractionsByInterval.Any(i => i.AgreementOccupancyList.Any(a => a.AgreementId == agreementId)));
        }
    }

    [Fact]
    public async Task CreateFractionTimelines_Test()
    {
        //Arrange
        const string testFilePath = "Data\\FractionIntervals_1185842.json";
        var testFractionIntervals = await FileUtils.ReadFileContent<List<PlaceAgreementInterval>>(testFilePath);

        var fractionService = new FractionService();

        //Act
        var fractionTimelines = fractionService.CreateFractionTimelines(testFractionIntervals);

        //Assert
        Assert.Fail("Unfinished test");
    }

    [Fact]
    public void AreFractionTimelinesEqual_SameTimelines_ReturnsTrue()
    {
        var fractionService = new FractionService();
        var first = CreateTimelines(CreateEntry(new DateTime(2026, 1, 1), new DateTime(2026, 6, 30), 1, 2));
        var second = CreateTimelines(CreateEntry(new DateTime(2026, 1, 1), new DateTime(2026, 6, 30), 1, 2));

        Assert.True(fractionService.AreFractionTimelinesEqual(first, second));
    }

    [Fact]
    public void AreFractionTimelinesEqual_DifferentAgreementOrder_ReturnsTrue()
    {
        var fractionService = new FractionService();
        var first = new List<AgreementFractionTimeline>
        {
            CreateTimeline(100, CreateEntry(new DateTime(2026, 1, 1), null, 1, 1)),
            CreateTimeline(200, CreateEntry(new DateTime(2026, 1, 1), null, 1, 1)),
        };
        var second = new List<AgreementFractionTimeline>
        {
            CreateTimeline(200, CreateEntry(new DateTime(2026, 1, 1), null, 1, 1)),
            CreateTimeline(100, CreateEntry(new DateTime(2026, 1, 1), null, 1, 1)),
        };

        Assert.True(fractionService.AreFractionTimelinesEqual(first, second));
    }

    [Fact]
    public void AreFractionTimelinesEqual_DifferentDate_ReturnsFalse()
    {
        var fractionService = new FractionService();
        var first = CreateTimelines(CreateEntry(new DateTime(2026, 1, 1), new DateTime(2026, 6, 30), 1, 2));
        var second = CreateTimelines(CreateEntry(new DateTime(2026, 1, 1), new DateTime(2026, 7, 31), 1, 2));

        Assert.False(fractionService.AreFractionTimelinesEqual(first, second));
    }

    [Fact]
    public void AreFractionTimelinesEqual_DifferentFraction_ReturnsFalse()
    {
        var fractionService = new FractionService();
        var first = CreateTimelines(CreateEntry(new DateTime(2026, 1, 1), null, 1, 2));
        var second = CreateTimelines(CreateEntry(new DateTime(2026, 1, 1), null, 1, 3));

        Assert.False(fractionService.AreFractionTimelinesEqual(first, second));
    }

    [Fact]
    public void GetChangedTimelines_UnchangedPlaceNr_IsDropped()
    {
        var fractionService = new FractionService();
        var current = new List<(int, List<AgreementFractionTimeline>)>
        {
            (1, CreateTimelines(CreateEntry(new DateTime(2026, 1, 1), null, 1, 1))),
        };
        var previous = new List<(int, List<AgreementFractionTimeline>)>
        {
            (1, CreateTimelines(CreateEntry(new DateTime(2026, 1, 1), null, 1, 1))),
        };

        var changed = fractionService.GetChangedTimelines(current, previous);

        Assert.Empty(changed);
    }

    [Fact]
    public void GetChangedTimelines_ChangedPlaceNr_IsKept()
    {
        var fractionService = new FractionService();
        var current = new List<(int, List<AgreementFractionTimeline>)>
        {
            (1, CreateTimelines(CreateEntry(new DateTime(2026, 1, 1), null, 1, 2))),
        };
        var previous = new List<(int, List<AgreementFractionTimeline>)>
        {
            (1, CreateTimelines(CreateEntry(new DateTime(2026, 1, 1), null, 1, 1))),
        };

        var changed = fractionService.GetChangedTimelines(current, previous);

        Assert.Single(changed);
        Assert.Equal(1, changed[0].Item1);
    }

    [Fact]
    public void GetChangedTimelines_NewPlaceNr_IsKept()
    {
        var fractionService = new FractionService();
        var current = new List<(int, List<AgreementFractionTimeline>)>
        {
            (1, CreateTimelines(CreateEntry(new DateTime(2026, 1, 1), null, 1, 1))),
            (2, CreateTimelines(CreateEntry(new DateTime(2026, 1, 1), null, 1, 1))),
        };
        var previous = new List<(int, List<AgreementFractionTimeline>)>
        {
            (1, CreateTimelines(CreateEntry(new DateTime(2026, 1, 1), null, 1, 1))),
        };

        var changed = fractionService.GetChangedTimelines(current, previous);

        Assert.Single(changed);
        Assert.Equal(2, changed[0].Item1);
    }

    private static List<AgreementFractionTimeline> CreateTimelines(params FractionTimeEntry[] entries)
    {
        return new List<AgreementFractionTimeline>
        {
            CreateTimeline(100, entries),
        };
    }

    private static AgreementFractionTimeline CreateTimeline(long agreementId, params FractionTimeEntry[] entries)
    {
        return new AgreementFractionTimeline
        {
            AgreementId = agreementId,
            FractionsInTime = entries.ToList(),
        };
    }

    private static FractionTimeEntry CreateEntry(DateTime dateFrom, DateTime? dateTo, int numerator, int denominator)
    {
        return new FractionTimeEntry
        {
            DateFrom = dateFrom,
            DateTo = dateTo,
            FractionNumerator = numerator,
            FractionDenominator = denominator,
        };
    }
}
