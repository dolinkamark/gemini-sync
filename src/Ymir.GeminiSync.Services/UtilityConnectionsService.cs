using Microsoft.Extensions.Options;
using Ymir.GeminiSync.Domain;
using Ymir.GeminiSync.Services.Abstract;
using Ymir.GeminiSync.Services.Models;

namespace Ymir.GeminiSync.Services;

public class UtilityConnectionsService(IOptions<UtilityConnectionsServiceOptions> options) : IUtilityConnectionsService
{
    public List<(long, UtilityUnitConnectionUpdateDto)> CreateUtilityUnitTimelines(
        List<AgreementPlaceConnectionLine> connectionLines,
        List<AgreementExcemption> exemptions)
    {
        var utilityUnitTimelines = new List<(long, UtilityUnitConnectionUpdateDto)>();

        //TODO: Add validation, shouldn't have empty or null ExternalAgreementId
        var agreementGroups = connectionLines
            .Where(l => !String.IsNullOrWhiteSpace(l.ExternalAgreementId))
            .GroupBy(l => l.AgreementId)
            .ToList();

        var agreementGroupsByBid = connectionLines
            .Where(c => c.NrOfOccupancyUnits > 1 && (c.ToDate == null || c.ToDate > (new DateTime(2026, 5, 1))))
            .GroupBy(c => c.Bid)
            .Where(b => b.Count() > 1)
            .ToList();

        var relatedExemptions = exemptions
            .Where(e => e.ExcemptionType == 2 || e.ExcemptionType == 4)
            .ToList();

        //Adjust occupancy by the bid grouping
        var unitDivisionErrors = new List<(string, long, string, int, int?, int)>();
        foreach (var agreementGroupByBid in agreementGroupsByBid)
        {
            var totalUnits = agreementGroupByBid.First().NrOfOccupancyUnits;
            var currentAgreements = agreementGroupByBid.ToList();

            if (totalUnits % currentAgreements.Count != 0)
            {
                //unitDivisionErrors.Add((agreementGroup.Key, totalUnits, currentAgreements.Count));

                unitDivisionErrors.AddRange(
                    currentAgreements.Select(c => (agreementGroupByBid.Key, c.AgreementId, c.ExternalAgreementId, c.PlaceNr, totalUnits, currentAgreements.Count))
                );
            }
            else
            {
                foreach (var agreement in currentAgreements)
                {
                    agreement.NrOfOccupancyUnits = totalUnits / currentAgreements.Count;
                }
            }
        }

        //Update specific rules to uneven utility unit count distrubiton
        var hasUnevenUnitDistribution = unitDivisionErrors
            .Select(e => e.Item2)
            .Distinct()
            .ToList();

        var buildingsWithUnevenDistribution = unitDivisionErrors
            .Select(e => e.Item1)
            .Distinct()
            .ToList();

        foreach (var currentBid in buildingsWithUnevenDistribution)
        {
            var relatedAgreements = connectionLines
                .Where(l => l.Bid == currentBid)
                .ToList();

            var closedAgreements = relatedAgreements.Where(a => a.ToDate != null).ToList();
            var openAgreements = relatedAgreements.Where(a => a.ToDate == null).ToList();

            int? totalUnitCount = relatedAgreements.FirstOrDefault(n => n.NrOfOccupancyUnits != null).NrOfOccupancyUnits;
            if (relatedAgreements.Count == 0 || totalUnitCount == null) continue;

            closedAgreements.ForEach(a => a.NrOfOccupancyUnits = 0);

            if (openAgreements.Count > 0)
            {
                openAgreements[0].NrOfOccupancyUnits = totalUnitCount;
                for (int i = 1; i < openAgreements.Count; ++i)
                {
                    openAgreements[i].NrOfOccupancyUnits = 0;
                }
            }
        }

        //Data quality validation
        var timelineErrors = new List<(long, DateTime?, DateTime?)>();

        var multiplePlaces = agreementGroups
            .Where(g => g.GroupBy(l => l.PlaceType).Count() > 1)
            .Select(s => s.Key)
            .ToList();

        var multipleOpenTimelines = agreementGroups
            .Where(g => g.Count(l => l.ToDate == null) > 1)
            .Select(s => s.Key)
            .ToList();

        foreach (var agreementGroup in agreementGroups)
        {
            var timelines = new List<ConnectionTimelineDto>();
            var currentLines = agreementGroup
                .OrderBy(l => l.FromDate)
                .ToList();

            var agreementExemptions = relatedExemptions
                .Where(e => e.AgreementId == agreementGroup.Key && e.ToDate == null)
                .ToList();

            var splitTimeline = SplitTimeline(currentLines);

            foreach (var split in splitTimeline)
            {
                var firstLine = split.Connections.First();
                var placeList = split.Connections.Select(c => c.PlaceType).ToList();
                var totalUnits = split.Connections.Sum(c => c.NrOfOccupancyUnits);

                timelines.Add(new ConnectionTimelineDto
                {
                    AgreementId = Int32.Parse(firstLine.ExternalAgreementId),
                    IsConnectedToGarbagePickupSystem = IsConnectedToGarbagePickupSystem(placeList),
                    IsConnectedToPublicContainer = IsPublicContainer(placeList),
                    IncludedUtilityUnitsCount = totalUnits,
                    DateFrom = split.StartDate.AddHours(12),
                    DateTo = split.ToDate?.AddHours(12),
                    UtilityUnitConnectionType = GetUtilitytype(firstLine.BuildingType),
                });
            }

            //Last interval
            CompostType? compostType = null;
            if (agreementExemptions.Any(e => e.ExcemptionType == 4))
            {
                compostType = CompostType.GardenAndFood;
            }
            else if (agreementExemptions.Any(e => e.ExcemptionType == 2))
            {
                compostType = CompostType.Food;
            }

            if(timelines.Count > 0)
            {
                timelines[timelines.Count - 1].CompostType = compostType;
            }

            utilityUnitTimelines.Add((
                agreementGroup.Key,
                new UtilityUnitConnectionUpdateDto
                {
                    ConnectionsInTime = timelines
                })
            );
        }

        return utilityUnitTimelines;
    }

