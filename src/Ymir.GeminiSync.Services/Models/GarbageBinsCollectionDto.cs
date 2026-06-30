using System.Text.Json.Serialization;

namespace Ymir.GeminiSync.Services.Models;

public class GarbageBinsCollectionDto
{
    public int GarbageBinCollectionId { get; set; }

    // Deprecated - Info is sent through utility units
    public int NumberOfConnectedUtilityUnit { get; set; }

    public List<GarbageBinDto> GarbageBins { get; set; } = new();

    public CompostType? CompostType { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public GarbageBinUtilityUnitType UtilityUnitType { get; set; }

    public DateTime InEffectFrom { get; set; }

    public DateTime? InEffectTo { get; set; }
}
