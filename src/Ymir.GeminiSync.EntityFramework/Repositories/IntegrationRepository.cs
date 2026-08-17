using Microsoft.EntityFrameworkCore;
using Ymir.GeminiSync.Domain;
using Ymir.GeminiSync.Domain.Repositories;

namespace Ymir.GeminiSync.EntityFramework.Repositories;

public class IntegrationRepository(WasteManagementContext dbContext) : IIntegrationRepository
{
    public async Task<Integration> GetIntegrationAsync(int customerId, string name, string integrationType)
    {
        return await dbContext.Integrations
            .Where(i => i.CustomerId == customerId && i.Name == name && i.IntegrationType == integrationType)
            .FirstOrDefaultAsync();
    }

    public async Task<int> UpdateUpdatedAtAsync(int customerId, string name, string integrationType)
    {
        var updatedAt = DateTime.Now;

        return await dbContext.Integrations
            .Where(i => i.CustomerId == customerId && i.Name == name && i.IntegrationType == integrationType)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.UpdatedAt, updatedAt));
    }
}
