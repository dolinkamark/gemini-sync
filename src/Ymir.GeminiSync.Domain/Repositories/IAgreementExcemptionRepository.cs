namespace Ymir.GeminiSync.Domain.Repositories;

public interface IAgreementExcemptionRepository
{
    Task<List<AgreementExcemption>> GetAllAgreementExcemptions(int customerId);
}
