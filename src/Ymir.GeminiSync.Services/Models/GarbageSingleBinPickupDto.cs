namespace Ymir.GeminiSync.Services.Models;

public class GarbageSingleBinPickupDto
{
    public int GarbageBinId { get; set; }
    public int BinSize { get; set; }
    public GarbageBinCategory GarbageBinCategory { get; set; }
}
