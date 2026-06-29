namespace Ymir.GeminiSync.Domain;

public class AgreementPlaceHistoryLine
{
    public int CustomerId { get; set; }

    public int PlaceNr { get; set; }

    public long AgreementId { get; set; }

    public string ExternalAgreementId { get; set; }

    public int? NrOfOccupancyUnits { get; set; }

    public DateTime FromDate { get; set; }

    public DateTime? ToDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
