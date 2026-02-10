using System.Text.Json;
using Ymir.GeminiSync.Domain;

namespace Ymir.GeminiSync.Services;

public static class FileUtils
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static List<GarbageBinCollectionLine> ReadList(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path is required.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("JSON file not found.", filePath);

        var json = File.ReadAllText(filePath);

        var items = JsonSerializer.Deserialize<List<GarbageBinCollectionLine>>(json, Options);
        return items ?? new List<GarbageBinCollectionLine>();
    }

    public static async Task<List<GarbageBinCollectionLine>> ReadGarbageBinListAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path is required.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("JSON file not found.", filePath);

        await using var stream = File.OpenRead(filePath);
        var items = await JsonSerializer.DeserializeAsync<List<GarbageBinCollectionLine>>(stream, Options);
        return items ?? new List<GarbageBinCollectionLine>();
    }

    public static async Task<List<AgreementPlaceHistoryLine>> GetAgreementPlaceHistoryLines(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path is required.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("JSON file not found.", filePath);

        await using var stream = File.OpenRead(filePath);
        var items = await JsonSerializer.DeserializeAsync<List<AgreementPlaceHistoryLine>>(stream, Options);
        return items ?? new List<AgreementPlaceHistoryLine>();
    }
}
