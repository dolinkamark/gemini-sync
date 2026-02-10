namespace Ymir.GeminiSync.Domain;

public class AgreementLine
{
    public int CustomerId { get; set; }
    public string PASystem { get; set; }
    public long AgreementLineId { get; set; }
    public long? AgreementId { get; set; }
    public DateTime? RegDate { get; set; }
    public int? Commune { get; set; }
    public string UnitId { get; set; }
    public double? Amount { get; set; }
    public double? PhysicalAmount { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public DateTime? ExceptFrom { get; set; }
    public DateTime? ExceptTo { get; set; }
    public int? PlaceNr { get; set; }
    public DateTime? LastChanged { get; set; }
    public string ContainerId { get; set; }
    public string Fraction { get; set; }
}
