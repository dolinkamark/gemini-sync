using NSubstitute;
using Ymir.GeminiSync.Domain;
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
        public async Task UpdatePrivateContainerFractionsInTime()
        {
            //Arrange
            const string filePath = "E:\\Temp\\Ymir\\20260629\\agreement_place_history_lines_Nedgravd privat_20260630.json";

            var errorCollection = new List<(long, string)>();
            var updateCount = 0;

            DateTime minDate = new DateTime(1900, 1, 1);
            var placeLines = await FileUtils.ReadFileContent<List<AgreementPlaceHistoryLine>>(filePath);
            var testGeminiClient = new GeminiClient(_settings, _httpClientFactory);

            //Act
            var groupedLines = placeLines
                .GroupBy(l => l.PlaceNr)
                .ToList();

            foreach (var agreementPlaceLines in groupedLines)
            {
                try
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
                    if (isSuccessful)
                    {
                        updateCount++;
                    }
                    else
                    {
                        errorCollection.Add((agreementPlaceLines.Key, $"Update failed for place: {agreementPlaceLines.Key}"));
                    }
                }
                catch (Exception ex)
                {
                    errorCollection.Add((agreementPlaceLines.Key, ex.ToString()));
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
            var loglineExportLines = await FileUtils.ReadFileContent<List<LoglineExportLine>>(filePath);

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
    }
}
