using Microsoft.Extensions.Options;
using System.Linq;
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

        foreach(var currentBid in buildingsWithUnevenDistribution)
        {
            var relatedAgreements = connectionLines
                .Where(l => l.Bid == currentBid)
                .ToList();

            var closedAgreements = relatedAgreements.Where(a => a.ToDate != null).ToList();
            var openAgreements = relatedAgreements.Where(a => a.ToDate == null).ToList();

            int? totalUnitCount = relatedAgreements.FirstOrDefault(n => n.NrOfOccupancyUnits != null).NrOfOccupancyUnits;
            if (relatedAgreements.Count == 0 || totalUnitCount == null) continue;

            closedAgreements.ForEach(a => a.NrOfOccupancyUnits = 0);

            if(openAgreements.Count > 0)
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

        foreach (var agreementGroup in agreementGroups)
        {
            if (hasUnevenUnitDistribution.Contains(agreementGroup.Key)) continue;

            var timelines = new List<ConnectionTimelineDto>();
            var currentLines = agreementGroup
                .OrderBy(l => l.FromDate)
                .ToList();

            var agreementExemptions = relatedExemptions
                .Where(e => e.AgreementId == agreementGroup.Key && e.ToDate == null)
                .ToList();

            //Closed intervals
            for (int i = 0; i < currentLines.Count - 1; i++)
            {
                var (fromDate, toDate) = GetFromToDates(currentLines[i], currentLines[i + 1]);

                var dataQualityError = CheckDataQualityError(currentLines[i]);
                if (dataQualityError != null)
                {
                    timelineErrors.Add(dataQualityError.Value);
                }

                timelines.Add(new ConnectionTimelineDto
                {
                    AgreementId = Int32.Parse(currentLines[i].ExternalAgreementId),
                    IsConnectedToGarbagePickupSystem = IsConnectedToGarbagePickupSystem(currentLines[i].PlaceType),
                    IsConnectedToPublicContainer = IsPublicContainer(currentLines[i].PlaceType),
                    IncludedUtilityUnitsCount = currentLines[i].NrOfOccupancyUnits,
                    DateFrom = fromDate,
                    DateTo = toDate,
                    UtilityUnitConnectionType = GetUtilitytype(currentLines[i].BuildingType),
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

            var lastInterval = currentLines[^1];

            timelines.Add(new ConnectionTimelineDto
            {
                AgreementId = Int32.Parse(lastInterval.ExternalAgreementId),
                IsConnectedToGarbagePickupSystem = IsConnectedToGarbagePickupSystem(lastInterval.PlaceType),
                IsConnectedToPublicContainer = IsPublicContainer(lastInterval.PlaceType),
                IncludedUtilityUnitsCount = lastInterval.NrOfOccupancyUnits,
                DateFrom = lastInterval.FromDate.AddHours(12),
                DateTo = lastInterval.ToDate?.AddHours(12),
                CompostType = compostType,
                UtilityUnitConnectionType = GetUtilitytype(lastInterval.BuildingType),
            });

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

    #region Private Helpers

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

        if(fromDate >= toDate?.Date)
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

    private bool IsPublicContainer(string placeType)
    {
        return options.Value.PublicContainerNames
            .Select(p => p.ToLower())
            .Contains(placeType.ToLower());
    }

    private UtilityUnitConnectionType GetUtilitytype(string buildingType)
    {
        bool isCabin = !string.IsNullOrWhiteSpace(buildingType) && buildingType.StartsWith("16");
        return isCabin ? UtilityUnitConnectionType.Cabin : UtilityUnitConnectionType.Housing;
    }

    #endregion
}
