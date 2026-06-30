using NSubstitute;
using Ymir.GeminiSync.Services.Settings;

namespace Ymir.GeminiSync.Services.ManualTests;

public class GeminiDeleteManualTests
{
    private readonly HttpClient _testHttpClient;
    private readonly IHttpClientFactory _httpClientFactory = Substitute.For<IHttpClientFactory>();

    private readonly GeminiSettings _settings = new GeminiSettings
    {
        BaseUrl = "https://powelqapfpublicapi.azure-api.net/public",
        MunicipalityNo = "stavangerkundetest",
        SubscriptionKey = "3d8d028ee9be4cc9a9e4ac0a92068966"
    };

    public GeminiDeleteManualTests()
    {
        _testHttpClient = new HttpClient();
        _httpClientFactory
            .CreateClient(Arg.Any<string>())
            .Returns(_testHttpClient);
    }

    [Fact]
    public async Task DeleteGarbageBinCollections()
    {
        //Arrange
        const string filePath = "E:\\Temp\\Ymir\\20260622\\garbage_bins_20260623.json";
        var testGeminiClient = new GeminiClient(_settings, _httpClientFactory);

        var collectionLines = await FileUtils.ReadGarbageBinListAsync(filePath);

        //Act
        var placeNumbers = collectionLines
            .GroupBy(c => c.PlaceNr)
            .Select(g => g.Key)
            .OrderBy(k => k)
            .ToList();

        //Assert
        Assert.Fail("Manual test only");
    }
}
