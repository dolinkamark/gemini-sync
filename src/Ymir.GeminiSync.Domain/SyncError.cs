namespace Ymir.GeminiSync.Domain;

public class SyncError
{
    public int? PlaceNr { get; set; }

    public int? AgreementId { get; set; }

    public string Description { get; set; }
}
