using NSubstitute;
using Ymir.GeminiSync.Services.Settings;

namespace Ymir.GeminiSync.Services.ManualTests;

public class FractionsInTimeManualTests
{
    private readonly IHttpClientFactory _httpClientFactory = Substitute.For<IHttpClientFactory>();

    private readonly GeminiSettings _settings = new GeminiSettings
    {
        BaseUrl = "https://powelqapfpublicapi.azure-api.net/public",
        MunicipalityNo = "stavangerkundetest",
        SubscriptionKey = "3d8d028ee9be4cc9a9e4ac0a92068966"
    };

    public FractionsInTimeManualTests()
    {
        _httpClientFactory
            .CreateClient(Arg.Any<string>())
            .Returns(_ => new HttpClient());
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
            catch (Exception ex)
            {
                errorLines.Add((intervalGroup.Key, ex.ToString()));
            }
        }

        //Assert
        Assert.Fail("Manual test only");
    }
}
