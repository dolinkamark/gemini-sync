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
    ISyncReportRepository reportRepository,
    IGeminiClient geminiClient) : IUtilityConnectionsSyncService
{
    public async Task<SyncReport> SyncUtilityUnitConnections(int customerId, bool checkDifference = false)
    {
        var syncReport = new SyncReport();

        //Step 1) Get things to sync
        var connectionsLines = await agreementPlacesRepository.GetAllUtilityUnitConnections(customerId);
        var exemptions = await agreementExcemptionRepository.GetAllAgreementExcemptions(customerId);

        //Step 2) Build the dto list to send
        var connectionTimelines = utilityConnectionService.CreateUtilityUnitTimelines(connectionsLines, exemptions);

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
                if (checkDifference)
                {
                    var utilityUnitTimeline = await geminiClient.GetUtilityConnectionTimeline(timeline.agreementId);
                    if(!utilityConnectionService.AreTimelinesEqual(utilityUnitTimeline, timeline.updateDto.ConnectionsInTime))
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

        syncReport.TotalCount = connectionTimelines.Count;
        syncReport.UpdatedCount = updateCount;

        //Step 4) Save report
        await reportRepository.SaveReport(syncReport);

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
