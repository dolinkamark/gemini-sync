namespace Ymir.GeminiSync.Domain;

public class AgreementConnectionLine
{
    public int CustomerId { get; set; }

    public int AgreementId { get; set; }

    public int ExternalAgreementId { get; set; }

    public int PlaceNr { get; set; }

    public int? PlaceTypeId { get; set; }

    public string PlaceType { get; set; }

    public string BuildingType { get; set; }

    public DateTime FromDate { get; set; }

    public DateTime? ToDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
