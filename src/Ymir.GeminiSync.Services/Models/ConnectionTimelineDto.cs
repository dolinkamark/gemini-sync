namespace Ymir.GeminiSync.Services.Models;

public class ConnectionTimelineDto : IEquatable<ConnectionTimelineDto>
{
    public int AgreementId { get; set; }

    public bool IsConnectedToGarbagePickupSystem { get; set; }

    public bool IsConnectedToPublicContainer { get; set; }

    public int? IncludedUtilityUnitsCount { get; set; }

    public UtilityUnitConnectionType UtilityUnitConnectionType { get; set; }

    public CompostType? CompostType { get; set; }

    public DateTime DateFrom { get; set; }

    public DateTime? DateTo { get; set; }

    public bool Equals(ConnectionTimelineDto? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return AgreementId == other.AgreementId
            && IsConnectedToGarbagePickupSystem == other.IsConnectedToGarbagePickupSystem
            && IsConnectedToPublicContainer == other.IsConnectedToPublicContainer
            && IncludedUtilityUnitsCount == other.IncludedUtilityUnitsCount
            && UtilityUnitConnectionType == other.UtilityUnitConnectionType
            && CompostType == other.CompostType
            && DateFrom.Date == other.DateFrom.Date
            && DateTo?.Date == other.DateTo?.Date;
    }

    public override bool Equals(object? obj)
    {
        return obj is ConnectionTimelineDto other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(
            AgreementId,
            IsConnectedToGarbagePickupSystem,
            IsConnectedToPublicContainer,
            IncludedUtilityUnitsCount,
            UtilityUnitConnectionType,
            CompostType,
            DateFrom,
            DateTo
        );
    }
}
