namespace Ymir.GeminiSync.Domain;

public class SyncReport
{
    public long Id { get; set; }

    public int UpdatedCount { get; set; }

    public List<SyncError> Errors { get; set; } = new List<SyncError>();
}
