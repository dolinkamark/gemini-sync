namespace Ymir.GeminiSync.Services.Models;

public class AgreementDto
{
    public int AgreementId { get; set; }

    public int FractionNumerator { get; set; }

    public int FractionDenominator { get; set; }

    public string AgreementText { get; set; }

    public int PropertyId { get; set; }
}
