namespace Ymir.GeminiSync.Services.Abstract;

public interface IGeminiSyncService
{
    Task SyncGarbageBinCollections(int customerId);

    Task SyncUtilityUnitConnections(int customerId, List<string> placeTypes);
}