    /// <summary>
    /// Checks whether the returned timeline from Gemini API equals the update timeline.
    /// </summary>
    public bool AreTimelinesEqual(List<ConnectionTimelineDto> firstTimeline, List<ConnectionTimelineDto> secondTimeline)
    {
        if (firstTimeline.Count != secondTimeline.Count) return false;

        return firstTimeline.SequenceEqual(secondTimeline);
    }

    #region Private Helpers

    private List<ConnectionTimelinePeriod> SplitTimeline(List<AgreementPlaceConnectionLine> connectionLines)
    {
        ArgumentNullException.ThrowIfNull(connectionLines);

        // Materializing the input ensures that the same object instances are
        // referenced from the generated periods.
        var connectionList = connectionLines.ToList();

        if (connectionList.Count == 0)
        {
            return new List<ConnectionTimelinePeriod>();
        }

        AdjustConnectionDates(connectionList);

        var boundaries = connectionList
            .SelectMany(connection =>
            {
                var dates = new List<DateTime>
                {
                    connection.FromDate
                };

                if (connection.ToDate.HasValue)
                {
                    dates.Add(connection.ToDate.Value);
                }

                return dates;
            })
            .Distinct()
            .OrderBy(date => date)
            .ToList();

        var result = new List<ConnectionTimelinePeriod>();

        for (var index = 0; index < boundaries.Count; index++)
        {
            var startDate = boundaries[index];

            // The end date is inclusive, so it is one day before
            // the start date of the following period.
            DateTime? toDate = index + 1 < boundaries.Count
                ? boundaries[index + 1].AddDays(-1)
                : null;

            var activeConnections = connectionList
                .Where(connection =>
                    connection.FromDate <= startDate &&
                    (!connection.ToDate.HasValue ||
                     connection.ToDate.Value > startDate))
                .ToList();

            // Do not generate periods during which no connection is active.
            if (activeConnections.Count == 0)
            {
                continue;
            }

            result.Add(new ConnectionTimelinePeriod
            {
                StartDate = startDate,
                ToDate = toDate,
                Connections = activeConnections
            });
        }

        return result;
    }

    private void AdjustConnectionDates(List<AgreementPlaceConnectionLine> connections)
    {
        foreach (var connection in connections)
        {
            if (connection.ToDate.HasValue &&
                connection.ToDate.Value <= connection.FromDate)
            {
                connection.FromDate = connection.ToDate.Value.Date.AddDays(-1);
            }
        }
    }

    private (long, DateTime?, DateTime?)? CheckDataQualityError(AgreementPlaceConnectionLine currentLine)
    {
        if (currentLine.FromDate == currentLine.ToDate)
        {
            return (currentLine.AgreementId, currentLine.FromDate, currentLine.ToDate);
        }

        return null;
    }

    private (DateTime fromDate, DateTime? toDate) GetFromToDates(AgreementPlaceConnectionLine currentLine, AgreementPlaceConnectionLine nextLine)
    {
        DateTime fromDate = currentLine.FromDate;
        DateTime? toDate = currentLine.ToDate;

        if (currentLine.ToDate == null || currentLine.ToDate?.Date >= nextLine.FromDate)
        {
            toDate = nextLine.FromDate.AddDays(-1);
        }

        if (fromDate >= toDate?.Date)
        {
            fromDate = toDate?.AddDays(-1) ?? fromDate;
        }

        return (fromDate.AddHours(12), toDate?.AddHours(12));
    }

    private bool IsConnectedToGarbagePickupSystem(string placeType)
    {
        return !options.Value.NotConnectedToPickupSystem
            .Select(n => n.ToLower())
            .Contains(placeType.ToLower());
    }

    private bool IsConnectedToGarbagePickupSystem(List<string> placeTypes)
    {
        var notConnected = options.Value.NotConnectedToPickupSystem
            .Select(n => n.ToLower())
            .ToList();

        return placeTypes.Any(p => !notConnected.Contains(p.ToLower()));
    }

    private bool IsPublicContainer(string placeType)
    {
        return options.Value.PublicContainerNames
            .Select(p => p.ToLower())
            .Contains(placeType.ToLower());
    }

    private bool IsPublicContainer(List<string> placeTypes)
    {
        var publicContainerList = options.Value.PublicContainerNames
            .Select(n => n.ToLower())
            .ToList();

        return placeTypes.Any(p => publicContainerList.Contains(p.ToLower()));
    }

    private UtilityUnitConnectionType GetUtilitytype(string buildingType)
    {
        bool isCabin = !string.IsNullOrWhiteSpace(buildingType) && buildingType.StartsWith("16");
        return isCabin ? UtilityUnitConnectionType.Cabin : UtilityUnitConnectionType.Housing;
    }

    #endregion
}
