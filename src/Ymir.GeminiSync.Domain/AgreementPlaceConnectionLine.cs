namespace Ymir.GeminiSync.Domain;

public class AgreementPlaceConnectionLine
{

    public int CustomerId { get; set; }

    public long AgreementId { get; set; }

    public string ExternalAgreementId { get; set; }

    public int PlaceNr { get; set; }

    public string PlaceTypeId { get; set; }

    public string PlaceType { get; set; }

    public string BuildingType { get; set; }

    public DateTime FromDate { get; set; }

    public DateTime? ToDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
