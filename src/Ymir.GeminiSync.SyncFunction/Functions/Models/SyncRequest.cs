namespace Ymir.GeminiSync.SyncFunction.Functions.Models;

public record SyncRequest(IReadOnlyList<string>? SyncedEntities);
