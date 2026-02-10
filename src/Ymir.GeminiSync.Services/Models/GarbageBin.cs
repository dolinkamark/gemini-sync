using System.Text.Json.Serialization;

namespace Ymir.GeminiSync.Services.Models;

public class GarbageBinDto
{
    public int GarbageBinId { get ; set; }

    public int BinSize { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public GarbageBinCategory GarbageBinCategory { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public GarbageBinsFrequencyToBeInvoiced FrequencyToBeInvoiced { get; set; }

    public bool IsLockable { get; set; }

    public bool IsCompactor { get; set; }
}
