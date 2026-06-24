namespace Ymir.GeminiSync.Domain.Repositories;

public interface IAgreementPlacesRepository
{
    Task<List<GarbageBinCollectionLine>> GetAgreementPlaceConnections(int customerId);
}
