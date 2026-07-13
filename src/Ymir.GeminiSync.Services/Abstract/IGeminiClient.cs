using Ymir.GeminiSync.Services.Models;
using Ymir.GeminiSync.Services.Models.Containers;

namespace Ymir.GeminiSync.Services.Abstract;

public interface IGeminiClient
{
    //Garbage bin fractions
    Task<List<FractionInTime>> GetFractionsInTime(int garbageBinCollectionId);

    Task<bool> UpdateFractionsInTime(int garbageBinCollectionId, List<AgreementFractionTimeline> fractions);

    //Garbage bin collections
    Task<List<GarbageBinsCollectionDto>> GetGarbageBinCollection(int collectionId);

    Task<bool> UpdateGarbageBinCollection(GarbageBinsStateInTimeDto garbageBinsStates);

    //Private containers
    Task<List<PrivateContainerFractionsResponse>> GetPrivateContainerGroupFractions(int privateContainerGroupId);

    Task<bool> UpdatePrivateContainerGroupFractions(
        int privateContainerGroupId, List<PrivateContainerGroupAgreementFractions> agreementFractions);

    //Garbage bin pickups
    Task<List<GarbagePickupDto>> GetGarbageBinPickups(int garbageCollectionId);

    Task<bool> AddGarbageBinPickup(GarbagePickupDto pickupDto);

    Task<bool> DeleteGarbageBinPickup(int garbageCollectionId, int pickupId);

    //Utility connection
    Task<List<ConnectionTimelineDto>> GetUtilityConnectionTimeline(long agreementId);

    Task<bool> UpdateUtilityConnectionTimeline(long agreementId, UtilityUnitConnectionUpdateDto updateDto);
}
