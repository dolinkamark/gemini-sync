using Ymir.GeminiSync.Domain;
using Ymir.GeminiSync.Domain.Repositories;

namespace Ymir.GeminiSync.EntityFramework.Repositories;

public class GarbageBinCollectionRepository : IGarbageBinCollectionRepository
{
    public Task<List<GarbageBinCollectionLine>> GetGarbageBinCollections(int customerId)
    {
        throw new NotImplementedException();
    }
}
