using System.Text.Json;
using Microsoft.Extensions.Options;
using Ymir.GeminiSync.Domain;
using Ymir.GeminiSync.Domain.Repositories;
using Ymir.GeminiSync.Importer.Models;
using Ymir.GeminiSync.Services.Abstract;
using Ymir.GeminiSync.Services.Settings;

namespace Ymir.GeminiSync.Importer;

public class SyncWorker(
    ILogger<SyncWorker> logger,
    IOptions<SyncOptions> syncOptions,
    IAgreementPlacesRepository agreementPlacesRepository,
    IAgreementExcemptionRepository agreementExcemptionRepository,
    IGarbageBinCollectionRepository garbageBinRepository,
    IGarbageBinService collectionService,
    ILoglineRepository loglineRepository,
    IIntegrationRepository integrationRepository,
    GeminiSettings geminiSettings,
    IHostApplicationLifetime applicationLifetime) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var options = syncOptions.Value;
            var customerId = options.CustomerId;
            var placeTypes = options.PlaceTypes;
            var disableSync = options.DisableCache;
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
                        logger.LogInformation("Saving garbage bins to cache");
                        File.WriteAllText(
                            $"Cache/garbage_bins_{placeType.Replace(" ", "_")}_{DateTime.Now.ToString("yyyyMMdd")}.json",
                            JsonSerializer.Serialize(garbageBins)
                        );
                    }

                    logger.LogInformation("Total bins returned for type {PlaceType}: {Count}", placeType, garbageBins.Count);

                    var groupedBins = collectionService.CreateStateInTimeCollections(garbageBins);

                    logger.LogInformation("Grouped bin count for type {PlaceType} (state of time): {Count}", placeType, garbageBins.Count);
                }
            }

            if (options.Entities.Contains(EntityTypes.Fractions))
            {
                var placeTypeList = placeTypes.Split(",");

                foreach (var placeType in placeTypeList)
                {
                    var agreementPlaces = await agreementPlacesRepository.GetFractionsHistory(customerId, placeType);

                    if (options.UseFileCache)
                    {
                        logger.LogInformation("Saving agreement history lines to cache");
                        File.WriteAllText(
                            $"Cache/agreement_place_history_lines_{placeType.Replace(" ", "_")}_{DateTime.Now.ToString("yyyyMMdd")}.json",
                            JsonSerializer.Serialize(agreementPlaces)
                        );
                    }

                    logger.LogInformation("Total agreement history lines returned for place type {PlaceType}: {Count}", placeType, agreementPlaces.Count);
                }
            }

            if (options.Entities.Contains(EntityTypes.UtilityConnections))
            {
                //Step 1.b) Verify if the utility connections are correct
                var agreementPlaces = await agreementPlacesRepository.GetAllUtilityUnitConnections(customerId);
                if (options.UseFileCache)
                {
                    logger.LogInformation("Saving agreement places");
                    File.WriteAllText($"Cache/agreement_places_{DateTime.Now.ToString("yyyyMMdd")}.json", JsonSerializer.Serialize(agreementPlaces));
                }

                //Verify download exemptions
                var exemptions = await agreementExcemptionRepository.GetAllAgreementExcemptions(customerId);
                if (options.UseFileCache)
                {
                    logger.LogInformation("Saving agreement exemptions");
                    File.WriteAllText($"Cache/agreement_exemptions_{DateTime.Now.ToString("yyyyMMdd")}.json", JsonSerializer.Serialize(exemptions));
                }
            }

            if (options.Entities.Contains(EntityTypes.GarbageBinPickups))
            {
                var placeTypeList = placeTypes.Split(",");

                foreach (var placeType in placeTypeList)
                {
                    var agreementPlaces = await loglineRepository.GetLoglineLines(customerId, placeType);

                    if (options.UseFileCache)
                    {
                        logger.LogInformation("Saving logline lines to cache");
                        File.WriteAllText(
                            $"Cache/logline_lines_{placeType.Replace(" ", "_")}_{DateTime.Now.ToString("yyyyMMdd")}.json",
                            JsonSerializer.Serialize(agreementPlaces)
                        );
                    }

                    logger.LogInformation("Total logline lines returned for place type {PlaceType}: {Count}", placeType, agreementPlaces.Count);
                }
            }

            var geminiIntegrationId = geminiSettings.GeminiIntegrationId;
            if (geminiIntegrationId is null
                || geminiIntegrationId.CustomerId == 0
                || string.IsNullOrWhiteSpace(geminiIntegrationId.Name)
                || string.IsNullOrWhiteSpace(geminiIntegrationId.IntegrationType))
            {
                logger.LogWarning("GeminiIntegrationId is not configured; skipping Integration.UpdatedAt update.");
            }
            else
            {
                var updatedCount = await integrationRepository.UpdateUpdatedAtAsync(
                    geminiIntegrationId.CustomerId,
                    geminiIntegrationId.Name,
                    geminiIntegrationId.IntegrationType);

                if (updatedCount == 0)
                {
                    logger.LogWarning(
                        "No Integration row found for CustomerId {CustomerId}, Name {Name}, IntegrationType {IntegrationType}.",
                        geminiIntegrationId.CustomerId,
                        geminiIntegrationId.Name,
                        geminiIntegrationId.IntegrationType);
                }
                else
                {
                    logger.LogInformation(
                        "Updated Integration.UpdatedAt for CustomerId {CustomerId}, Name {Name}, IntegrationType {IntegrationType}.",
                        geminiIntegrationId.CustomerId,
                        geminiIntegrationId.Name,
                        geminiIntegrationId.IntegrationType);
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
