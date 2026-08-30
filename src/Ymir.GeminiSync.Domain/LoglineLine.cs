namespace Ymir.GeminiSync.Domain;

public class LoglineLine
{
    public int CustomerId { get; set; }

    public long LogLineId { get; set; }

    public DateTime? Time { get; set; }

    public long AgreementLineId { get; set; }

    public long AgreementId { get; set; }

    public string ExternalAgreementId { get; set; }

    public int PlaceNr { get; set; }

    public string Bid { get; set; }

    public string BuildingType { get; set; }

    public string UnitId { get; set; }

    public string FractionName { get; set; }

    public string Name { get; set; }

    public int? ShortName { get; set; }
}
