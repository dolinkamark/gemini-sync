using System.Text.Json;
using Microsoft.Extensions.Options;
using Ymir.GeminiSync.Domain;
using Ymir.GeminiSync.Domain.Repositories;

namespace Ymir.GeminiSync.Common;

public class SyncReportFileRepository : ISyncReportRepository
{
    private readonly string _filePath;

    public SyncReportFileRepository(IOptions<SyncReportOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _filePath = options.Value.FilePath;
    }

    public async Task SaveReport(SyncReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var directoryPath = Path.GetFullPath(_filePath);
        var directory = Path.GetDirectoryName(directoryPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Write to a temporary file first so the final report is not left
        // partially written if serialization fails.
        var tempPath = Path.Join(directoryPath, $"SyncReport.{Guid.NewGuid():N}.json");

        try
        {
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, report);
            }

            File.Move(tempPath, directoryPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
