using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Ymir.GeminiSync.Domain;
using Ymir.GeminiSync.Domain.Repositories;

namespace Ymir.GeminiSync.Common;

public class FileHistoryRepository : IHistoryRepository
{
    private const string GarbageBinCollectionsType = "GarbageBinCollections";
    private const string UtilityUnitConnectionsType = "UtilityUnitConnections";
    private const string FractionsHistoryType = "FractionsHistory";
    private const string LoglineLinesType = "LoglineLines";
    private const string AgreementExcemptionsType = "AgreementExcemptions";

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly string _directory;

    public FileHistoryRepository(IOptions<HistoryOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _directory = Path.GetFullPath(options.Value.Directory);
    }

    public Task<List<GarbageBinCollectionLine>> GetPreviousGarbageBinCollections(int customerId, string placeTypeDescription)
        => ReadLatest<GarbageBinCollectionLine>(GarbageBinCollectionsType, placeTypeDescription);

    public Task<List<AgreementPlaceConnectionLine>> GetPreviousAllUtilityUnitConnections(int customerId)
        => ReadLatest<AgreementPlaceConnectionLine>(UtilityUnitConnectionsType, placeTypeDescription: null);

    public Task<List<AgreementPlaceConnectionLine>> GetPreviousUtilityUnitConnections(int customerId, string placeTypeDescription)
        => ReadLatest<AgreementPlaceConnectionLine>(UtilityUnitConnectionsType, placeTypeDescription);

    public Task<List<AgreementPlaceHistoryLine>> GetPreviousFractionsHistory(int customerId, string placeTypeDescription)
        => ReadLatest<AgreementPlaceHistoryLine>(FractionsHistoryType, placeTypeDescription);

    public Task<List<LoglineLine>> GetPreviousLoglineLines(int customerId, string placeTypeDescription)
        => ReadLatest<LoglineLine>(LoglineLinesType, placeTypeDescription);

    public Task<List<AgreementExcemption>> GetPreviousAllAgreementExcemptions(int customerId)
        => ReadLatest<AgreementExcemption>(AgreementExcemptionsType, placeTypeDescription: null);

    public Task SaveHistoricalData(int customerId, string placeTypeDescription, List<GarbageBinCollectionLine> garbageBinCollections)
        => Write(GarbageBinCollectionsType, placeTypeDescription, garbageBinCollections);

    public Task SaveHistoricalData(int customerId, string placeTypeDescription, List<AgreementPlaceHistoryLine> fractionsHistory)
        => Write(FractionsHistoryType, placeTypeDescription, fractionsHistory);

    public Task SaveHistoricalData(int customerId, List<AgreementPlaceConnectionLine> utilityUnitConnections)
        => Write(UtilityUnitConnectionsType, placeTypeDescription: null, utilityUnitConnections);

    public Task SaveHistoricalData(int customerId, string placeTypeDescription, List<AgreementPlaceConnectionLine> utilityUnitConnections)
        => Write(UtilityUnitConnectionsType, placeTypeDescription, utilityUnitConnections);

    public Task SaveHistoricalData(int customerId, List<AgreementExcemption> agreementExcemptions)
        => Write(AgreementExcemptionsType, placeTypeDescription: null, agreementExcemptions);

    public Task SaveHistoricalData(int customerId, string placeTypeDescription, List<LoglineLine> loglineLines)
        => Write(LoglineLinesType, placeTypeDescription, loglineLines);

    private async Task<List<T>> ReadLatest<T>(string type, string? placeTypeDescription)
    {
        if (!Directory.Exists(_directory))
        {
            return [];
        }

        var latestPath = FindLatestFile(type, placeTypeDescription);
        if (latestPath is null)
        {
            return [];
        }

        await using var stream = File.OpenRead(latestPath);
        var result = await JsonSerializer.DeserializeAsync<List<T>>(stream, ReadOptions);
        return result ?? [];
    }

    private string? FindLatestFile(string type, string? placeTypeDescription)
    {
        var fileNameRegex = BuildFileNameRegex(type, placeTypeDescription);

        return Directory.GetFiles(_directory, "*.json")
            .Select(path =>
            {
                var match = fileNameRegex.Match(Path.GetFileName(path));
                if (!match.Success
                    || !DateTime.TryParseExact(
                        match.Groups[1].Value,
                        "yyyyMMdd",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var date))
                {
                    return (Path: (string?)null, Date: DateTime.MinValue);
                }

                return (Path: path, Date: date);
            })
            .Where(file => file.Path is not null)
            .OrderByDescending(file => file.Date)
            .Select(file => file.Path)
            .FirstOrDefault();
    }

    private async Task Write<T>(string type, string? placeTypeDescription, IReadOnlyCollection<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        Directory.CreateDirectory(_directory);

        var fileName = BuildFileName(type, placeTypeDescription, DateTime.Now);
        var filePath = Path.Join(_directory, fileName);
        var json = JsonSerializer.Serialize(items);
        await File.WriteAllTextAsync(filePath, json);
    }

    private static string BuildFileName(string type, string? placeTypeDescription, DateTime date)
    {
        var dateToken = date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var placeToken = NormalizePlaceType(placeTypeDescription);
        if (string.IsNullOrEmpty(placeToken))
        {
            return $"{type}_{dateToken}.json";
        }

        return $"{type}_{placeToken}_{dateToken}.json";
    }

    private static Regex BuildFileNameRegex(string type, string? placeTypeDescription)
    {
        var placeToken = NormalizePlaceType(placeTypeDescription);
        var pattern = string.IsNullOrEmpty(placeToken)
            ? $"^{Regex.Escape(type)}_(\\d{{8}})\\.json$"
            : $"^{Regex.Escape(type)}_{Regex.Escape(placeToken)}_(\\d{{8}})\\.json$";

        return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string NormalizePlaceType(string? placeTypeDescription)
    {
        if (string.IsNullOrWhiteSpace(placeTypeDescription))
        {
            return string.Empty;
        }

        return placeTypeDescription.Trim().Replace(" ", "_");
    }
}
