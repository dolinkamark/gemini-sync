using Ymir.GeminiSync.Domain;
using Ymir.GeminiSync.Services.Models;

namespace Ymir.GeminiSync.Services.ManualTests
{
    public static class GeminiUtils
    {
        /// <summary>
        /// For each PlaceNr, builds contiguous DATE intervals (inclusive FromDate/ToDate)
        /// and groups AgreementIds that overlap each interval.
        ///
        /// Assumptions:
        /// - FromDate and ToDate represent whole dates (no time-of-day meaning).
        /// - ToDate is inclusive. Null means "open ended".
        ///
        /// Intervals are split at boundaries where the active set can change:
        /// - any FromDate
        /// - the day AFTER any ToDate (since ToDate is inclusive)
        /// </summary>
        public static List<PlaceAgreementInterval> BuildIntervalsByDate(List<AgreementPlaceHistoryLine> lines)
        {
            if (lines == null) return new();

            var result = new List<PlaceAgreementInterval>();
            var placeGroups = lines
                .GroupBy(x => x.PlaceNr)
                .OrderBy(g => g.Key);

            foreach(var placeGroup in placeGroups)
            {
                var placeNr = placeGroup.Key;
                var placeLines = placeGroup.ToList();

                // Change points are dates when an interval could start.
                // - every FromDate
                // - (ToDate + 1 day) because ToDate is inclusive
                var changePoints = new SortedSet<DateTime>();
                foreach (var l in placeLines)
                {
                    changePoints.Add(l.FromDate);
                    if (l.ToDate.HasValue)
                    {
                        changePoints.Add(l.ToDate.Value.AddDays(1));
                    }
                }

                var points = changePoints.ToList();
                for (int i = 0; i < points.Count; i++)
                {
                    var intervalStart = points[i];

                    DateTime? intervalEnd = null;
                    if (i < points.Count - 1)
                    {
                        var nextStart = points[i + 1];
                        intervalEnd = nextStart.AddDays(-1);

                        // Defensive: if input had weird overlaps that create nextStart == intervalStart,
                        // then intervalEnd would be < intervalStart. Skip those empty intervals.
                        if (intervalEnd.Value < intervalStart)
                            continue;
                    }

                    // Overlap test for date ranges (inclusive):
                    // Agreement overlaps interval if:
                    //   agreement.FromDate <= intervalEnd (or open-ended)
                    //   AND agreement.ToDate (or Max) >= intervalStart
                    var endForCompare = intervalEnd ?? DateTime.MaxValue.Date;

                    var activeLines = placeLines
                    .Where(l =>
                    {
                        var to = l.ToDate ?? DateTime.MaxValue.Date;
                        return l.FromDate <= endForCompare && to >= intervalStart;
                    })
                    .ToList();

                    if (activeLines.Count == 0)
                        continue;

                    var activeAgreementIds = activeLines
                        .Select(l => l.AgreementId)
                        .Distinct()
                        .OrderBy(id => id)
                        .ToList();

                    var geminiAgreementIds = activeLines
                        .Select(l => l.ExternalAgreementId)
                        .Distinct()
                        .OrderBy(id => id)
                        .ToList();

                    var intervalUpdatedAt = activeLines
                    .Max(l => l.UpdatedAt > l.FromDate
                              ? l.UpdatedAt
                              : l.FromDate);

                    // Merge adjacent intervals if identical agreement set;
                    // UpdatedAt becomes the max across merged parts (safe + intuitive).
                    if (result.Count > 0)
                    {
                        var prev = result[^1];
                        if (prev.PlaceNr == placeNr &&
                            SameAgreements(prev.AgreementIds, activeAgreementIds) &&
                            prev.ToDate.HasValue &&
                            prev.ToDate.Value.AddDays(1) == intervalStart)
                        {
                            prev.ToDate = intervalEnd;
                            if (intervalUpdatedAt > prev.UpdatedAt)
                                prev.UpdatedAt = intervalUpdatedAt;
                            continue;
                        }
                    }

                    result.Add(new PlaceAgreementInterval
                    {
                        PlaceNr = placeNr,
                        FromDate = intervalStart,
                        ToDate = intervalEnd,
                        AgreementIds = geminiAgreementIds,
                        UpdatedAt = intervalUpdatedAt
                    });
                }
            }

            return result;
        }

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
                        .GroupBy(l => l.ExternalAgreementId)
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

