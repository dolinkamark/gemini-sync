using Ymir.GeminiSync.Domain;

namespace Ymir.GeminiSync.Services.ManualTests;

public class GarbageBinCollectionServiceManualTests
{
    [Fact(Skip = "Manual test only")]
    public async Task BuildStateInTimeCollections_HasBigCollection()
    {
        //Arrange
        const string filePath = "E:\\Temp\\Ymir_sync\\GarbageBins_271.json";
        var collectionLines = await FileUtils.ReadFileContent<List<GarbageBinCollectionLine>>(filePath);
        var geminiServices = new GarbageBinService();

        //Act
        var states = geminiServices.CreateStateInTimeCollections(collectionLines);

        //Assert
        Assert.Fail("Manual test only");
    }
}
