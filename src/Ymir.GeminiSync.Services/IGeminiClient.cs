using System.Text.Json.Serialization;
using Ymir.GeminiSync.Services.Models;

namespace Ymir.GeminiSync.Services;

public interface IGeminiClient
{
    //Garbage bin fractions
    Task<List<FractionInTime>> GetFractionsInTime(int garbageBinCollectionId);

    Task<bool> UpdateFractionsInTime(int garbageBinCollectionId, List<AgreementFractionTimeline> fractions);

    //Garbage bin collections
    Task<List<GarbageBinsCollectionDto>> GetGarbageBinCollection(int collectionId);

    Task<bool> UpdateGarbageBinCollection(GarbageBinsStateInTimeDto garbageBinsStates);

    //Garbage bin pickups


    //Private containers
    Task<List<GarbageBinsCollectionDto>> GetPrivateContainerGroupFractions(int privateContainerGroupId);

    Task<bool> UpdatePrivateContainerGroupFractions(GarbageBinsStateInTimeDto garbageBinsStates);

    //Public containers

}
