using NSubstitute;
using System.Text.Json;
using Ymir.GeminiSync.Services.Models;
using Ymir.GeminiSync.Services.Settings;

namespace Ymir.GeminiSync.Services.ManualTests;

public class GeminiSyncManualTest
{
    private readonly IHttpClientFactory _httpClientFactory = Substitute.For<IHttpClientFactory>();

    private readonly GeminiSettings _settings = new GeminiSettings
    {
        BaseUrl = "https://powelqapfpublicapi.azure-api.net/public",
        MunicipalityNo = "stavangerkundetest",
        SubscriptionKey = "3d8d028ee9be4cc9a9e4ac0a92068966"
    };

    public GeminiSyncManualTest()
    {
        _httpClientFactory
            .CreateClient(Arg.Any<string>())
            .Returns(_ => new HttpClient());
    }

    [Fact(Skip = "Manual test only")]
    public async Task CleanGarbageBinsTest()
    {
        //Arrange
        const string filePath = "E:\\Temp\\Ymir\\agreement_lines_270_20260511.json";
        var noEndDate = new DateTime(1900, 1, 1);
        var testGeminiClient = new GeminiClient(_settings, _httpClientFactory);
        var collectionLine = await FileUtils.ReadGarbageBinListAsync(filePath);

        //Act
        var groupedByPlaces = collectionLine
            .GroupBy(x => x.PlaceNr)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach(var group in groupedByPlaces)
        {
            if(group.Key != 0)
            {
                try
                {
                    var result = await testGeminiClient.DeleteGarbageBinCollection((int)group.Key);
                    if(!result)
                    {
                        Console.WriteLine("Something went wrong");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Oooops, something went wrong: {ex.Message}");
                }
            }
        }

        //Assert
        Assert.Fail("Manual test only");
    }

    [Fact]
    public async Task SyncGarbageBinGroups()
    {
        //Arrange
        const string filePath = "E:\\Temp\\Ymir\\20260622\\garbage_binsbuildinttype.json";
        var noEndDate = new DateTime(1900, 1, 1);
        var testGeminiClient = new GeminiClient(_settings, _httpClientFactory);
        var collectionLines = await FileUtils.ReadGarbageBinListAsync(filePath);
        var errorCollection = new List<(int, string)>();
        var garbageBinCollectionBuilder = new GarbageBinCollectionBuilder();

        //Act
        var filteredLines = collectionLines
            .Where(l => l.ShortName < 1000 && l.PlaceNr != null)
            .ToList();

        //&& l.BuildingType != null && l.BuildingType.StartsWith("16")
        var placesToUpdate = collectionLines
            .Where(l => l.ShortName < 1000 && l.PlaceNr != null && l.BuildingType != null && l.BuildingType.StartsWith("16"))
            .Select(l => l.PlaceNr)
            .ToList();

        var groupedByPlaces = filteredLines
            .GroupBy(x => (int)x.PlaceNr)
            .ToDictionary(g => g.Key, g => g.ToList());

        var orderedGroups = groupedByPlaces
            .OrderBy(g => g.Key)
            .ToList();

        int updatedCount = 0;

        var groupsToRun = orderedGroups
            .Skip(19999)
            .ToList();

        foreach (var place in groupsToRun)
        {
            var states = garbageBinCollectionBuilder.BuildStateInTimeCollections(place.Value);
            var stateInTime = new GarbageBinsStateInTimeDto();

            foreach (var state in states)
            {
                var collectionDto = new GarbageBinsCollectionDto
                {
                    GarbageBinCollectionId = place.Key,
                    NumberOfConnectedUtilityUnit = 1,
                    UtilityUnitType = GarbageBinUtilityUnitType.Housing,
                    GarbageBins = state.Lines.Select(l => new GarbageBinDto
                    {
                        GarbageBinId = (int)l.AgreementLineId,
                        GarbageBinCategory = GeminiUtils.ToGarbageBinCategory(l.FractionName),
                        BinSize = l.ShortName ?? 0,
                        FrequencyToBeInvoiced = GarbageBinsFrequencyToBeInvoiced.BiWeekly,
                        IsLockable = l.HasLock,
                        IsCompactor = false
                    }).ToList(),
                    InEffectFrom = state.StartDate,
                    InEffectTo = state.EndDate,
                };

                stateInTime.StateInTime.Add(collectionDto);
            }

            if (stateInTime.StateInTime.Count > 0)
            {
                var isSuccessful = await testGeminiClient.UpdateGarbageBinCollection(stateInTime);

                if (!isSuccessful)
                {
                    Console.WriteLine("Ooops, something went wrong");
                    errorCollection.Add((stateInTime.StateInTime.First().GarbageBinCollectionId, "Update failed"));
                }
                else
                {
                    updatedCount++;
                }
            }
        }

        File.WriteAllText("error_report_20260624.json", JsonSerializer.Serialize(errorCollection));
        Console.WriteLine("Ooops, something went wrong");

        //Assert
        Assert.Fail("Manual test only");
    }
}
