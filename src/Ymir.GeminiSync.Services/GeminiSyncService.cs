using Ymir.GeminiSync.Domain.Repositories;
using Ymir.GeminiSync.Services.Abstract;

namespace Ymir.GeminiSync.Services;

public class GeminiSyncService(
    IGarbageBinCollectionRepository garbageBinRepository,
    IGarbageBinCollectionBuilder garbageBinService,
    IGeminiClient geminiClient) : IGeminiSyncService
{
    private const string PlaceTypeDescription = "Spann";

    public async Task SyncGarbageBinCollections(int customerId)
    {
        //Step 1) Get things to sync
        var garbageBinCollections = await garbageBinRepository.GetGarbageBinCollections(customerId, PlaceTypeDescription);

        //Step 2) Check if needs to sync


        //Step 3) Sync changed parts


        //Step 4) Create report

    }
}
