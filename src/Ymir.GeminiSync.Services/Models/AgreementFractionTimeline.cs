namespace Ymir.GeminiSync.Services.Models
{
    public class AgreementFractionTimeline
    {
        public int AgreementId { get; set; }

        public List<FractionTimeEntry> FractionsInTime { get; set; } = new();
    }

    public class FractionTimeEntry
    {
        public DateTimeOffset DateFrom { get; set; }

        public DateTimeOffset? DateTo { get; set; }

        public int FractionNumerator { get; set; }

        public int FractionDenominator { get; set; }
    }
}
