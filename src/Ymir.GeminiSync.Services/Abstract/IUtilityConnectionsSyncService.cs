using Ymir.GeminiSync.Domain;

namespace Ymir.GeminiSync.Services.Abstract;

public interface IUtilityConnectionsSyncService
{
    Task<SyncReport> SyncUtilityUnitConnections(int customerId, bool checkDifference = false);

    Task<SyncReport> SyncUtilityUnitConnectionsByPlace(int customerId, string placeTypeDescription, bool checkDifference = false);
}
