namespace Ymir.GeminiSync.Domain;

public class AgreementOccupancy
{
    public long AgreementId { get; set; }

    public int GeminiAgreementId { get; set; }

    public int NrOfOccupancyUnits { get; set; }
}
