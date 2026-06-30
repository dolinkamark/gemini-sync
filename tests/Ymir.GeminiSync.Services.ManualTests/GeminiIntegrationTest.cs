using NSubstitute;
using Ymir.GeminiSync.Services.Models;
using Ymir.GeminiSync.Services.Models.Containers;
using Ymir.GeminiSync.Services.Settings;

namespace Ymir.GeminiSync.Services.ManualTests
{
    public class GeminiIntegrationTest
    {
        private readonly IHttpClientFactory _httpClientFactory = Substitute.For<IHttpClientFactory>();

        private readonly GeminiSettings _settings = new GeminiSettings
        {
            BaseUrl = "https://powelqapfpublicapi.azure-api.net/public",
            MunicipalityNo = "stavangerkundetest",
            SubscriptionKey = "3d8d028ee9be4cc9a9e4ac0a92068966"
        };

        public GeminiIntegrationTest()
        {
            _httpClientFactory
                .CreateClient(Arg.Any<string>())
                .Returns(_ => new HttpClient());
        }

        [Fact(Skip = "Manual test only")]
        public async Task UpdateGarbageBins()
        {
            //Arrange
            const string filePath = "E:\\Temp\\Ymir\\agreement_lines_20260312.json";
            var noEndDate = new DateTime(1900, 1, 1);
            var testGeminiClient = new GeminiClient(_settings, _httpClientFactory);
            var collectionLine = await FileUtils.ReadGarbageBinListAsync(filePath);
            var garbageBinCollectionBuilder = new GarbageBinCollectionBuilder();

            //Act
            var groupedByPlaces = collectionLine
                .GroupBy(x => x.PlaceNr)
                .ToDictionary(g => g.Key, g => g.ToList());

            var multipleAgreements = groupedByPlaces.Where(p => p.Value.GroupBy(v => v.AgreementId).Count() > 1).ToList();

            foreach (var place in groupedByPlaces)
            {
                var states = garbageBinCollectionBuilder.BuildStateInTimeCollections(place.Value);
                var stateInTime = new GarbageBinsStateInTimeDto();

                foreach (var state in states)
                {
                    var collectionDto = new GarbageBinsCollectionDto
                    {
                        GarbageBinCollectionId = (int)place.Key,
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
                    }
                }
            }

            var endDate = new DateTime(1900, 1, 1);

            //Assert
            Assert.Fail("Manual test only");
        }

        [Fact]
        public async Task UpdateFractionsInTime()
        {
            //Arrange
            const string filePath = "E:\\Temp\\Ymir\\20260629\\agreement_place_history_lines_Spann_20260629.json";

            DateTime minDate = new DateTime(1900, 1, 1);
            var placeLines = await FileUtils.ReadAgreementPlaceHistoryLines(filePath);
            placeLines = placeLines
                .Where(p => !String.IsNullOrWhiteSpace(p.ExternalAgreementId))
                .ToList();

            var testGeminiClient = new GeminiClient(_settings, _httpClientFactory);

            //Act
            var intervals = GeminiUtils.BuildIntervalsByDate(placeLines);

            var intervalGroups = intervals
                .GroupBy(i => i.PlaceNr)
                .ToList();

            var partialGroups = intervalGroups
                .Skip(1000)
                .Take(5000)
                .ToList();

            var processed = 0;
            var errorLines = new List<(int, string)>();

            foreach (var intervalGroup in partialGroups)
            {
                //if (intervalGroup.Key != 272) continue;

                try
                {
                    var currentIntervals = intervalGroup.ToList();
                    var fractions = GeminiUtils.ToFractionsInTime(currentIntervals);
                    fractions.ForEach(f =>
                    {
                        f.DateFrom = f.DateFrom.AddHours(12);

                        if (f.DateTo != null)
                        {
                            f.DateTo = f.DateTo.Value.AddHours(12);
                        }
                    });

                    var timelines = GeminiUtils.ToFractionTimelines(fractions);

                    var isSuccessful = await testGeminiClient.UpdateFractionsInTime(intervalGroup.Key, timelines);
                    if (!isSuccessful)
                    {
                        Console.WriteLine("Whoops " + intervalGroup.Key);
                        errorLines.Add((intervalGroup.Key, "Gemini client call failed"));
                    }
                    else
                    {
                        processed++;
                    }
                }
                catch(Exception ex)
                {
                    errorLines.Add((intervalGroup.Key, ex.ToString()));
                }
            }

            //Assert
            Assert.Fail("Manual test only");
        }

        [Fact(Skip = "Manual test only")]
        public async Task UpdatePrivateContainerFractionsInTime()
        {
            //Arrange
            const string filePath = "E:\\Temp\\Ymir\\private_container_agreements_to_places.json";

            DateTime minDate = new DateTime(1900, 1, 1);
            var placeLines = await FileUtils.ReadAgreementPlaceHistoryLines(filePath);
            var testGeminiClient = new GeminiClient(_settings, _httpClientFactory);

            //Act
            var groupedLines = placeLines.GroupBy(l => l.PlaceNr);

            foreach (var agreementPlaceLines in groupedLines)
            {
                var geminiIntervals = GeminiUtils.BuildGeminiToAgreementIntervalsByDate(agreementPlaceLines.ToList());
                var fractionsTimeline = PrivateContainerTimelineMapper.ToPrivateContainerFractionTimelinesNew(geminiIntervals);

                //Adjust times
                fractionsTimeline.ForEach(timeline =>
                {
                    timeline.FractionsInTime.ForEach(fraction =>
                    {
                        fraction.DateFrom = fraction.DateFrom.AddHours(12);
                        fraction.DateTo = fraction.DateTo?.AddHours(12);
                    });
                });

                var isSuccessful = await testGeminiClient.UpdatePrivateContainerGroupFractions(agreementPlaceLines.Key, fractionsTimeline);
                if (!isSuccessful)
                {
                    Console.WriteLine("Whoops " + agreementPlaceLines.Key);
                }
            }

            //Assert
            Assert.Fail("Manual test only");
        }

