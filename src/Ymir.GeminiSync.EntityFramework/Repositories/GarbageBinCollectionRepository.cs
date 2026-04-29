using Ymir.GeminiSync.Domain;
using Ymir.GeminiSync.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Ymir.GeminiSync.EntityFramework.Repositories;

public class GarbageBinCollectionRepository(WasteManagementContext dbContext) : IGarbageBinCollectionRepository
{
    public Task<List<GarbageBinCollectionLine>> GetGarbageBinCollections(int customerId, string placeTypeDescription)
    {
        return dbContext.GarbageBinCollections
            .FromSqlInterpolated(
                $"EXEC dbo.GetGarbageBinCollectionsByPlaceType @CustomerId={customerId}, @PlaceTypeDescription={placeTypeDescription}")
            .AsNoTracking()
            .ToListAsync();
    }
}
