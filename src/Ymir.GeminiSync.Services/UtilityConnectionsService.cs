using Microsoft.Extensions.Options;
using Ymir.GeminiSync.Domain;
using Ymir.GeminiSync.Services.Abstract;
using Ymir.GeminiSync.Services.Models;

namespace Ymir.GeminiSync.Services;

public class UtilityConnectionsService(IOptions<UtilityConnectionsServiceOptions> options) : IUtilityConnectionsService
{
    public List<UtilityUnitConnectionUpdateDto> CreateUtilityUnitTimelines(
        List<AgreementPlaceConnectionLine> connectionLines,
        List<AgreementExcemption> exemptions)
    {
        var utilityUnitTimelines = new List<UtilityUnitConnectionUpdateDto>();

        //TODO: Add validation, shouldn't have empty or null ExternalAgreementId
        var agreementGroups = connectionLines
            .Where(l => !String.IsNullOrWhiteSpace(l.ExternalAgreementId))
            .GroupBy(l => l.AgreementId)
            .ToList();

        var agreementsByBid = connectionLines
            .Where(c => c.NrOfOccupancyUnits > 1 && (c.ToDate == null || c.ToDate > (new DateTime(2026, 5, 1))))
            .GroupBy(c => c.Bid)
            .Where(b => b.Count() > 1)
            .ToList();

        //Adjust occupancy by the bid grouping
        var unitDivisionErrors = new List<(string, long, string, int?, int)>();
        foreach (var agreementGroup in agreementsByBid)
        {
            var totalUnits = agreementGroup.First().NrOfOccupancyUnits;
            var currentAgreements = agreementGroup.ToList();

            if (totalUnits % currentAgreements.Count != 0)
            {
                //unitDivisionErrors.Add((agreementGroup.Key, totalUnits, currentAgreements.Count));

                unitDivisionErrors.AddRange(
                    currentAgreements.Select(c => (agreementGroup.Key, c.AgreementId, c.ExternalAgreementId, totalUnits, currentAgreements.Count))
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

        //Data quality validation
        var timelineErrors = new List<(long, DateTime?, DateTime?)>();

        foreach (var agreementGroup in agreementGroups)
        {
            var timelines = new List<ConnectionTimelineDto>();
            var currentLines = agreementGroup
                .OrderBy(l => l.FromDate)
                .ToList();

            //Closed intervals
            for (int i = 0; i < currentLines.Count - 1; i++)
            {
                var dateTo = GetToDate(currentLines[i], currentLines[i+1]);
                var dataQualityError = CheckDataQualityError(currentLines[i]);
                if(dataQualityError != null)
                {
                    timelineErrors.Add(dataQualityError.Value);
                }

                timelines.Add(new ConnectionTimelineDto
                {
                    AgreementId = Int32.Parse(currentLines[i].ExternalAgreementId),
                    IsConnectedToGarbagePickupSystem = IsConnectedToGarbagePickupSystem(currentLines[i].PlaceType),
                    IsConnectedToPublicContainer = IsPublicContainer(currentLines[i].PlaceType),
                    DateFrom = GetFromDate(currentLines[i]),
                    DateTo = dateTo,
                    UtilityUnitConnectionType = GetUtilitytype(currentLines[i].BuildingType),
                });
            }

            //Last interval
            var lastInterval = currentLines[^1];

            timelines.Add(new ConnectionTimelineDto
            {
                AgreementId = Int32.Parse(lastInterval.ExternalAgreementId),
                IsConnectedToGarbagePickupSystem = IsConnectedToGarbagePickupSystem(lastInterval.PlaceType),
                IsConnectedToPublicContainer = IsPublicContainer(lastInterval.PlaceType),
                DateFrom = lastInterval.FromDate.AddHours(12),
                DateTo = lastInterval.ToDate != null ? lastInterval.ToDate.Value.AddHours(12) : null,
                UtilityUnitConnectionType = GetUtilitytype(lastInterval.BuildingType),
            });

            utilityUnitTimelines.Add(new UtilityUnitConnectionUpdateDto
            {
                ConnectionsInTime = timelines
            });
        }

        return utilityUnitTimelines;
    }

    #region Private Helpers

    private (long, DateTime?, DateTime?)? CheckDataQualityError(AgreementPlaceConnectionLine currentLine)
    {
        if(currentLine.FromDate == currentLine.ToDate)
        {
            return (currentLine.AgreementId, currentLine.FromDate, currentLine.ToDate);
        }

        return null;
    }

    private DateTime GetFromDate(AgreementPlaceConnectionLine currentLine)
    {
        return currentLine.FromDate == currentLine.ToDate ?
            currentLine.FromDate.AddHours(-12) :
            currentLine.FromDate.AddHours(12);
    }

    private DateTime? GetToDate(AgreementPlaceConnectionLine currentLine, AgreementPlaceConnectionLine nextLine)
    {
        DateTime? dateTo = null;

        if(currentLine.ToDate != null)
        {
            dateTo = currentLine.ToDate.Value.AddHours(-12);
        }
        else
        {
            dateTo = nextLine.FromDate.AddHours(-12);
        }

        return dateTo;
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
