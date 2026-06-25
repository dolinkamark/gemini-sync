using Ymir.GeminiSync.Domain;
using Ymir.GeminiSync.Services.Models;

namespace Ymir.GeminiSync.Services.Abstract;

public interface IGarbageBinCollectionBuilder
{
    List<StateInTimeCollection> BuildStateInTimeCollections(List<GarbageBinCollectionLine> lines);
}
