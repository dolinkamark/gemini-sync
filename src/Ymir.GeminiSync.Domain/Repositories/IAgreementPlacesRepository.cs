namespace Ymir.GeminiSync.Domain.Repositories;

public interface IAgreementPlacesRepository
{
    Task<List<AgreementPlaceConnectionLine>> GetAgreementPlaceConnections(int customerId, string placeTypeDescription);

    Task<List<AgreementPlaceHistoryLine>> GetAgreementPlaceHistory(int customerId, string placeTypeDescription);
}
