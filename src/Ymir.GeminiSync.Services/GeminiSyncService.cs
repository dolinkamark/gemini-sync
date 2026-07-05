using Ymir.GeminiSync.Domain;
using Ymir.GeminiSync.Domain.Repositories;
using Ymir.GeminiSync.Services.Abstract;

namespace Ymir.GeminiSync.Services;

public class GeminiSyncService(
    IGarbageBinCollectionRepository garbageBinRepository,
    IGarbageBinService garbageBinService,
    ISyncReportRepository reportRepository,
    IGeminiClient geminiClient) : IGeminiSyncService
{
    public async Task<SyncReport> SyncGarbageBinCollections(int customerId, string placeTypeDescription)
    {
        var syncReport = new SyncReport();

        //Step 1) Get things to sync
        var garbageBinCollections = await garbageBinRepository.GetGarbageBinCollections(customerId, placeTypeDescription);

        //Step 2) Build the dto list to send
        var garbageBinStateInTimeList = garbageBinService.CreateGarbageBinsStateInTimeList(garbageBinCollections);

        //TODO: log stateInTime.StateInTime == 0 as errors

        //Step 3) Sync changed parts
        int updatedCount = 0;
        foreach (var stateInTime in garbageBinStateInTimeList)
        {
            var garbageBinId = stateInTime.StateInTime.FirstOrDefault()?.GarbageBinCollectionId ?? 0;

            try
            {
                if (stateInTime.StateInTime.Count > 0)
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
        }

        //Step 4) Save report


        return syncReport;
    }

    public async Task SyncUtilityUnitConnections(int customerId, string placeTypeDescription)
    {
        //Step 1) Get things to sync

        //Step 2) Sync changed parts

        //Step 3) Create and save report
    }
}
