using System.Text.Json.Serialization;

namespace Ymir.GeminiSync.Services.Models;

public class GarbageBinsCollectionDto : IEquatable<GarbageBinsCollectionDto>
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

    public bool Equals(GarbageBinsCollectionDto other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return GarbageBinCollectionId == other.GarbageBinCollectionId
            && NumberOfConnectedUtilityUnit == other.NumberOfConnectedUtilityUnit
            && CompostType == other.CompostType
            && UtilityUnitType == other.UtilityUnitType
            && InEffectFrom == other.InEffectFrom
            && InEffectTo == other.InEffectTo
            && GarbageBins.SequenceEqual(other.GarbageBins);
    }

    public override bool Equals(object obj)
    {
        return obj is GarbageBinsCollectionDto other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();

        hash.Add(GarbageBinCollectionId);
        hash.Add(NumberOfConnectedUtilityUnit);
        hash.Add(CompostType);
        hash.Add(UtilityUnitType);
        hash.Add(InEffectFrom);
        hash.Add(InEffectTo);

        foreach (var garbageBin in GarbageBins)
        {
            hash.Add(garbageBin);
        }

        return hash.ToHashCode();
    }
}
