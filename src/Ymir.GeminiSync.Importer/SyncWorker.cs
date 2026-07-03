using System.Text.Json;
using Microsoft.Extensions.Options;
using Ymir.GeminiSync.Domain;
using Ymir.GeminiSync.Domain.Repositories;
using Ymir.GeminiSync.Importer.Models;
using Ymir.GeminiSync.Services.Abstract;

namespace Ymir.GeminiSync.Importer;

public class SyncWorker(
    ILogger<SyncWorker> logger,
    IOptions<SyncOptions> syncOptions,
    IAgreementPlacesRepository agreementPlacesRepository,
    IAgreementExcemptionRepository agreementExcemptionRepository,
    IGarbageBinCollectionRepository garbageBinRepository,
    IGarbageBinCollectionBuilder collectionService,
    IHostApplicationLifetime applicationLifetime) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var options = syncOptions.Value;
            var customerId = options.CustomerId;
            var placeTypes = options.PlaceTypes;
            var cacheFolder = "Cache";

            //Step 1) Verify if the collections are correct
            if(options.UseFileCache)
            {
                if(!Directory.Exists(cacheFolder))
                {
                    Directory.CreateDirectory(cacheFolder);
                }
            }

            if(options.Entities.Contains(EntityTypes.GarbageBins))
            {
                var placeTypeList = placeTypes.Split(",");

                foreach(var placeType in placeTypeList)
                {
                    var garbageBins = await garbageBinRepository.GetGarbageBinCollections(customerId, placeType);

                    if (options.UseFileCache)
                    {
                        Console.WriteLine("Saving garbage bins to cache");
                        File.WriteAllText(
                            $"Cache/garbage_bins_{placeType}_{DateTime.Now.ToString("yyyyMMdd")}.json",
                            JsonSerializer.Serialize(garbageBins)
                        );
                    }

                    Console.WriteLine($"Total bins returned for type {placeType}: {garbageBins.Count}");

                    var groupedBins = collectionService.BuildStateInTimeCollections(garbageBins);

                    Console.WriteLine($"Grouped bin count for type {placeType} (state of time): {garbageBins.Count}");
                }
            }

            if (options.Entities.Contains(EntityTypes.Fractions))
            {
                var agreementPlaces = await agreementPlacesRepository.GetAgreementPlaceHistory(customerId, placeTypes);

                if (options.UseFileCache)
                {
                    Console.WriteLine("Saving agreement history lines to cache");
                    File.WriteAllText(
                        $"Cache/agreement_place_history_lines_{placeTypes}_{DateTime.Now.ToString("yyyyMMdd")}.json",
                        JsonSerializer.Serialize(agreementPlaces)
                    );
                }

                Console.WriteLine($"Total agreement history lines returned for place type {placeTypes}: {agreementPlaces.Count}");
            }

            if (options.Entities.Contains(EntityTypes.UtilityConnections))
            {
                //Step 1.b) Verify if the utility connections are correct
                var agreementPlaces = await agreementPlacesRepository.GetAgreementPlaceConnections(customerId, placeTypes);
                if (options.UseFileCache)
                {
                    Console.WriteLine("Saving agreement places");
                    File.WriteAllText($"Cache/agreement_places_{placeTypes}_{DateTime.Now.ToString("yyyyMMdd")}.json", JsonSerializer.Serialize(agreementPlaces));
                }

                //Verify download exemptions
                var exemptions = await agreementExcemptionRepository.GetAllAgreementExcemptions(customerId);
                if (options.UseFileCache)
                {
                    Console.WriteLine("Saving agreement exemptions");
                    File.WriteAllText($"Cache/agreement_exemptions_{placeTypes}_{DateTime.Now.ToString("yyyyMMdd")}.json", JsonSerializer.Serialize(exemptions));
                }
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
