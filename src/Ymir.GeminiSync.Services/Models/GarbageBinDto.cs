using System.Text.Json.Serialization;

namespace Ymir.GeminiSync.Services.Models;

public class GarbageBinDto : IEquatable<GarbageBinDto>
{
    public int GarbageBinId { get ; set; }

    public int BinSize { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public GarbageBinCategory GarbageBinCategory { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public GarbageBinsFrequencyToBeInvoiced FrequencyToBeInvoiced { get; set; }

    public bool IsLockable { get; set; }

    public bool IsCompactor { get; set; }

    public bool IsPlasticBag { get; set; }

    public bool Equals(GarbageBinDto other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return GarbageBinId == other.GarbageBinId
            && BinSize == other.BinSize
            && GarbageBinCategory == other.GarbageBinCategory
            && FrequencyToBeInvoiced == other.FrequencyToBeInvoiced
            && IsLockable == other.IsLockable
            && IsCompactor == other.IsCompactor
            && IsPlasticBag == other.IsPlasticBag;
    }

    public override bool Equals(object obj)
    {
        return obj is GarbageBinDto other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(
            GarbageBinId,
            BinSize,
            GarbageBinCategory,
            FrequencyToBeInvoiced,
            IsLockable,
            IsCompactor,
            IsPlasticBag);
    }
}
