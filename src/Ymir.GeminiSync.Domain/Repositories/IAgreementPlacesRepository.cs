namespace Ymir.GeminiSync.Domain.Repositories;

public interface IAgreementPlacesRepository
{
    Task<List<AgreementPlaceConnectionLine>> GetAgreementPlaceConnections(int customerId);
}
