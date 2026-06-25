using Ymir.GeminiSync.Domain;
using Ymir.GeminiSync.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Ymir.GeminiSync.EntityFramework.Repositories;

public class AgreementPlacesRepository(WasteManagementContext dbContext) : IAgreementPlacesRepository
{
  public Task<List<AgreementPlaceConnectionLine>> GetAgreementPlaceConnections(int customerId)
    {
        return dbContext.AgreementPlaceConnections
            .FromSqlInterpolated(
                $"EXEC dbo.GetAgreementPlaceConnections @CustomerId={customerId}")
            .AsNoTracking()
            .ToListAsync();
    }
}
