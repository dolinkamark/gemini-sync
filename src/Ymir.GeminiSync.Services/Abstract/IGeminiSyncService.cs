using Ymir.GeminiSync.Domain;

namespace Ymir.GeminiSync.Services.Abstract;

public interface IGeminiSyncService
{
    Task<SyncReport> SyncGarbageBinCollections(int customerId, string placeTypeDescription);

    Task SyncUtilityUnitConnections(int customerId, string placeTypeDescription);
}
