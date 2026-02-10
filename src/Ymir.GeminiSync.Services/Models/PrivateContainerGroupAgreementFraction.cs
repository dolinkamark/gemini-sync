namespace Ymir.GeminiSync.Services.Models;

public class PrivateContainerGroupAgreementFraction
{
    public DateTimeOffset DateFrom { get; set; }

    public DateTimeOffset? DateTo { get; set; }

    public int FractionNumerator { get; set; }

    public int FractionDenominator { get; set; }
}
