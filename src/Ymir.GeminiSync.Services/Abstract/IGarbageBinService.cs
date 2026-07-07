using Ymir.GeminiSync.Domain;
using Ymir.GeminiSync.Services.Models;

namespace Ymir.GeminiSync.Services.Abstract;

public interface IGarbageBinService
{
    List<GarbageBinsStateInTimeDto> CreateGarbageBinsStateInTimeList(List<GarbageBinCollectionLine> collectionLines, string placeType);

    List<StateInTimeCollection> CreateStateInTimeCollections(List<GarbageBinCollectionLine> lines);
}
