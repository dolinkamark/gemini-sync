namespace Ymir.GeminiSync.Domain.Repositories;

public interface IGarbageBinCollectionRepository
{
    Task<List<GarbageBinCollectionLine>> GetGarbageBinCollections(int customerId);
}
