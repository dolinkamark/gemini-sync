namespace Ymir.GeminiSync.Domain.Repositories;

public interface ISyncReportRepository
{
    Task SaveReport(SyncReport report);
}
