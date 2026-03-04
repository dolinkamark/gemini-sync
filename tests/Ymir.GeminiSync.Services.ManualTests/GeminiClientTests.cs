using NSubstitute;
using System.Text.Json;
using Ymir.GeminiSync.Services.Models;
using Ymir.GeminiSync.Services.Models.Containers;
using Ymir.GeminiSync.Services.Settings;

namespace Ymir.GeminiSync.Services.ManualTests;

public class GeminiClientTests
{
    private readonly HttpClient _testHttpClient;
    private readonly IHttpClientFactory _httpClientFactory = Substitute.For<IHttpClientFactory>();

    private readonly GeminiSettings _settings = new GeminiSettings
    {
        BaseUrl = "https://powelqapfpublicapi.azure-api.net/public",
        MunicipalityNo = "stavangerkundetest",
        SubscriptionKey = "3d8d028ee9be4cc9a9e4ac0a92068966"
    };

    public GeminiClientTests()
    {
        _testHttpClient = new HttpClient();
        _httpClientFactory
            .CreateClient(Arg.Any<string>())
            .Returns(_testHttpClient);
    }

    [Fact(Skip = "Manual test only")]
    public async Task GetGarbageBinCollectionTest()
    {
        //Arrange
        var testGeminiClient = new GeminiClient(_settings, _httpClientFactory);
        int testCollectionId = 2;

        //Act
        var garbageBinCollection = await testGeminiClient.GetGarbageBinCollection(testCollectionId);

        //Assert
        Assert.Fail("Manual test only");
    }

    [Fact(Skip = "Manual test only")]
    public async Task UpdateGarbageBinCollectionTest()
    {
        //Arrange
        var testGeminiClient = new GeminiClient(_settings, _httpClientFactory);
        var testCollection = new List<GarbageBinsCollectionDto>
        {
            new GarbageBinsCollectionDto
            {
                GarbageBinCollectionId = 2,
                NumberOfConnectedUtilityUnit = 1,
                UtilityUnitType = GarbageBinUtilityUnitType.Housing,
                GarbageBins = new List<GarbageBinDto>
                {
                    new GarbageBinDto
                    {
                        GarbageBinId = 1,
                        BinSize = 240,
                        GarbageBinCategory = GarbageBinCategory.OtherWaste,
                        FrequencyToBeInvoiced = GarbageBinsFrequencyToBeInvoiced.None
                    }
                },
                InEffectFrom = new DateTime(2025, 10, 12, 0, 0, 0, DateTimeKind.Utc)
            }
        };
        var stateInTime = new GarbageBinsStateInTimeDto
        {
            StateInTime = testCollection
        };

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        options.Converters.Add(new UtcDateTimeConverter());

        var test = JsonSerializer.Serialize(stateInTime, options);

        //Act
        var isSuccessful = await testGeminiClient.UpdateGarbageBinCollection(stateInTime);

        //Assert
        Assert.Fail("Manual test only");
    }

    [Fact(Skip = "Manual test only")]
    public async Task GetGarbageBinPickupsTest()
    {
        //Arrange
        var testGeminiClient = new GeminiClient(_settings, _httpClientFactory);
        const int testCollectionId = 37962;

        //Act
        var garbageBinPickups = await testGeminiClient.GetGarbageBinPickups(testCollectionId);

        //Assert
        Assert.Fail("Manual test only");
    }

    [Fact(Skip = "Manual test only")]
    public async Task AddGarbageBinPickupTest()
    {
        //Arrange
        var testGeminiClient = new GeminiClient(_settings, _httpClientFactory);
        const int testCollectionId = 37962;

        var testGarbageBinPickup = new GarbagePickupDto
        {
            GarbageBinCollectionId = testCollectionId,
            GarbageBinPickUpId = 2,
            UtilityUnitType = GarbageBinUtilityUnitType.Housing,
            ExecutedDate = new DateTime(2026, 1, 1, 12, 0, 0),
            ExtraPickup = false,
            GarbageBins = new List<GarbageSingleBinPickupDto>
            {
                new GarbageSingleBinPickupDto
                {
                    GarbageBinId = 8304348,
                    BinSize = 120,
                    GarbageBinCategory = GarbageBinCategory.OtherWaste
                }
            }
        };

        //Act
        var addResult = await testGeminiClient.AddGarbageBinPickup(testGarbageBinPickup);

        //Assert
        Assert.Fail("Manual test only");
    }

