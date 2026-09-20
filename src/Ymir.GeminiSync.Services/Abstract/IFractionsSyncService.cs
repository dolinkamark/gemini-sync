using Ymir.GeminiSync.Domain;

namespace Ymir.GeminiSync.Services.Abstract;

public interface IFractionsSyncService
{
    Task<SyncReport> SyncFractionsInTime(int customerId, string placeTypeDescription);
}