        private static bool SameGeminiMapping(
            Dictionary<int, List<int>> a,
            Dictionary<int, List<int>> b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            if (a.Count != b.Count) return false;

            foreach (var kv in a)
            {
                if (!b.TryGetValue(kv.Key, out var bList))
                    return false;

                var aList = kv.Value ?? new List<int>();
                bList ??= new List<int>();

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

        public static List<FractionInTime> ToFractionsInTime(List<PlaceAgreementInterval> intervals)
        {
            var fractionInTimeList = intervals
                .Select(interval =>
                {
                    var denominator = interval.AgreementIds.Count;

                    return new FractionInTime
                    {
                        DateFrom = new DateTimeOffset(interval.FromDate),
                        DateTo = interval.ToDate.HasValue
                                    ? new DateTimeOffset(interval.ToDate.Value)
                                    : (DateTimeOffset?)null,
                        ModifiedAt = new DateTimeOffset(interval.UpdatedAt),

                        Agreements = interval.AgreementIds
                            .Select((agreementId, index) => new FractionAgreement
                            {
                                AgreementId = agreementId,
                                FractionNumerator = 1,
                                FractionDenominator = denominator
                            })
                            .ToList()
                    };
                })
                .ToList();

            return fractionInTimeList;
        }

        public static List<AgreementFractionTimeline> ToFractionTimelines(
        List<FractionInTime> intervals)
        {
            if (intervals == null || intervals.Count == 0)
                return new();

            return intervals
                // Flatten: one row per (interval + agreement fraction)
                .SelectMany(interval =>
                    interval.Agreements.Select(agreement => new
                    {
                        agreement.AgreementId,
                        interval.DateFrom,
                        interval.DateTo,
                        agreement.FractionNumerator,
                        agreement.FractionDenominator
                    }))
                // Group by agreement
                .GroupBy(x => x.AgreementId)
                .Select(group => new AgreementFractionTimeline
                {
                    AgreementId = group.Key,

                    FractionsInTime = group
                        .OrderBy(x => x.DateFrom)
                        .Select(x => new FractionTimeEntry
                        {
                            DateFrom = x.DateFrom,
                            DateTo = x.DateTo,
                            FractionNumerator = x.FractionNumerator,
                            FractionDenominator = x.FractionDenominator
                        })
                        .ToList()
                })
                .OrderBy(x => x.AgreementId)
                .ToList();
        }

        public static List<FractionInTime> BuildFractionsInTime(List<AgreementPlaceHistoryLine> lines)
        {
            if (lines == null) return new();

            var result = new List<FractionInTime>();
            var placeGroups = lines
                .GroupBy(x => x.PlaceNr)
                .OrderBy(g => g.Key);

            foreach (var placeGroup in placeGroups)
            {
                var placeLines = placeGroup
                .Select(l => new
                {
                    l.AgreementId,
                    FromDate = l.FromDate.Date,
                    ToDate = l.ToDate?.Date,
                    EffectiveUpdatedAt =
                        l.UpdatedAt > l.FromDate
                            ? l.UpdatedAt
                            : l.FromDate
                })
                .ToList();

                // Change points are dates when an interval could start.
                // - every FromDate
                // - (ToDate + 1 day) because ToDate is inclusive
                var changePoints = new SortedSet<DateTime>();
                foreach (var l in placeLines)
                {
                    changePoints.Add(l.FromDate);
                    if (l.ToDate.HasValue)
                    {
                        changePoints.Add(l.ToDate.Value.AddDays(1));
                    }
                }

                var points = changePoints.ToList();
                FractionInTime? previous = null;

                for (int i = 0; i < points.Count; i++)
                {
                    var intervalStart = points[i];

                    DateTime? intervalEnd = null;

                    if (i < points.Count - 1)
                    {
                        intervalEnd = points[i + 1].AddDays(-1);

                        if (intervalEnd < intervalStart)
                            continue;
                    }

                    var compareEnd = intervalEnd ?? DateTime.MaxValue.Date;

                    // Find active agreements
                    var active = placeLines
                        .Where(l =>
                        {
                            var to = l.ToDate ?? DateTime.MaxValue.Date;
                            return l.FromDate <= compareEnd &&
                                   to >= intervalStart;
                        })
                        .OrderBy(l => l.AgreementId)
                        .ToList();

                    if (!active.Any())
                        continue;

                    var denominator = active.Count;

                    var modifiedAt = active.Max(a => a.EffectiveUpdatedAt);

                    var agreements = active
                        .Select((a, index) => new FractionAgreement
                        {
                            AgreementId = a.AgreementId,
                            FractionNumerator = index + 1,
                            FractionDenominator = denominator
                        })
                        .ToList();

                    var current = new FractionInTime
                    {
                        DateFrom = new DateTimeOffset(intervalStart),
                        DateTo = intervalEnd.HasValue
                                    ? new DateTimeOffset(intervalEnd.Value)
                                    : null,
                        ModifiedAt = new DateTimeOffset(modifiedAt),
                        Agreements = agreements
                    };

                    // Merge adjacent equal agreement sets
                    if (previous != null &&
                        SameFractions(previous.Agreements, current.Agreements) &&
                        previous.DateTo.HasValue &&
                        previous.DateTo.Value.Date.AddDays(1) == current.DateFrom.Date)
                    {
                        previous.DateTo = current.DateTo;

                        if (current.ModifiedAt > previous.ModifiedAt)
                            previous.ModifiedAt = current.ModifiedAt;

                        continue;
                    }

                    result.Add(current);
                    previous = current;
                }
            }

            return result;
        }

        /// <summary>
        /// Builds a timeline of collection "states" where each state is defined by the set (and count)
        /// of active GarbageBinCollectionLine entries ("bins") for a contiguous date range.
        ///
        /// Assumptions (matches your example):
        /// - We group on whole days (Date component only).
        /// - FromDate is inclusive (active starting that date).
        /// - ToDate is treated as the *change date* (exclusive). So an interval ends the day before ToDate.Date.
        /// - ToDate == 1900-01-01 (or <= that) means "open-ended" (no end).
        /// </summary>
        public static List<CollectionStateInTime> BuildAgreementIntervalsByDate(List<GarbageBinCollectionLine> lines)
        {
            if (lines == null || lines.Count == 0)
                return new List<CollectionStateInTime>();

            // date -> (adds, removes)
            var events = new SortedDictionary<DateTime, (List<GarbageBinCollectionLine> Adds, List<GarbageBinCollectionLine> Removes)>();

            void Ensure(DateTime d)
            {
                if (!events.TryGetValue(d, out _))
                    events[d] = (new List<GarbageBinCollectionLine>(), new List<GarbageBinCollectionLine>());
            }

            foreach (var line in lines)
            {
                var start = line.FromDate.Date;

                Ensure(start);
                events[start].Adds.Add(line);

                var endExclusive = GetEndExclusiveDateOrNull(line.ToDate);
                if (endExclusive.HasValue)
                {
                    Ensure(endExclusive.Value);
                    events[endExclusive.Value].Removes.Add(line);
                }
            }

            // Active set keyed by AgreementLineId (stable + unique in your model)
            var active = new Dictionary<long, GarbageBinCollectionLine>();
            var result = new List<CollectionStateInTime>();

            DateTime? currentStart = null;

            foreach (var kvp in events)
            {
                var date = kvp.Key;
                var (adds, removes) = kvp.Value;

                if (currentStart == null)
                {
                    // First event date: apply changes, start timeline here.
                    Apply(removes, adds, active);
                    if (active.Count > 0)
                        currentStart = date;

                    continue;
                }

                // We have an active state from currentStart up to (date - 1 day)
                if (currentStart.Value < date)
                {
                    var endInclusive = date.AddDays(-1);

                    // Only emit if we had something active during this span
                    if (active.Count > 0)
                    {
                        result.Add(new CollectionStateInTime
                        {
                            StartDate = currentStart.Value.AddHours(12),
                            EndDate = endInclusive.AddHours(12),
                            Lines = active.Values.ToList()
                        });
                    }
                }

                // Apply changes effective at this 'date', then next segment starts here (if anything active)
                Apply(removes, adds, active);
                currentStart = active.Count > 0 ? date : (DateTime?)null;
            }

            // Trailing open-ended segment
            if (currentStart != null && active.Count > 0)
            {
                result.Add(new CollectionStateInTime
                {
                    StartDate = currentStart.Value.AddHours(12),
                    EndDate = null,
                    Lines = active.Values.ToList()
                });
            }

            return result;

            static void Apply(
                List<GarbageBinCollectionLine> removes,
                List<GarbageBinCollectionLine> adds,
                Dictionary<long, GarbageBinCollectionLine> active)
            {
                // Remove first, then add (important when something ends and another starts same day)
                foreach (var r in removes)
                    active.Remove(r.AgreementLineId);

                foreach (var a in adds)
                    active[a.AgreementLineId] = a;
            }

            static DateTime? GetEndExclusiveDateOrNull(DateTime toDate)
            {
                // Sentinel for "no end" in your data
                if (toDate.Date <= new DateTime(1900, 1, 1))
                    return null;

                // End is exclusive at day granularity: the state changes on toDate.Date
                return toDate.Date;
            }
        }

        public static List<CollectionStateInTime> SplitByDates(List<GarbageBinCollectionLine> lines)
        {
            var states = new List<CollectionStateInTime>();
            var noEndDate = new DateTime(1900, 1, 1);
            var startDateGroups = lines
                .GroupBy(l => l.FromDate.Date)
                .OrderBy(l => l.Key)
                .ToList();

            for(int i = 0; i < startDateGroups.Count; ++i)
            {
                var currentState = new CollectionStateInTime();
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

        public static GarbageBinCategory ToGarbageBinCategory(string? category)
        {
            if (string.IsNullOrWhiteSpace(category))
                return GarbageBinCategory.OtherWaste;

            return category.Trim().ToLowerInvariant() switch
            {
                "bio" => GarbageBinCategory.Bio,
                "mat/hage" => GarbageBinCategory.Bio,
                "juletre" => GarbageBinCategory.Bio,

                "papp/papir" => GarbageBinCategory.Paper,

                "glass" => GarbageBinCategory.GlassAndMetal,

                "restavfall" => GarbageBinCategory.OtherWaste,
                "plast" => GarbageBinCategory.OtherWaste,

                _ => GarbageBinCategory.OtherWaste
            };
        }

        private static bool SameAgreements(List<int> a, List<int> b)
        {
            if (a.Count != b.Count) return false;

            for (int i = 0; i < a.Count; i++)
                if (a[i] != b[i]) return false;

            return true;
        }

        private static bool SameFractions(
            List<FractionAgreement> a,
            List<FractionAgreement> b)
        {
            if (a.Count != b.Count) return false;

            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].AgreementId != b[i].AgreementId)
                    return false;
            }

            return true;
        }
    }

    public class CollectionStateInTime
    {
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public List<GarbageBinCollectionLine> Lines { get; set; } = new List<GarbageBinCollectionLine>();
    }
}
