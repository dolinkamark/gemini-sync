namespace Ymir.GeminiSync.Services.Models;

public class ContainerPickupResponseDto
{
    public int Id { get; set; }

    public DateTimeOffset ExecutionDate { get; set; }

    public int GarbagePrivateContainerGroupId { get; set; }

    public GarbageBinCategory WasteType { get; set; }
}
