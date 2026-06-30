using System.Text.Json;

namespace Ymir.GeminiSync.Services;

public static class FileUtils
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static async Task<T> ReadFileContent<T>(string filePath) where T: class
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path is required.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("JSON file not found.", filePath);

        await using var stream = File.OpenRead(filePath);
        var deserializedValue = await JsonSerializer.DeserializeAsync<T>(stream, Options);
        return deserializedValue ?? default;
    }
}
