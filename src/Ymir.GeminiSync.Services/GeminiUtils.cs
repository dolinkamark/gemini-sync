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

                    //Adjust Occupancy units
                    foreach(var activeLine in activeLines)
                    {
                        if(activeLine.NrOfOccupancyUnits == null || activeLine.NrOfOccupancyUnits == 0)
                        {
                            activeLine.NrOfOccupancyUnits = 1;
                        }
                    }

                    var agreementOccupancyList = activeLines
                        .Select(l => new AgreementOccupancy
                        {
                            AgreementId = l.AgreementId,
                            GeminiAgreementId = Int32.Parse(l.ExternalAgreementId),
                            NrOfOccupancyUnits = (l.NrOfOccupancyUnits ?? 1),
                        })
                        .Distinct()
                        .OrderBy(l => l.GeminiAgreementId)
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
                            SameAgreements(prev.AgreementOccupancyList.Select(i => i.GeminiAgreementId).ToList(), activeAgreementIds) &&
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
                        UpdatedAt = intervalUpdatedAt,
                        AgreementOccupancyList = agreementOccupancyList,
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

        public static List<FractionInTime> ToFractionsInTime(List<PlaceAgreementInterval> intervals)
        {
            var fractionInTimeList = new List<FractionInTime>();

            foreach(var interval in intervals)
            {
                var denominator = interval.AgreementOccupancyList.Sum(o => o.NrOfOccupancyUnits);

                fractionInTimeList.Add(new FractionInTime
                {
                    DateFrom = new DateTimeOffset(interval.FromDate),
                    DateTo = interval.ToDate.HasValue
                                ? new DateTimeOffset(interval.ToDate.Value)
                                : (DateTimeOffset?)null,
                    ModifiedAt = new DateTimeOffset(interval.UpdatedAt),

                    Agreements = interval.AgreementOccupancyList
                        .Select(occupancy => new FractionAgreement
                        {
                            AgreementId = occupancy.GeminiAgreementId,
                            FractionNumerator = occupancy.NrOfOccupancyUnits,
                            FractionDenominator = denominator
                        })
                        .ToList()
                });
            }

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

        public static GarbageBinsFrequencyToBeInvoiced MapGarbageBinFrequency(int? frequency)
        {
            switch (frequency)
            {
                case 2: return GarbageBinsFrequencyToBeInvoiced.Weekly;
                case 1:
                default:
                    return GarbageBinsFrequencyToBeInvoiced.BiWeekly;

            }
        }

        #region Private Helpers

        private static bool SameAgreements(List<int> a, List<long> b)
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

        #endregion
    }
}
