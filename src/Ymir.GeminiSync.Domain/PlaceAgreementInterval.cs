namespace Ymir.GeminiSync.Domain;

public class PlaceAgreementInterval
{
    public int PlaceNr { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<int> AgreementIds { get; set; } = new();
}
