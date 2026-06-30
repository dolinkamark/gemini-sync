using NSubstitute;
using System.Text.Json;
using Ymir.GeminiSync.Services.Models;
using Ymir.GeminiSync.Services.Settings;

namespace Ymir.GeminiSync.Services.ManualTests;

public class GarbageBinSyncManualTest
{
    private readonly IHttpClientFactory _httpClientFactory = Substitute.For<IHttpClientFactory>();

    private readonly GeminiSettings _settings = new GeminiSettings
    {
        BaseUrl = "https://powelqapfpublicapi.azure-api.net/public",
        MunicipalityNo = "stavangerkundetest",
        SubscriptionKey = "3d8d028ee9be4cc9a9e4ac0a92068966"
    };

    public GarbageBinSyncManualTest()
    {
        _httpClientFactory
            .CreateClient(Arg.Any<string>())
            .Returns(_ => new HttpClient());
    }

    [Fact(Skip = "Manual test only")]
    public async Task SyncGarbageBinGroups()
    {
        //Arrange
        const string filePath = "E:\\Temp\\Ymir\\20260629\\garbage_bins_Spann_20260629.json";
        var noEndDate = new DateTime(1900, 1, 1);
        var testGeminiClient = new GeminiClient(_settings, _httpClientFactory);
        var collectionLines = await FileUtils.ReadGarbageBinListAsync(filePath);
        var errorCollection = new List<(int, string)>();
        var garbageBinCollectionBuilder = new GarbageBinCollectionBuilder();

        //Act

        //Filter out invalid containers by size
        var filteredLines = collectionLines
            .Where(l => l.ShortName < 1000 && l.PlaceNr != null)
            .ToList();

        //Adjust agreements based on building id


        var groupedByPlaces = filteredLines
            .GroupBy(x => (int)x.PlaceNr)
            .ToDictionary(g => g.Key, g => g.ToList());

        var orderedGroups = groupedByPlaces
            .OrderBy(g => g.Key)
            .ToList();

        int updatedCount = 0;

        var groupsToRun = orderedGroups
            .ToList();

        foreach (var place in groupsToRun)
        {
            //if (place.Key != 54302) continue;

            var states = garbageBinCollectionBuilder.BuildStateInTimeCollections(place.Value);
            var stateInTime = new GarbageBinsStateInTimeDto();

            foreach (var state in states)
            {
                var isCabin = state.Lines.All(s => !string.IsNullOrWhiteSpace(s.BuildingType) && s.BuildingType.StartsWith("16"));

                var collectionDto = new GarbageBinsCollectionDto
                {
                    GarbageBinCollectionId = place.Key,
                    NumberOfConnectedUtilityUnit = 1,
                    UtilityUnitType = isCabin ? GarbageBinUtilityUnitType.Cabin : GarbageBinUtilityUnitType.Housing,
                    GarbageBins = state.Lines.Select(l => new GarbageBinDto
                    {
                        GarbageBinId = (int)l.AgreementLineId,
                        GarbageBinCategory = GeminiUtils.ToGarbageBinCategory(l.FractionName),
                        BinSize = l.ShortName ?? 0,
                        FrequencyToBeInvoiced = GeminiUtils.MapGarbageBinFrequency(l.Frequence),
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

    [Fact(Skip = "Manual test only")]
    public async Task SyncPlasticGarbageBinGroups()
    {
        //Arrange
        const string filePath = "E:\\Temp\\Ymir\\20260629\\garbage_bins_Sekk i spann_20260630.json";
        var noEndDate = new DateTime(1900, 1, 1);
        var testGeminiClient = new GeminiClient(_settings, _httpClientFactory);
        var collectionLines = await FileUtils.ReadGarbageBinListAsync(filePath);
        var errorCollection = new List<(int, string)>();
        var garbageBinCollectionBuilder = new GarbageBinCollectionBuilder();

        //Act

        //Filter out invalid containers by size
        var filteredLines = collectionLines
            .Where(l => l.ShortName < 1000 && l.PlaceNr != null)
            .ToList();

        //Adjust agreements based on building id


        var groupedByPlaces = filteredLines
            .GroupBy(x => (int)x.PlaceNr)
            .ToDictionary(g => g.Key, g => g.ToList());

        var orderedGroups = groupedByPlaces
            .OrderBy(g => g.Key)
            .ToList();

        int updatedCount = 0;

        var groupsToRun = orderedGroups
            .ToList();

        foreach (var place in groupsToRun)
        {
            //if (place.Key != 54302) continue;

            var states = garbageBinCollectionBuilder.BuildStateInTimeCollections(place.Value);
            var stateInTime = new GarbageBinsStateInTimeDto();

            foreach (var state in states)
            {
                var isCabin = state.Lines.All(s => !string.IsNullOrWhiteSpace(s.BuildingType) && s.BuildingType.StartsWith("16"));

                var collectionDto = new GarbageBinsCollectionDto
                {
                    GarbageBinCollectionId = place.Key,
                    NumberOfConnectedUtilityUnit = 1,
                    UtilityUnitType = isCabin ? GarbageBinUtilityUnitType.Cabin : GarbageBinUtilityUnitType.Housing,
                    GarbageBins = state.Lines.Select(l => new GarbageBinDto
                    {
                        GarbageBinId = (int)l.AgreementLineId,
                        GarbageBinCategory = GeminiUtils.ToGarbageBinCategory(l.FractionName),
                        BinSize = l.ShortName ?? 0,
                        FrequencyToBeInvoiced = GeminiUtils.MapGarbageBinFrequency(l.Frequence),
                        IsLockable = l.HasLock,
                        IsCompactor = false,
                        isPlasticBag = true
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