        [Fact]
        public async Task UptadeUtilityConnections()
        {
            //Arrange
            const string filePath = "E:\\Temp\\Ymir\\20260625\\agreement_places20260624.json";
            const string publicContainerName = "Bruksdel nedgravd";

            var errorCollection = new List<(long, string)>();
            var updateCount = 0;

            var testGeminiClient = new GeminiClient(_settings, _httpClientFactory);

            DateTime minDate = new DateTime(1900, 1, 1);
            var connectionLines = await FileUtils.ReadAgreementConnectionLines(filePath);

            //var agreementGroups = connectionLines
            //    .Where(l => !String.IsNullOrWhiteSpace(l.ExternalAgreementId))
            //    .GroupBy(l => l.AgreementId);

            var agreementGroups = connectionLines
               .GroupBy(l => l.AgreementId)
               .ToList();

            foreach (var agreementGroup in agreementGroups)
            {
                var timelines = new List<ConnectionTimelineDto>();
                var currentLines = agreementGroup
                    .OrderBy(l => l.FromDate)
                    .ToList();

                //Closed intervals
                for (int i = 0; i < currentLines.Count - 1; i++)
                {
                    bool isPublicContainer = currentLines[i].PlaceType.ToLower() == publicContainerName.ToLower();
                    bool isCabin = !string.IsNullOrWhiteSpace(currentLines[i].BuildingType) && currentLines[i].BuildingType.StartsWith("16");

                    timelines.Add(new ConnectionTimelineDto
                    {
                        AgreementId = Int32.Parse(currentLines[i].ExternalAgreementId),
                        IsConnectedToGarbagePickupSystem = true,
                        IsConnectedToPublicContainer = isPublicContainer,
                        DateFrom = currentLines[i].FromDate.AddHours(12),
                        DateTo = currentLines[i+1].FromDate.AddHours(-12),
                        UtilityUnitConnectionType = isCabin ? UtilityUnitConnectionType.Cabin : UtilityUnitConnectionType.Housing
                    });
                }

                //Last interval
                var lastInterval = currentLines[^1];
                bool isLastCabin = !string.IsNullOrWhiteSpace(lastInterval.BuildingType) && lastInterval.BuildingType.StartsWith("16");

                timelines.Add(new ConnectionTimelineDto
                {
                    AgreementId = Int32.Parse(lastInterval.ExternalAgreementId),
                    IsConnectedToGarbagePickupSystem = true,
                    IsConnectedToPublicContainer = lastInterval.PlaceType?.ToLower() == publicContainerName.ToLower(),
                    DateFrom = lastInterval.FromDate.AddHours(12),
                    DateTo = lastInterval.ToDate?.AddHours(12),
                    UtilityUnitConnectionType = isLastCabin ? UtilityUnitConnectionType.Cabin : UtilityUnitConnectionType.Housing
                });

                var updateDto = new UtilityUnitConnectionUpdateDto
                {
                    ConnectionsInTime = timelines
                };

                try
                {
                    var isSuccessful = await testGeminiClient.UpdateUtilityConnectionTimeline((int)agreementGroup.Key, updateDto);

                    if (isSuccessful)
                    {
                        updateCount++;
                    }
                    else
                    {
                        errorCollection.Add((agreementGroup.Key, $"Update failed for agreementId: {agreementGroup.Key}"));
                    }
                    
                }
                catch (Exception ex)
                {
                    errorCollection.Add((agreementGroup.Key, ex.ToString()));
                    Console.WriteLine($"Ooops error at {lastInterval.ExternalAgreementId}");
                }
            }

            //Assert
            Assert.Fail("Manual test only");
        }

        [Fact(Skip = "Manual test only")]
        public async Task UpdateLoglines()
        {
            //Arrange
            const string filePath = "E:\\Temp\\Ymir\\logline_export.json";
            var loglineExportLines = await FileUtils.ReadLogLineExportLines(filePath);

            var testGeminiClient = new GeminiClient(_settings, _httpClientFactory);

            //Act
            var loglinesByPlace = loglineExportLines
                .Where(l => l.Description != "NULL")
                .GroupBy(l => l.PlaceNr);

            foreach(var loglineGroup in loglinesByPlace)
            {
                if (loglineGroup.Key != 1174129) continue;

                foreach(var logline in loglineGroup.ToList())
                {
                    if(logline.Message == "Ja")
                    {
                        var containerPickupDto = new ContainerPickupDto
                        {
                            GarbagePrivateContainerPickupId = logline.LogLineId,
                            ExecutedDate = logline.Time,
                            GarbagePrivateContainerGroupId = logline.PlaceNr,
                            WasteType = GeminiUtils.FromLoglineName(logline.Name)
                        };

                        var isSuccessful = await testGeminiClient.AddPrivateContainerPickup(containerPickupDto);
                        if(!isSuccessful)
                        {
                            Console.WriteLine($"Oooops something went wrong with {logline.LogLineId}");
                        }

                        await Task.Delay(50);
                    }
                }
            }

            //Assert
            Assert.Fail("Manual test only");
        }


        public async Task ResetGarbageBinCollections()
        {

        }

    }
}
