namespace Ymir.GeminiSync.Services.Models.Containers;

public class PrivateContainerGroupFractionInTime
{
    public DateTimeOffset DateFrom { get; set; }

    public DateTimeOffset? DateTo { get; set; }

    public int FractionNumerator { get; set; }

    public int FractionDenominator { get; set; }
}
