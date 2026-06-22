using System.Text.Json;
using Microsoft.Extensions.Options;
using Ymir.GeminiSync.Domain.Repositories;
using Ymir.GeminiSync.Importer.Models;
using Ymir.GeminiSync.Services.Abstract;

namespace Ymir.GeminiSync.Importer;

public class SyncWorker(
    ILogger<SyncWorker> logger,
    IOptions<SyncOptions> syncOptions,
    IGarbageBinCollectionRepository garbageBinRepository,
    IGarbageBinCollectionService collectionService,
    IHostApplicationLifetime applicationLifetime) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var options = syncOptions.Value;
            var customerId = options.CustomerId;
            var placeTypeDescription = options.PlaceTypeDescription;

            //Step 1) Validations
            var garbageBins = await garbageBinRepository.GetGarbageBinCollections(customerId, placeTypeDescription);

            Console.WriteLine($"Total bins returned: {garbageBins.Count}");

            var otherWasteBins = garbageBins
                .Where(b => b.FractionName.ToLower() == "restavfall")
                .ToList();

            var groupedBins = collectionService.BuildStateInTimeCollections(otherWasteBins);

            Console.WriteLine($"Grouped bin count (state of time): {otherWasteBins.Count}");

            Console.WriteLine("State in time example: ");
            Console.WriteLine(JsonSerializer.Serialize(groupedBins.FirstOrDefault()?.ToString()));
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
