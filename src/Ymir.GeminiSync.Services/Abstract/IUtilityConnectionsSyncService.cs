using Ymir.GeminiSync.Domain;

namespace Ymir.GeminiSync.Services.Abstract;

public interface IUtilityConnectionsSyncService
{
    Task<SyncReport> SyncUtilityUnitConnections(int customerId, string placeTypeDescription, bool checkDifference = false);
}
