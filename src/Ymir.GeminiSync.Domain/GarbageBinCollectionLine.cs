namespace Ymir.GeminiSync.Domain;

public class GarbageBinCollectionLine
{
    public int CustomerId { get; set; }
    public long AgreementLineId { get; set; }
    public long? AgreementId { get; set; }

    public string ExternalAgreementId { get; set; }

    public string BuildingType { get; set; }

    public int? NrOfOccupancyUnits { get; set; }

    public string UnitId { get; set; }

    public int? PlaceNr { get; set; }

    public string Termin { get; set; }

    public int? Frequence { get; set; }

    public string FractionName { get; set; }

    public int? ShortName { get; set; }

    public bool HasLock { get; set; }

    public DateTime RegDate { get; set; }

    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    public DateTime LastChanged { get; set; }
}
