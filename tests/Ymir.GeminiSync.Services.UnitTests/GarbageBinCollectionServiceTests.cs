using Ymir.GeminiSync.Domain;

namespace Ymir.GeminiSync.Services.UnitTests;

public class GarbageBinCollectionServiceTests
{
    [Fact]
    public async Task BuildStateInTimeCollections_MultipleBins_SingleGeminiAgreement()
    {
        //Arrange
        const string filePath = "Data\\GarbageBins_SingleGeminiAgreement.json";
        var collectionLines = await FileUtils.ReadFileContent<List<GarbageBinCollectionLine>>(filePath);
        var geminiServices = new GarbageBinCollectionBuilder();

        //Act
        var states = geminiServices.BuildStateInTimeCollections(collectionLines);

        //Assert
        Assert.Single(states);
        Assert.Equal(4, states[0].Lines.Count);
    }

    [Fact]
    public async Task BuildStateInTimeCollections_HasToDates_SingleGeminiAgreement_ReturnsHasTwoStates()
    {
        //Arrange
        const string filePath = "Data\\GarbageBins_HasToDates.json";
        var collectionLines = await FileUtils.ReadFileContent<List<GarbageBinCollectionLine>>(filePath);
        var geminiServices = new GarbageBinCollectionBuilder();

        //Act
        var states = geminiServices.BuildStateInTimeCollections(collectionLines);

        //Assert
        Assert.Equal(2, states.Count);
    }

    [Fact]
    public async Task BuildStateInTimeCollections_HasBigCollection()
    {
        //Arrange
        const string filePath = "Data\\GarbageBins_271.json";
        var collectionLines = await FileUtils.ReadFileContent<List<GarbageBinCollectionLine>>(filePath);
        var geminiServices = new GarbageBinCollectionBuilder();

        //Act
        var states = geminiServices.BuildStateInTimeCollections(collectionLines);

        //Assert
        Assert.Fail("Unfinished test");
    }
}
