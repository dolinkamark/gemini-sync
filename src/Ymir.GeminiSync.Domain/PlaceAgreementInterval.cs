namespace Ymir.GeminiSync.Domain;

public class PlaceAgreementInterval
{
    public int PlaceNr { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<int> GeminiAgreementIds { get; set; } = new();

    public List<AgreementOccupancy> AgreementOccupancyList { get; set; } = new();
}
