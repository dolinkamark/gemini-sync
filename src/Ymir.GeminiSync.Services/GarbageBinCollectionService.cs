using Ymir.GeminiSync.Domain;
using Ymir.GeminiSync.Services.Abstract;
using Ymir.GeminiSync.Services.Models;

namespace Ymir.GeminiSync.Services;

public class GarbageBinCollectionService : IGarbageBinCollectionService
{
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
    public List<StateInTimeCollection> BuildStateInTimeCollections(List<GarbageBinCollectionLine> lines)
    {
        if (lines == null || lines.Count == 0)
            return new List<StateInTimeCollection>();

        // date -> (adds, removes)
        var events = new SortedDictionary<DateTime, (List<GarbageBinCollectionLine> Adds, List<GarbageBinCollectionLine> Removes)>();

        void EnsureDateValidity(DateTime d)
        {
            if (!events.TryGetValue(d, out _))
                events[d] = (new List<GarbageBinCollectionLine>(), new List<GarbageBinCollectionLine>());
        }

        foreach (var line in lines)
        {
            var start = line.FromDate.Date;

            EnsureDateValidity(start);
            events[start].Adds.Add(line);

            var endExclusive = GetEndExclusiveDateOrNull(line.ToDate);
            if (endExclusive.HasValue)
            {
                EnsureDateValidity(endExclusive.Value);
                events[endExclusive.Value].Removes.Add(line);
            }
        }

        // Active set keyed by AgreementLineId
        var active = new Dictionary<long, GarbageBinCollectionLine>();
        var stateInTimeCollection = new List<StateInTimeCollection>();

        DateTime? currentStart = null;

        foreach (var currentEvent in events)
        {
            var date = currentEvent.Key;
            var (adds, removes) = currentEvent.Value;

            if (currentStart == null)
            {
                // First event date: apply changes, start timeline here.
                ApplyEvent(removes, adds, active);
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
                    stateInTimeCollection.Add(new StateInTimeCollection
                    {
                        StartDate = currentStart.Value.AddHours(12),
                        EndDate = endInclusive.AddHours(12),
                        Lines = active.Values.ToList()
                    });
                }
            }

            // Apply changes effective at this 'date', then next segment starts here (if anything active)
            ApplyEvent(removes, adds, active);
            currentStart = active.Count > 0 ? date : (DateTime?)null;
        }

        // Trailing open-ended segment
        if (currentStart != null && active.Count > 0)
        {
            stateInTimeCollection.Add(new StateInTimeCollection
            {
                StartDate = currentStart.Value.AddHours(12),
                EndDate = null,
                Lines = active.Values.ToList()
            });
        }

        return stateInTimeCollection;
    }


    private GarbageBinCategory ToGarbageBinCategory(string? category)
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

    private void ApplyEvent(
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

    private DateTime? GetEndExclusiveDateOrNull(DateTime toDate)
    {
        // Sentinel for "no end" in your data
        if (toDate.Date <= new DateTime(1900, 1, 1))
            return null;

        // End is exclusive at day granularity: the state changes on toDate.Date
        return toDate.Date;
    }
}
