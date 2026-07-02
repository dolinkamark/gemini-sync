using Microsoft.Extensions.Options;
using Ymir.GeminiSync.Domain;
using Ymir.GeminiSync.Services.Abstract;
using Ymir.GeminiSync.Services.Models;

namespace Ymir.GeminiSync.Services;

public class UtilityConnectionsService(IOptions<UtilityConnectionsServiceOptions> options) : IUtilityConnectionsService
{
    public List<UtilityUnitConnectionUpdateDto> CreateUtilityUnitTimelines(List<AgreementPlaceConnectionLine> connectionLines)
    {
        var utilityUnitTimelines = new List<UtilityUnitConnectionUpdateDto>();

        //TODO: Add validation, shouldn't have empty or null ExternalAgreementId
        var agreementGroups = connectionLines
            .Where(l => !String.IsNullOrWhiteSpace(l.ExternalAgreementId))
            .GroupBy(l => l.AgreementId)
            .ToList();

        foreach (var agreementGroup in agreementGroups)
        {
            var timelines = new List<ConnectionTimelineDto>();
            var currentLines = agreementGroup
                .OrderBy(l => l.FromDate)
                .ToList();

            //Closed intervals
            for (int i = 0; i < currentLines.Count - 1; i++)
            {
                bool isCabin = !string.IsNullOrWhiteSpace(currentLines[i].BuildingType) && currentLines[i].BuildingType.StartsWith("16");

                timelines.Add(new ConnectionTimelineDto
                {
                    AgreementId = Int32.Parse(currentLines[i].ExternalAgreementId),
                    IsConnectedToGarbagePickupSystem = IsConnectedToGarbagePickupSystem(),
                    IsConnectedToPublicContainer = IsPublicContainer(currentLines[i].PlaceType),
                    DateFrom = currentLines[i].FromDate.AddHours(12),
                    DateTo = currentLines[i + 1].FromDate.AddHours(-12),
                    UtilityUnitConnectionType = GetUtilitytype(currentLines[i].BuildingType),
                });
            }

            //Last interval
            var lastInterval = currentLines[^1];

            timelines.Add(new ConnectionTimelineDto
            {
                AgreementId = Int32.Parse(lastInterval.ExternalAgreementId),
                IsConnectedToGarbagePickupSystem = IsConnectedToGarbagePickupSystem(),
                IsConnectedToPublicContainer = IsPublicContainer(lastInterval.PlaceType),
                DateFrom = lastInterval.FromDate.AddHours(12),
                DateTo = lastInterval.ToDate?.AddHours(12),
                UtilityUnitConnectionType = GetUtilitytype(lastInterval.BuildingType),
            });

            utilityUnitTimelines.Add(new UtilityUnitConnectionUpdateDto
            {
                ConnectionsInTime = timelines
            });
        }

        return utilityUnitTimelines;
    }

    private bool IsConnectedToGarbagePickupSystem()
    {
        return true;
    }

    private bool IsPublicContainer(string placeType)
    {
        return placeType.ToLower() == options.Value.PublicContainerName.ToLower();
    }

    private UtilityUnitConnectionType GetUtilitytype(string buildingType)
    {
        bool isCabin = !string.IsNullOrWhiteSpace(buildingType) && buildingType.StartsWith("16");
        return isCabin ? UtilityUnitConnectionType.Cabin : UtilityUnitConnectionType.Housing;
    }
}
