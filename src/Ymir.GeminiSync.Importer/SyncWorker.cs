using System.Text.Json;
using Microsoft.Extensions.Options;
using Ymir.GeminiSync.Domain.Repositories;
using Ymir.GeminiSync.Importer.Models;
using Ymir.GeminiSync.Services.Abstract;

namespace Ymir.GeminiSync.Importer;

public class SyncWorker(
    ILogger<SyncWorker> logger,
    IOptions<SyncOptions> syncOptions,
    IAgreementPlacesRepository agreementPlacesRepository,
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

            //Step 1) Verify if the collections are correct
            var garbageBins = await garbageBinRepository.GetGarbageBinCollections(customerId, placeTypeDescription);

            if(options.UseFileCache)
            {
                Console.WriteLine("Saving garbage bins to cache");
                File.WriteAllText("Cache/garbage_bins.json", JsonSerializer.Serialize(garbageBins));
            }

            Console.WriteLine($"Total bins returned: {garbageBins.Count}");

            var groupedBins = collectionService.BuildStateInTimeCollections(garbageBins);

            Console.WriteLine($"Grouped bin count (state of time): {garbageBins.Count}");

            //Step 1.b) Verify if the utility connections are correct
            var agreementPlaces = await agreementPlacesRepository.GetAgreementPlaceConnections(customerId);
            if (options.UseFileCache)
            {
                Console.WriteLine("Saving agreement places");
                File.WriteAllText("Cache/agreement_places.json", JsonSerializer.Serialize(agreementPlaces));
            }
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
