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
            const string filePath = "E:\\Temp\\agreement_lines.json";
            var noEndDate = new DateTime(1900, 1, 1);
            var testGeminiClient = new GeminiClient(_settings, _httpClientFactory);
            var collectionLine = await FileUtils.ReadGarbageBinListAsync(filePath);

            //Act
            var groupedByPlaces = collectionLine
                .GroupBy(x => x.PlaceNr)
                .ToDictionary(g => g.Key, g => g.ToList());

            var multipleAgreements = groupedByPlaces.Where(p => p.Value.GroupBy(v => v.AgreementId).Count() > 1).ToList();

            foreach (var place in groupedByPlaces)
            {
               // if (place.Key != 40004) continue;

                var states = GeminiUtils.BuildAgreementIntervalsByDate(place.Value);
                var stateInTime = new GarbageBinsStateInTimeDto();

                foreach (var state in states)
                {
                    var collectionDto = new GarbageBinsCollectionDto
                    {
                        GarbageBinCollectionId = place.Key,
                        NumberOfConnectedUtilityUnit = state.Lines.Count,
                        UtilityUnitType = GarbageBinUtilityUnitType.Housing,
                        GarbageBins = state.Lines.Select(l => new GarbageBinDto
                        {
                            GarbageBinId = l.AgreementLineId,
                            GarbageBinCategory = GeminiUtils.ToGarbageBinCategory(l.FractionName),
                            BinSize = int.Parse(l.ShortName),
                            FrequencyToBeInvoiced = GarbageBinsFrequencyToBeInvoiced.BiWeekly,
                            IsLockable = l.HasLock,
                            IsCompactor = false
                        }).ToList(),
                        InEffectFrom = state.StartDate,
                        InEffectTo = state.EndDate,
                    };

                    stateInTime.StateInTime.Add(collectionDto);
                }

                if(stateInTime.StateInTime.Count > 0)
                {
                    var isSuccessful = await testGeminiClient.UpdateGarbageBinCollection(stateInTime);
                }
            }

            var endDate = new DateTime(1900, 1, 1);

            //Assert
            Assert.Fail("Manual test only");
        }

        [Fact(Skip = "Manual test only")]
        public async Task UpdateFractionsInTime()
        {
            //Arrange
            const string filePath = "E:\\Temp\\agreements_to_places_with_all.json";

            DateTime minDate = new DateTime(1900, 1, 1);
            var placeLines = await FileUtils.GetAgreementPlaceHistoryLines(filePath);
            var testGeminiClient = new GeminiClient(_settings, _httpClientFactory);

            //Act
            var intervals = GeminiUtils.BuildIntervalsByDate(placeLines);

            var intervalGroups = intervals
                .GroupBy(i => i.PlaceNr);

            foreach(var intervalGroup in intervalGroups)
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
                if(!isSuccessful)
                {
                    Console.WriteLine("Whoops " + intervalGroup.Key);
                }
            }

            //Assert
            Assert.Fail("Manual test only");
        }

        [Fact]
        public async Task UpdatePrivateContainerFractionsInTime()
        {
            //Arrange
            const string filePath = "E:\\Temp\\Ymir\\private_container_agreements_to_places.json";

            DateTime minDate = new DateTime(1900, 1, 1);
            var placeLines = await FileUtils.GetAgreementPlaceHistoryLines(filePath);
            var testGeminiClient = new GeminiClient(_settings, _httpClientFactory);

            //Act
            var groupedLines = placeLines.GroupBy(l => l.PlaceNr);

            foreach(var agreementPlaceLines in groupedLines)
            {
                if (agreementPlaceLines.Key == 1183360) continue;

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
    }
}
