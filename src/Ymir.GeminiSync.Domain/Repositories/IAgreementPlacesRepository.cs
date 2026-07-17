namespace Ymir.GeminiSync.Domain.Repositories;

public interface IAgreementPlacesRepository
{
    Task<List<AgreementPlaceConnectionLine>> GetAllUtilityUnitConnections(int customerId);

    Task<List<AgreementPlaceConnectionLine>> GetUtilityUnitConnections(int customerId, string placeTypeDescription);

    Task<List<AgreementPlaceHistoryLine>> GetFractionsHistory(int customerId, string placeTypeDescription);
}