    [Fact(Skip = "Manual test only")]
    public async Task DeleteGarbageBinPickupTest()
    {
        //Arrange
        var testGeminiClient = new GeminiClient(_settings, _httpClientFactory);
        const int testCollectionId = 37962;
        const int testPickupId = 1;

        //Act
        var deleteResult = await testGeminiClient.DeleteGarbageBinPickup(testCollectionId, testPickupId);

        //Assert
        Assert.Fail("Manual test only");
    }

    [Fact(Skip = "Manual test only")]
    public async Task GetFractionInTimeTest()
    {
        //Arrange
        var testGeminiClient = new GeminiClient(_settings, _httpClientFactory);
        int testCollectionId = 1;

        //Act
        var fractionsInTime = await testGeminiClient.GetFractionsInTime(testCollectionId);

        //Assert
        Assert.Fail("Manual test only");
    }

    [Fact(Skip = "Manual test only")]
    public async Task GetPrivateContainerGroupFractionsTest()
    {
        //Arrange
        var testGeminiClient = new GeminiClient(_settings, _httpClientFactory);
        int testCollectionId = 1;

        //Act
        var fractionResponse = await testGeminiClient.GetPrivateContainerGroupFractions(testCollectionId);

        //Assert
        Assert.Fail("Manual test only");
    }

    [Fact(Skip = "Manual test only")]
    public async Task UpdatePrivateContainerGroupFractionsTest()
    {
        //Arrange
        var testGeminiClient = new GeminiClient(_settings, _httpClientFactory);
        int testCollectionId = 1;

        var fractions = new List<PrivateContainerGroupAgreementFractions>
        {
            new PrivateContainerGroupAgreementFractions()
            {
                AgreementId = 1,
                FractionsInTime = new List<PrivateContainerGroupFractionInTime>
                {
                    new PrivateContainerGroupFractionInTime
                    {
                        DateFrom = DateTime.UtcNow.AddDays(-5).AddHours(12),
                        FractionNumerator = 1,
                        FractionDenominator = 1,
                    }
                }
            }
        };

        //Act
        var fractionResponse = await testGeminiClient.UpdatePrivateContainerGroupFractions(testCollectionId, fractions);

        //Assert
        Assert.Fail("Manual test only");
    }

    [Fact(Skip = "Manual test only")]
    public async Task GetUtilityConnectionTimeline()
    {
        //Arrange
        const int testAgreementId = 33970;
        var testGeminiClient = new GeminiClient(_settings, _httpClientFactory);

        //Act
        var connectionTimeline = await testGeminiClient.GetUtilityConnectionTimeline(testAgreementId);

        //Assert
        Assert.Fail("Manual test only");
    }

    [Fact(Skip = "Manual test only")]
    public async Task UpdateUtilityConnectionTimeline()
    {
        //Arrange
        const int testAgreementId = 33970;
        var testGeminiClient = new GeminiClient(_settings, _httpClientFactory);

        var connectionTimeline = new List<ConnectionTimelineDto>
        {
            new ConnectionTimelineDto
            {
                AgreementId = 17571,
                IsConnectedToGarbagePickupSystem = true,
                IsConnectedToPublicContainer = true,
                DateFrom = new DateTime(2014, 11, 1),
                UtilityUnitConnectionType = UtilityUnitConnectionType.Housing
            }
        };

        var updateRequest = new UtilityUnitConnectionUpdateDto
        {
            ConnectionsInTime = connectionTimeline
        };

        //Act
        var isSuccessful = await testGeminiClient.UpdateUtilityConnectionTimeline(testAgreementId, updateRequest);

        //Assert
        Assert.Fail("Manual test only");
    }
}
