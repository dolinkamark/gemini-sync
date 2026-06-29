using Ymir.GeminiSync.Domain;
using Ymir.GeminiSync.Services.Models.Containers;

namespace Ymir.GeminiSync.Services;

public static class PrivateContainerTimelineMapper
{
    public static List<PrivateContainerGroupAgreementFractions> ToPrivateContainerFractionTimelines(
        List<PlaceGeminiToAgreementInterval> intervals)
    {
        if (intervals == null || intervals.Count == 0)
            return new List<PrivateContainerGroupAgreementFractions>();

        // Accumulate per agreement
        var byAgreement = new Dictionary<long, List<PrivateContainerGroupFractionInTime>>();

        foreach (var interval in intervals)
        {
            if (interval?.GeminiToAgreementIds == null || interval.GeminiToAgreementIds.Count == 0)
                continue;

            // Collect all DISTINCT AgreementIds that are active in this interval (across all Gemini ids)
            var activeAgreementIds = interval.GeminiToAgreementIds
                .SelectMany(kv => kv.Value ?? Enumerable.Empty<long>())
                .Distinct()
                .OrderBy(id => id)
                .ToList();

            var denom = activeAgreementIds.Count;
            if (denom == 0)
                continue;

            var from = ToUtcOffset(interval.FromDate);
            var to = interval.ToDate.HasValue ? ToUtcOffset(interval.ToDate.Value) : (DateTimeOffset?)null;

            foreach (var agreementId in activeAgreementIds)
            {
                var entry = new PrivateContainerGroupFractionInTime
                {
                    DateFrom = from,
                    DateTo = to,
                    FractionNumerator = 1,
                    FractionDenominator = denom
                };

                if (!byAgreement.TryGetValue(agreementId, out var list))
                {
                    list = new List<PrivateContainerGroupFractionInTime>();
                    byAgreement[agreementId] = list;
                }

                // Merge with previous if contiguous and same fraction
                if (list.Count > 0)
                {
                    var prev = list[^1];

                    var contiguous =
                        prev.DateTo.HasValue &&
                        prev.DateTo.Value.Date.AddDays(1) == entry.DateFrom.Date;

                    var sameFraction =
                        prev.FractionNumerator == entry.FractionNumerator &&
                        prev.FractionDenominator == entry.FractionDenominator;

                    if (contiguous && sameFraction)
                    {
                        prev.DateTo = entry.DateTo; // extend
                        continue;
                    }
                }

                list.Add(entry);
            }
        }

        return byAgreement
            .OrderBy(k => k.Key)
            .Select(k => new PrivateContainerGroupAgreementFractions
            {
                AgreementId = k.Key,
                FractionsInTime = k.Value.OrderBy(x => x.DateFrom).ToList()
            })
            .ToList();
    }

    public static List<PrivateContainerGroupAgreementFractions> ToPrivateContainerFractionTimelinesNew(
        List<PlaceGeminiToAgreementInterval> intervals)
    {
        if (intervals == null || intervals.Count == 0)
            return new List<PrivateContainerGroupAgreementFractions>();

        var agreementFractions = new List<PrivateContainerGroupAgreementFractions>();
        var geminiAgreementIds = intervals
            .SelectMany(interval => interval.GeminiToAgreementIds.Keys)
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        foreach(var geminiAgreementId in geminiAgreementIds)
        {
            var agreementFraction = new PrivateContainerGroupAgreementFractions
            {
                AgreementId = geminiAgreementId,
            };

            foreach(var interval in intervals)
            {
                var allAgreementCount = interval.GeminiToAgreementIds.Sum(s => s.Value?.Count ?? 0);
                if(interval.GeminiToAgreementIds.Keys.Contains(geminiAgreementId))
                {
                    var relatedAgreements = interval.GeminiToAgreementIds[geminiAgreementId];

                    agreementFraction.FractionsInTime.Add(new PrivateContainerGroupFractionInTime
                    {
                        DateFrom = interval.FromDate,
                        DateTo = interval.ToDate,
                        FractionNumerator = relatedAgreements.Count,
                        FractionDenominator = allAgreementCount,
                    });
                }
            }

            agreementFractions.Add(agreementFraction);
        }

        return agreementFractions;
    }

    private static DateTimeOffset ToUtcOffset(DateTime dt)
    {
        // Treat unspecified/local as UTC by convention (adjust if your domain expects local time).
        var utc = dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        return new DateTimeOffset(utc);
    }
}