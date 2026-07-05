using Ymir.GeminiSync.Domain;

namespace Ymir.GeminiSync.Services.Abstract;

public interface IGarbageBinSyncService
{
    Task<SyncReport> SyncGarbageBinCollections(int customerId, string placeTypeDescription);
}
