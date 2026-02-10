namespace Ymir.GeminiSync.Domain;

public class GarbageBinCollectionLine
{
    public int CustomerId { get; set; }
    public int AgreementLineId { get; set; }
    public int? AgreementId { get; set; }
    public int ExternalAgreementId { get; set; }
    public int? Termin { get; set; }
    public int UnitId { get; set; }
    public int PlaceNr { get; set; }
    public string FractionName { get; set; }
    public string ShortName { get; set; }
    public bool HasLock { get; set; }

    public DateTime RegDate { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public DateTime LastChanged { get; set; }
}
