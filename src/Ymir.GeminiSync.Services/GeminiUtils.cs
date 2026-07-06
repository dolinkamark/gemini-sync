using Ymir.GeminiSync.Domain;
using Ymir.GeminiSync.Services.Models;

namespace Ymir.GeminiSync.Services.ManualTests
{
    public static class GeminiUtils
    {
        public static List<PlaceGeminiToAgreementInterval> BuildGeminiToAgreementIntervalsByDate(
            List<AgreementPlaceHistoryLine> lines)
        {
            if (lines == null) return new();

            var result = new List<PlaceGeminiToAgreementInterval>();

            var placeGroups = lines
                .GroupBy(x => x.PlaceNr)
                .OrderBy(g => g.Key);

            foreach (var placeGroup in placeGroups)
            {
                var placeNr = placeGroup.Key;
                var placeLines = placeGroup.ToList();
                var changePoints = new SortedSet<DateTime>();

                foreach (var l in placeLines)
                {
                    changePoints.Add(l.FromDate.Date);
                    if (l.ToDate.HasValue)
                    {
                        changePoints.Add(l.ToDate.Value.Date);
                    }
                }

                var points = changePoints.ToList();
                for (int i = 0; i < points.Count; i++)
                {
                    var intervalStart = points[i].Date;

                    DateTime? intervalEnd = null;
                    if (i < points.Count - 1)
                    {
                        var nextStart = points[i + 1].Date;
                        intervalEnd = nextStart.AddDays(-1);

                        // Defensive: skip empty/invalid intervals
                        if (intervalEnd.Value < intervalStart)
                            continue;
                    }

                    var endForCompare = intervalEnd ?? DateTime.MaxValue.Date;

                    // Active lines overlap the interval (inclusive range logic)
                    var activeLines = placeLines
                        .Where(l =>
                        {
                            var to = (l.ToDate ?? DateTime.MaxValue.Date).Date;
                            var from = l.FromDate.Date;
                            return from <= endForCompare && to > intervalStart;
                        })
                        .ToList();

                    if (activeLines.Count == 0)
                        continue;

                    // Build mapping: ExternalAgreementId (Gemini) -> distinct AgreementIds
                    var geminiToAgreement = activeLines
                        .GroupBy(l => Int32.Parse(l.ExternalAgreementId))
                        .OrderBy(g => g.Key)
                        .ToDictionary(
                            g => g.Key,
                            g => g.Select(x => x.AgreementId).Distinct().OrderBy(id => id).ToList()
                        );

                    var intervalUpdatedAt = activeLines
                        .Max(l => (l.UpdatedAt > l.FromDate ? l.UpdatedAt : l.FromDate));

                    // Merge adjacent intervals if mapping is identical
                    if (result.Count > 0)
                    {
                        var prev = result[^1];
                        if (prev.PlaceNr == placeNr &&
                            prev.ToDate.HasValue &&
                            prev.ToDate.Value.Date.AddDays(1) == intervalStart &&
                            SameGeminiMapping(prev.GeminiToAgreementIds, geminiToAgreement))
                        {
                            prev.ToDate = intervalEnd;
                            if (intervalUpdatedAt > prev.UpdatedAt)
                                prev.UpdatedAt = intervalUpdatedAt;
                            continue;
                        }
                    }

                    result.Add(new PlaceGeminiToAgreementInterval
                    {
                        PlaceNr = placeNr,
                        FromDate = intervalStart,
                        ToDate = intervalEnd,
                        GeminiToAgreementIds = geminiToAgreement,
                        UpdatedAt = intervalUpdatedAt
                    });
                }
            }

            return result;
        }

        public static List<StateInTimeCollection> SplitByDates(List<GarbageBinCollectionLine> lines)
        {
            var states = new List<StateInTimeCollection>();
            var noEndDate = new DateTime(1900, 1, 1);
            var startDateGroups = lines
                .GroupBy(l => l.FromDate.Date)
                .OrderBy(l => l.Key)
                .ToList();

            for(int i = 0; i < startDateGroups.Count; ++i)
            {
                var currentState = new StateInTimeCollection();
                var currentGroup = startDateGroups[i];

                currentState.StartDate = currentGroup.Key;
                if (i < startDateGroups.Count - 1)
                {
                    currentState.EndDate = startDateGroups[i + 1].Key;
                }
                else
                {
                    currentState.EndDate = null;
                }

                var previousLines = lines.Where(l => 
                    l.FromDate.Date < currentState.StartDate && 
                    (l.ToDate == noEndDate || l.ToDate <= currentState.EndDate)
                ).ToList();

                var relatedLinesNoEnd = lines
                    .Where(l => l.FromDate.Date == currentState.StartDate)
                    .ToList();

                currentState.Lines.AddRange(previousLines);
                currentState.Lines.AddRange(relatedLinesNoEnd);

                //Ensure no UTC shenanigans happen
                currentState.StartDate = currentState.StartDate.AddHours(12);

                //Adjust end
                if (currentState.EndDate != null)
                {
                    currentState.EndDate = currentState.EndDate.Value.AddDays(-1);
                    currentState.EndDate = currentState.EndDate.Value.AddHours(12);
                }

                states.Add(currentState);
            }

            return states;
        }

        public static GarbageBinCategory FromLoglineName(string loglineName)
        {
            if (string.IsNullOrWhiteSpace(loglineName))
                return GarbageBinCategory.OtherWaste;

            var lowerCaseName = loglineName.Trim().ToLowerInvariant();

            if (lowerCaseName.Contains("restavfall")) return GarbageBinCategory.OtherWaste;
            if (lowerCaseName.Contains("papp/papir")) return GarbageBinCategory.Paper;
            if (lowerCaseName.Contains("bio")) return GarbageBinCategory.Bio;
            if (lowerCaseName.Contains("glass")) return GarbageBinCategory.GlassAndMetal;

            return GarbageBinCategory.OtherWaste;
        }

        private static bool SameGeminiMapping(
            Dictionary<int, List<long>> a,
            Dictionary<int, List<long>> b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            if (a.Count != b.Count) return false;

            foreach (var kv in a)
            {
                if (!b.TryGetValue(kv.Key, out var bList))
                    return false;

                var aList = kv.Value ?? new List<long>();
                bList ??= new List<long>();

                if (aList.Count != bList.Count)
                    return false;

                for (int i = 0; i < aList.Count; i++)
                {
                    if (aList[i] != bList[i])
                        return false;
                }
            }

            return true;
        }

    }
}
