using Microsoft.EntityFrameworkCore;
using Ymir.GeminiSync.Domain;
using Ymir.GeminiSync.Domain.Repositories;

namespace Ymir.GeminiSync.EntityFramework.Repositories;

public class AgreementExcemptionRepository(WasteManagementContext dbContext) : IAgreementExcemptionRepository
{
    public async Task<List<AgreementExcemption>> GetAllAgreementExcemptions(int customerId)
    {
        return await dbContext.AgreementExcemptions.ToListAsync();
    }
}
