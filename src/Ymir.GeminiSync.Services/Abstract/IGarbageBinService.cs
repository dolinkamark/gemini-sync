using Ymir.GeminiSync.Domain;
using Ymir.GeminiSync.Services.Models;

namespace Ymir.GeminiSync.Services.Abstract;

public interface IGarbageBinService
{
    List<GarbageBinsStateInTimeDto> CreateGarbageBinsStateInTimeList(List<GarbageBinCollectionLine> collectionLines);

    List<StateInTimeCollection> CreateStateInTimeCollections(List<GarbageBinCollectionLine> lines);
}
