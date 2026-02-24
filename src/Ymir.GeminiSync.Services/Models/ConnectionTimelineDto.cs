namespace Ymir.GeminiSync.Services.Models;

public class ConnectionTimelineDto
{
    public int AgreementId { get; set; }

    public bool IsConnectedToGarbagePickupSystem { get; set; }

    public bool IsConnectedToPublicContainer { get; set; }

    public UtilityUnitConnectionType UtilityUnitConnectionType { get; set; }

    public DateTime DateFrom { get; set; }

    public DateTime? DateTo { get; set; }
}
