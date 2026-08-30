using Microsoft.EntityFrameworkCore;
using Ymir.GeminiSync.Domain;
using Ymir.GeminiSync.Domain.Repositories;

namespace Ymir.GeminiSync.EntityFramework.Repositories;

public class LoglineRepository(WasteManagementContext dbContext) : ILoglineRepository
{
    public Task<List<LoglineLine>> GetLoglineLines(int customerId, string placeTypeDescription)
    {
        return dbContext.LoglineLines
            .FromSqlInterpolated(
                $"EXEC dbo.GetLoglinesByPlaceType @CustomerId={customerId}, @PlaceTypeDescription={placeTypeDescription}")
            .AsNoTracking()
            .ToListAsync();
    }
}
