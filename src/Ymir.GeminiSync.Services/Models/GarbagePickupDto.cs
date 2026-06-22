namespace Ymir.GeminiSync.Services.Models;

public class GarbagePickupDto
{
    public int GarbageBinCollectionId { get; set; }
    public int GarbageBinPickUpId { get; set; }

    public GarbageBinUtilityUnitType UtilityUnitType { get; set; }

    public DateTimeOffset ExecutedDate { get; set; }

    public int? NumberOfThreshold { get; set; }
    public int? NumberOfLockedDoors { get; set; }
    public int? NumberOfMeters { get; set; }
    public int? NumberOfRamps { get; set; }
    public int? NumberOfStaircaseSteps { get; set; }

    public bool ExtraPickup { get; set; }

    public List<GarbageSingleBinPickupDto> GarbageBins { get; set; }
}
