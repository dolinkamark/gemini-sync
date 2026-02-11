namespace Ymir.GeminiSync.Services.Models.Containers;

public class PrivateContainerAgreementsResponse
{
    public int AgreementId { get; set; }

    public int FractionNumerator { get; set; }

    public int FractionDenominator { get; set; }

    public string AgreementText { get; set; }

    public long? PropertyId { get; set; }
}
