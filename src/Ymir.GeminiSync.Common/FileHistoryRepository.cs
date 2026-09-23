using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Ymir.GeminiSync.Domain;
using Ymir.GeminiSync.Domain.Repositories;

namespace Ymir.GeminiSync.Common;

public class FileHistoryRepository : IHistoryRepository
{
    private const string GarbageBinsFolder = "GarbageBins";
    private const string FractionsFolder = "Fractions";
    private const string EmptyingsFolder = "Emptyings";
    private const string UtilityConnectionsFolder = "UtilityConnections";

    private const string UtilityConnectionsPrefix = "UtilityConnections";
    private const string ExemptionsPrefix = "Exemptions";

    private const string DateFormat = "yyyyMMdd";

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly string _rootDirectory;

    public FileHistoryRepository(IOptions<HistoryOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _rootDirectory = Path.GetFullPath(options.Value.Directory);
    }

    public Task<List<GarbageBinCollectionLine>> GetPreviousGarbageBinCollections(int customerId, string placeTypeDescription)
        => ReadLatest<GarbageBinCollectionLine>(GarbageBinsFolder, NormalizePlaceType(placeTypeDescription));

    public Task<List<AgreementPlaceConnectionLine>> GetPreviousAllUtilityUnitConnections(int customerId)
        => ReadLatest<AgreementPlaceConnectionLine>(UtilityConnectionsFolder, UtilityConnectionsPrefix);

    public Task<List<AgreementPlaceHistoryLine>> GetPreviousFractionsHistory(int customerId, string placeTypeDescription)
        => ReadLatest<AgreementPlaceHistoryLine>(FractionsFolder, NormalizePlaceType(placeTypeDescription));

    public Task<List<LoglineLine>> GetPreviousLoglineLines(int customerId, string placeTypeDescription)
        => ReadLatest<LoglineLine>(EmptyingsFolder, NormalizePlaceType(placeTypeDescription));

    public Task<List<AgreementExcemption>> GetPreviousAllAgreementExcemptions(int customerId)
        => ReadLatest<AgreementExcemption>(UtilityConnectionsFolder, ExemptionsPrefix);

    public Task SaveHistoricalData(int customerId, string placeTypeDescription, List<GarbageBinCollectionLine> garbageBinCollections)
        => Write(GarbageBinsFolder, NormalizePlaceType(placeTypeDescription), garbageBinCollections);

    public Task SaveHistoricalData(int customerId, string placeTypeDescription, List<AgreementPlaceHistoryLine> fractionsHistory)
        => Write(FractionsFolder, NormalizePlaceType(placeTypeDescription), fractionsHistory);

    public Task SaveHistoricalData(int customerId, List<AgreementPlaceConnectionLine> utilityUnitConnections)
        => Write(UtilityConnectionsFolder, UtilityConnectionsPrefix, utilityUnitConnections);

    public Task SaveHistoricalData(int customerId, List<AgreementExcemption> agreementExcemptions)
        => Write(UtilityConnectionsFolder, ExemptionsPrefix, agreementExcemptions);

    public Task SaveHistoricalData(int customerId, string placeTypeDescription, List<LoglineLine> loglineLines)
        => Write(EmptyingsFolder, NormalizePlaceType(placeTypeDescription), loglineLines);

    private async Task<List<T>> ReadLatest<T>(string folder, string prefix)
    {
        var directory = Path.Join(_rootDirectory, folder);
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var latestPath = FindLatestFile(directory, prefix);
        if (latestPath is null)
        {
            return [];
        }

        await using var stream = File.OpenRead(latestPath);
        var result = await JsonSerializer.DeserializeAsync<List<T>>(stream, ReadOptions);
        return result ?? [];
    }

    private static string? FindLatestFile(string directory, string prefix)
    {
        var fileNameRegex = BuildFileNameRegex(prefix, dateToken: null);

        return Directory.GetFiles(directory, "*.json")
            .Select(path =>
            {
                var match = fileNameRegex.Match(Path.GetFileName(path));
                if (!match.Success
                    || !DateTime.TryParseExact(
                        match.Groups[1].Value,
                        DateFormat,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var date))
                {
                    return (Path: (string?)null, Date: DateTime.MinValue, Increment: 0);
                }

                return (Path: path, Date: date, Increment: int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture));
            })
            .Where(file => file.Path is not null)
            .OrderByDescending(file => file.Date)
            .ThenByDescending(file => file.Increment)
            .Select(file => file.Path)
            .FirstOrDefault();
    }

    private async Task Write<T>(string folder, string prefix, IReadOnlyCollection<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var directory = Path.Join(_rootDirectory, folder);
        Directory.CreateDirectory(directory);

        var dateToken = DateTime.Now.ToString(DateFormat, CultureInfo.InvariantCulture);
        var increment = NextIncrement(directory, prefix, dateToken);
        var filePath = Path.Join(directory, $"{prefix}_{dateToken}_{increment:D2}.json");
        var json = JsonSerializer.Serialize(items);
        await File.WriteAllTextAsync(filePath, json);
    }

    private static int NextIncrement(string directory, string prefix, string dateToken)
    {
        var fileNameRegex = BuildFileNameRegex(prefix, dateToken);

        return Directory.GetFiles(directory, "*.json")
            .Select(path => fileNameRegex.Match(Path.GetFileName(path)))
            .Where(match => match.Success)
            .Select(match => int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture))
            .DefaultIfEmpty(0)
            .Max() + 1;
    }

    private static Regex BuildFileNameRegex(string prefix, string? dateToken)
    {
        var datePattern = dateToken is null ? "\\d{8}" : Regex.Escape(dateToken);
        var pattern = $"^{Regex.Escape(prefix)}_({datePattern})_(\\d{{2,}})\\.json$";

        return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string NormalizePlaceType(string placeTypeDescription)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(placeTypeDescription);

        return placeTypeDescription.Trim().Replace(" ", "_");
    }
}
