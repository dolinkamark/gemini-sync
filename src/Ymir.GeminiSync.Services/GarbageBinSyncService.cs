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
    public async Task<SyncReport> SyncGarbageBinCollections(int customerId, string placeTypeDescription)
    {
        var syncReport = new SyncReport();

        //Step 1) Get things to sync
        var garbageBinCollections = await garbageBinRepository.GetGarbageBinCollections(customerId, placeTypeDescription);

        //garbageBinCollections = garbageBinCollections.Where(c => c.ExternalAgreementId == "18443").ToList();

        //Step 2) Build the dto list to send
        var garbageBinStateInTimeList = garbageBinService.CreateGarbageBinsStateInTimeList(garbageBinCollections, placeTypeDescription);

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

        syncReport.UpdatedCount = updatedCount;

        //Step 4) Save report
        await reportRepository.SaveReport(syncReport);

        return syncReport;
    }
}
