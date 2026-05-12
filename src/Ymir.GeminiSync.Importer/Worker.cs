using Ymir.GeminiSync.Domain.Repositories;
using Ymir.GeminiSync.Services.Abstract;

namespace Ymir.GeminiSync.Importer;

public class Worker(
    ILogger<Worker> logger,
    IGarbageBinCollectionRepository garbageBinRepository,
    IGarbageBinCollectionService collectionService,
    IHostApplicationLifetime applicationLifetime) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            const int customerId = 270;
            const string placeTypeDescription = "Spann";
            var garbageBins = await garbageBinRepository.GetGarbageBinCollections(customerId, placeTypeDescription);

            Console.WriteLine($"Total bins returned: {garbageBins.Count}");

            var otherWasteBins = garbageBins
                .Where(b => b.FractionName.ToLower() == "restavfall")
                .ToList();

            var groupedBins = collectionService.BuildStateInTimeCollections(otherWasteBins);

            Console.WriteLine($"Grouped bin count (state of time): {otherWasteBins.Count}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Worker failed.");
            Environment.ExitCode = 1;
        }
        finally
        {
            applicationLifetime.StopApplication();
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
        }
    }
}
