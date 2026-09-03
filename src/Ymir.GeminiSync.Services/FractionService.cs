using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ymir.GeminiSync.Domain;
using Ymir.GeminiSync.Services.Abstract;
using Ymir.GeminiSync.Services.Models;

namespace Ymir.GeminiSync.Services;

public class FractionService : IFractionService
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
    public List<PlaceAgreementInterval> BuildFractionIntervalsByDate(List<AgreementPlaceHistoryLine> lines)
    {
        if (lines == null) return new();

        var agreementIntervals = new List<PlaceAgreementInterval>();
        var placeGroups = lines
            .GroupBy(x => x.PlaceNr)
            .OrderBy(g => g.Key);

        //Create the intervals
        foreach (var placeGroup in placeGroups)
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
                foreach (var activeLine in activeLines)
                {
                    if (activeLine.NrOfOccupancyUnits == null || activeLine.NrOfOccupancyUnits == 0)
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
                if (agreementIntervals.Count > 0)
                {
                    var prev = agreementIntervals[^1];
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

                agreementIntervals.Add(new PlaceAgreementInterval
                {
                    PlaceNr = placeNr,
                    FromDate = intervalStart,
                    ToDate = intervalEnd,
                    UpdatedAt = intervalUpdatedAt,
                    AgreementOccupancyList = agreementOccupancyList,
                });
            }
        }

        return agreementIntervals;
    }

    /// <summary>
    /// Creates a list of AgreementFractionTimeline related to every PlaceNr in the intervals
    /// </summary>
    /// <param name="intervals">AgreementPlaceHistory lines grouped as intervals</param>
    /// <returns>A list of tuple containing PlaceNr and the related AgreementFractionTimeline list</returns>
    public List<(int, List<AgreementFractionTimeline>)> CreateFractionTimelines(List<PlaceAgreementInterval> intervals)
    {
        if (intervals == null || intervals.Count == 0)
            return new();

        var timelines = new List<(int, List<AgreementFractionTimeline>)>();
        var intervalsByPlace = intervals
            .GroupBy(i => i.PlaceNr)
            .ToList();

        //Step 1) The intervals must be created for each place separately
        foreach(var intervalList in intervalsByPlace)
        {
            var orderedIntervals = intervalList
                .OrderBy(i => i.FromDate)
                .ToList();

            var agreementEntryList = new List<(int, FractionTimeEntry)>();
            foreach (var currentInterval in orderedIntervals)
            {
                var totalUnits = currentInterval.AgreementOccupancyList.Sum(a => a.NrOfOccupancyUnits);

                var geminiAgreements = currentInterval.AgreementOccupancyList
                    .GroupBy(a => a.GeminiAgreementId)
                    .ToList();

                foreach(var geminiAgreement in geminiAgreements)
                {
                    var currentUnits = geminiAgreement.Sum(a => a.NrOfOccupancyUnits);
                    var dateFrom = currentInterval.FromDate.Date == currentInterval.ToDate?.Date ?
                        currentInterval.FromDate.AddDays(-1) :
                        currentInterval.FromDate;

                    var fractionEntry = new FractionTimeEntry
                    {
                        DateFrom = currentInterval.FromDate,
                        DateTo = currentInterval.ToDate,
                        FractionNumerator = currentUnits,
                        FractionDenominator = totalUnits,
                    };

                    agreementEntryList.Add((geminiAgreement.Key, fractionEntry));
                }
            }

            //Piece the timelines together by agreement
            var agreementFractionTimelines = agreementEntryList
                .GroupBy(entry => entry.Item1)
                .Select(group => new AgreementFractionTimeline
                {
                    AgreementId = group.Key,
                    FractionsInTime = group
                        .Select(entry => entry.Item2)
                        .ToList()
                })
                .ToList();

            timelines.Add((intervalList.Key, agreementFractionTimelines));
        }

        return timelines;
    }

    public bool AreFractionTimelinesEqual(List<AgreementFractionTimeline> first, List<AgreementFractionTimeline> second)
    {
        if (ReferenceEquals(first, second)) return true;
        if (first == null || second == null) return false;
        if (first.Count != second.Count) return false;

        var orderedFirst = first.OrderBy(t => t.AgreementId).ToList();
        var orderedSecond = second.OrderBy(t => t.AgreementId).ToList();

        for (int i = 0; i < orderedFirst.Count; i++)
        {
            var firstTimeline = orderedFirst[i];
            var secondTimeline = orderedSecond[i];

            if (firstTimeline.AgreementId != secondTimeline.AgreementId)
                return false;

            if (firstTimeline.FractionsInTime.Count != secondTimeline.FractionsInTime.Count)
                return false;

            for (int j = 0; j < firstTimeline.FractionsInTime.Count; j++)
            {
                var firstEntry = firstTimeline.FractionsInTime[j];
                var secondEntry = secondTimeline.FractionsInTime[j];

                if (firstEntry.DateFrom.Date != secondEntry.DateFrom.Date
                    || firstEntry.DateTo?.Date != secondEntry.DateTo?.Date
                    || firstEntry.FractionNumerator != secondEntry.FractionNumerator
                    || firstEntry.FractionDenominator != secondEntry.FractionDenominator)
                {
                    return false;
                }
            }
        }

        return true;
    }

    public List<(int, List<AgreementFractionTimeline>)> GetChangedTimelines(
        List<(int, List<AgreementFractionTimeline>)> currentTimelines,
        List<(int, List<AgreementFractionTimeline>)> previousTimelines)
    {
        if (currentTimelines == null || currentTimelines.Count == 0)
            return new();

        var previousByPlaceNr = (previousTimelines ?? new())
            .ToDictionary(t => t.Item1, t => t.Item2);

        return currentTimelines
            .Where(current =>
                !previousByPlaceNr.TryGetValue(current.Item1, out var previous)
                || !AreFractionTimelinesEqual(previous, current.Item2))
            .ToList();
    }

    #region Private Helpers

    private bool SameAgreements(List<int> a, List<long> b)
    {
        if (a.Count != b.Count) return false;

        for (int i = 0; i < a.Count; i++)
            if (a[i] != b[i]) return false;

        return true;
    }

    #endregion
}
