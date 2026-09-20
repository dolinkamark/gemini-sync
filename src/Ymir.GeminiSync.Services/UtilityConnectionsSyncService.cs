using System.Text.Json;
using Ymir.GeminiSync.Domain;
using Ymir.GeminiSync.Domain.Repositories;
using Ymir.GeminiSync.Services.Abstract;
using Ymir.GeminiSync.Services.Models;

namespace Ymir.GeminiSync.Services;

public class UtilityConnectionsSyncService(
    IAgreementPlacesRepository agreementPlacesRepository,
    IAgreementExcemptionRepository agreementExcemptionRepository,
    IUtilityConnectionsService utilityConnectionService,
    IHistoryRepository historyRepository,
    ISyncReportRepository reportRepository,
    IGeminiClient geminiClient) : IUtilityConnectionsSyncService
{
    public async Task<SyncReport> SyncUtilityUnitConnections(
        int customerId,
        bool checkDifference = false)
    {
        var syncReport = new SyncReport();

        //Step 1) Get things to sync
        var connectionsLines = await agreementPlacesRepository.GetAllUtilityUnitConnections(customerId);
        var exemptions = await agreementExcemptionRepository.GetAllAgreementExcemptions(customerId);

        var previousConnections = await historyRepository.GetPreviousAllUtilityUnitConnections(customerId);
        var previousExemptions = await historyRepository.GetPreviousAllAgreementExcemptions(customerId);

        //Step 2) Build the dto list to send
        var connectionTimelines = utilityConnectionService.CreateUtilityUnitTimelines(connectionsLines, exemptions);
        var totalCount = connectionTimelines.Count;

        //Without a previous snapshot every timeline is treated as changed
        if (previousConnections.Count > 0)
        {
            var previousTimelines = utilityConnectionService.CreateUtilityUnitTimelines(
                previousConnections,
                previousExemptions);

            var previousByAgreementId = previousTimelines
                .Where(t => t.updateDto.ConnectionsInTime.Count > 0)
                .ToDictionary(t => t.agreementId, t => t.updateDto.ConnectionsInTime);

            connectionTimelines = connectionTimelines
                .Where(timeline =>
                    !previousByAgreementId.TryGetValue(timeline.agreementId, out var previous)
                    || !utilityConnectionService.AreTimelinesEqual(
                        previous, timeline.updateDto.ConnectionsInTime))
                .ToList();
        }

        //Step 3) Sync changed parts
        var updateCount = 0;
        var checkedCount = 0;

        foreach (var timeline in connectionTimelines)
        {
            if (timeline.updateDto.ConnectionsInTime.Count == 0)
            {
                syncReport.Errors.Add(new SyncError
                {
                    AgreementId = 0,
                    Description = "Invalid timeline: ConnectionsInTime doesn't contain any items"
                });

                continue;
            }

            try
            {
                bool shouldUpdate = true;
                if (checkDifference)
                {
                    var utilityUnitTimeline = await geminiClient.GetUtilityConnectionTimeline(timeline.agreementId);
                    if (utilityConnectionService.AreTimelinesEqual(utilityUnitTimeline, timeline.updateDto.ConnectionsInTime))
                    {
                        shouldUpdate = false;
                    }
                }

                if(shouldUpdate)
                {
                    var isSuccessful = await geminiClient.UpdateUtilityConnectionTimeline(timeline.agreementId, timeline.updateDto);

                    if (isSuccessful)
                    {
                        updateCount++;
                    }
                    else
                    {
                        syncReport.Errors.Add(new SyncError
                        {
                            AgreementId = timeline.agreementId,
                            Description = $"Update failed for dto: {JsonSerializer.Serialize(timeline)}"
                        });
                    }
                }

                checkedCount++;
            }
            catch (Exception ex)
            {
                syncReport.Errors.Add(new SyncError
                {
                    AgreementId = timeline.agreementId,
                    Description = ex.ToString()
                });

                checkedCount++;
            }
        }

        syncReport.TotalCount = totalCount;
        syncReport.UpdatedCount = updateCount;

        //Step 4) Save report and the snapshots the next run compares against
        await reportRepository.SaveReport(syncReport);
        await historyRepository.SaveHistoricalData(customerId, connectionsLines);
        await historyRepository.SaveHistoricalData(customerId, exemptions);

        return syncReport;
    }

    public async Task<SyncReport> SyncUtilityUnitConnectionsByPlace(int customerId, string placeTypeDescription, bool checkDifference = false)
    {
        var syncReport = new SyncReport();

        //Step 1) Get things to sync
        var connectionsLines = await agreementPlacesRepository.GetUtilityUnitConnections(customerId, placeTypeDescription);
        var exemptions = await agreementExcemptionRepository.GetAllAgreementExcemptions(customerId);

        //Step 2) Build the dto list to send
        var connectionTimelines = utilityConnectionService.CreateUtilityUnitTimelines(connectionsLines, exemptions);

        //Step 3) Sync changed parts
        var updateCount = 0;

        foreach (var timeline in connectionTimelines)
        {
            if (timeline.updateDto.ConnectionsInTime.Count == 0)
            {
                syncReport.Errors.Add(new SyncError
                {
                    AgreementId = 0,
                    Description = "Invalid timeline: ConnectionsInTime doesn't contain any items"
                });

                continue;
            }

            try
            {
                if(checkDifference)
                {
                    var utilityUnitTimeline = await geminiClient.GetUtilityConnectionTimeline(timeline.agreementId);
                    if(utilityUnitTimeline.Count == 0)
                    {
                        var isSuccessful = await geminiClient.UpdateUtilityConnectionTimeline(timeline.agreementId, timeline.updateDto);

                        if (isSuccessful)
                        {
                            updateCount++;
                        }
                        else
                        {
                            syncReport.Errors.Add(new SyncError
                            {
                                AgreementId = timeline.agreementId,
                                Description = $"Update failed for dto: {JsonSerializer.Serialize(timeline)}"
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                syncReport.Errors.Add(new SyncError
                {
                    AgreementId = timeline.agreementId,
                    Description = ex.ToString()
                });
            }
        }

        syncReport.TotalCount = connectionTimelines.Count;
        syncReport.UpdatedCount = updateCount;

        //Step 4) Save report
        await reportRepository.SaveReport(syncReport);

        return syncReport;
    }
}
