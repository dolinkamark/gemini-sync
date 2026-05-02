namespace Ymir.GeminiSync.Services.Abstract;

public interface IGeminiSyncService
{
    Task SyncGarbageBinCollections(int customerId);
}
