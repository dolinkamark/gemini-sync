namespace Ymir.GeminiSync.Services.ManualTests;

public class GarbageBinCollectionServiceManualTests
{
    [Fact]
    public async Task BuildStateInTimeCollections_HasBigCollection()
    {
        //Arrange
        const string filePath = "E:\\Temp\\Ymir_sync\\GarbageBins_271.json";
        var collectionLines = await FileUtils.ReadGarbageBinListAsync(filePath);
        var geminiServices = new GarbageBinCollectionService();

        //Act
        var states = geminiServices.BuildStateInTimeCollections(collectionLines);

        //Assert
        Assert.Fail("Manual test only");
    }
}
