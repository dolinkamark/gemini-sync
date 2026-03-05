namespace Ymir.GeminiSync.Services.Models;

public class ContainerPickupDto
{
    public int GarbagePrivateContainerPickupId { get; set; }

    public DateTimeOffset ExecutedDate { get; set; }

    public int GarbagePrivateContainerGroupId { get; set; }

    public GarbageBinCategory WasteType { get; set; }
}
