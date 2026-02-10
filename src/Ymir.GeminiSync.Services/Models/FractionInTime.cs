namespace Ymir.GeminiSync.Services.Models;

public class FractionInTime
{
    public DateTimeOffset DateFrom { get; set; }

    public DateTimeOffset? DateTo { get; set; }

    public DateTimeOffset ModifiedAt { get; set; }

    public List<FractionAgreement> Agreements { get; set; } = new List<FractionAgreement>();
}
