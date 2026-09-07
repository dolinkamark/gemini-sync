using System.Text.Json;
using Ymir.GeminiSync.Domain;
using Ymir.GeminiSync.Domain.Repositories;
using Ymir.GeminiSync.Services.Abstract;

namespace Ymir.GeminiSync.Services;

public class GarbageBinSyncService(
    IGarbageBinCollectionRepository garbageBinRepository,
    IGarbageBinService garbageBinService,
    ISyncReportRepository reportRepository,
    IGeminiClient geminiClient) : IGarbageBinSyncService
{
    public async Task<SyncReport> SyncGarbageBinCollections(
        int customerId,
        string placeTypeDescription,
        bool checkDifference = false,
        List<GarbageBinCollectionLine> previousCollection = null)
    {
        var syncReport = new SyncReport();

        //Step 1) Get things to sync
        var garbageBinCollections = await garbageBinRepository.GetGarbageBinCollections(customerId, placeTypeDescription);

        //Step 2) Build the dto list to send
        var garbageBinStateInTimeList = garbageBinService.CreateGarbageBinsStateInTimeList(garbageBinCollections, placeTypeDescription);
        var totalCount = garbageBinStateInTimeList.Count;

        if (previousCollection != null)
        {
            var previousStateInTimeList = garbageBinService
                .CreateGarbageBinsStateInTimeList(previousCollection, placeTypeDescription);

            var previousByCollectionId = previousStateInTimeList
                .Where(s => s.StateInTime.Count > 0)
                .ToDictionary(s => s.StateInTime[0].GarbageBinCollectionId, s => s.StateInTime);

            garbageBinStateInTimeList = garbageBinStateInTimeList
                .Where(stateInTime =>
                {
                    var collectionId = stateInTime.StateInTime.FirstOrDefault()?.GarbageBinCollectionId ?? 0;
                    return !previousByCollectionId.TryGetValue(collectionId, out var previousState)
                        || !garbageBinService.AreGarbageBinStateInTimesEqual(previousState, stateInTime.StateInTime);
                })
                .ToList();
        }

        //TODO: log stateInTime.StateInTime == 0 as errors

        //Step 3) Sync changed parts
        int updatedCount = 0;
        int checkedCount = 0;

        var toUpdateJson = JsonSerializer.Serialize(garbageBinStateInTimeList);

        foreach (var stateInTime in garbageBinStateInTimeList)
        {
            var garbageBinId = stateInTime.StateInTime.FirstOrDefault()?.GarbageBinCollectionId ?? 0;

            try
            {
                bool shouldUpdate = true;
                if (checkDifference)
                {
                    var currentStateInTime = await geminiClient.GetGarbageBinCollection(garbageBinId);
                    if (garbageBinService.AreGarbageBinStateInTimesEqual(currentStateInTime, stateInTime.StateInTime))
                    {
                        shouldUpdate = false;
                    }
                }

                if(shouldUpdate)
                {
                    var isSuccessful = await geminiClient.UpdateGarbageBinCollection(stateInTime);
                    if (!isSuccessful)
                    {
                        syncReport.Errors.Add(new SyncError
                        {
                            AgreementId = garbageBinId,
                            Description = placeTypeDescription,
                        });
                    }
                    else
                    {
                        updatedCount++;
                    }
                }
            }
            catch(Exception ex)
            {
                syncReport.Errors.Add(new SyncError
                {
                    AgreementId = garbageBinId,
                    Description = ex.ToString(),
                });
            }
            finally
            {
                checkedCount++;
            }
        }

        syncReport.TotalCount = totalCount;
        syncReport.UpdatedCount = updatedCount;

        //Step 4) Save report
        await reportRepository.SaveReport(syncReport);

        return syncReport;
    }
}
