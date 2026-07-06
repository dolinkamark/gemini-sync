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
}
