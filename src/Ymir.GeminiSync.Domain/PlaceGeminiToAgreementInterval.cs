namespace Ymir.GeminiSync.Domain;

public class PlaceGeminiToAgreementInterval
{
    public int PlaceNr { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public DateTime UpdatedAt { get; set; }

    // One GeminiAgreementId (ExternalAgreementId) can map to multiple AgreementIds in the same interval
    public Dictionary<int, List<long>> GeminiToAgreementIds { get; set; } = new();
}
