namespace Ymir.GeminiSync.Domain.Repositories;

public interface IHistoryRepository
{
    Task<List<GarbageBinCollectionLine>> GetPreviousGarbageBinCollections(int customerId, string placeTypeDescription);

    Task<List<AgreementPlaceConnectionLine>> GetPreviousAllUtilityUnitConnections(int customerId);

    Task<List<AgreementPlaceConnectionLine>> GetPreviousUtilityUnitConnections(int customerId, string placeTypeDescription);

    Task<List<AgreementPlaceHistoryLine>> GetPreviousFractionsHistory(int customerId, string placeTypeDescription);

    Task<List<LoglineLine>> GetPreviousLoglineLines(int customerId, string placeTypeDescription);

    Task<List<AgreementExcemption>> GetPreviousAllAgreementExcemptions(int customerId);

    Task SaveHistoricalData(int customerId, string placeTypeDescription, List<GarbageBinCollectionLine> garbageBinCollections);

    Task SaveHistoricalData(int customerId, string placeTypeDescription, List<AgreementPlaceHistoryLine> fractionsHistory);

    Task SaveHistoricalData(int customerId, List<AgreementPlaceConnectionLine> utilityUnitConnections);

    Task SaveHistoricalData(int customerId, string placeTypeDescription, List<AgreementPlaceConnectionLine> utilityUnitConnections);

    Task SaveHistoricalData(int customerId, List<AgreementExcemption> agreementExcemptions);

    Task SaveHistoricalData(int customerId, string placeTypeDescription, List<LoglineLine> loglineLines);
}
