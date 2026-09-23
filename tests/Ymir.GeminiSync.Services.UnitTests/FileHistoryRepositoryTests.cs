using System.Text.Json;
using Microsoft.Extensions.Options;
using Ymir.GeminiSync.Common;
using Ymir.GeminiSync.Domain;

namespace Ymir.GeminiSync.Services.UnitTests;

public class FileHistoryRepositoryTests : IDisposable
{
    private const int CustomerId = 1;
    private const string PlaceType = "Spann";
    private const string OtherPlaceType = "Nedgravd privat";

    private readonly string _directory;
    private readonly FileHistoryRepository _sut;

    public FileHistoryRepositoryTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "YmirHistoryTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _sut = new FileHistoryRepository(Options.Create(new HistoryOptions { Directory = _directory }));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public async Task SaveThenGetPrevious_RoundTripsGarbageBinCollections()
    {
        var collections = new List<GarbageBinCollectionLine>
        {
            new() { CustomerId = CustomerId, PlaceNr = 100, Bid = "BIN-1" }
        };

        await _sut.SaveHistoricalData(CustomerId, PlaceType, collections);

        var previous = await _sut.GetPreviousGarbageBinCollections(CustomerId, PlaceType);

        var previousLine = Assert.Single(previous);
        Assert.Equal(100, previousLine.PlaceNr);
        Assert.Equal("BIN-1", previousLine.Bid);
        Assert.True(File.Exists(Path.Combine(_directory, "GarbageBins", $"{PlaceType}_{DateTime.Now:yyyyMMdd}_01.json")));
    }

    [Fact]
    public async Task GetPrevious_WhenNoFilesExist_ReturnsEmptyLists()
    {
        Assert.Empty(await _sut.GetPreviousGarbageBinCollections(CustomerId, PlaceType));
        Assert.Empty(await _sut.GetPreviousAllUtilityUnitConnections(CustomerId));
        Assert.Empty(await _sut.GetPreviousFractionsHistory(CustomerId, PlaceType));
        Assert.Empty(await _sut.GetPreviousLoglineLines(CustomerId, PlaceType));
        Assert.Empty(await _sut.GetPreviousAllAgreementExcemptions(CustomerId));
    }

    [Fact]
    public async Task GetPrevious_WhenDirectoryDoesNotExist_ReturnsEmptyList()
    {
        Directory.Delete(_directory, recursive: true);

        var previous = await _sut.GetPreviousGarbageBinCollections(CustomerId, PlaceType);

        Assert.Empty(previous);
    }

    [Fact]
    public async Task GetPrevious_SelectsMatchingPlaceTypeAndSyncTypeFolder()
    {
        await WriteJson(
            "GarbageBins",
            $"{PlaceType}_20260101_01.json",
            new List<GarbageBinCollectionLine> { new() { PlaceNr = 1, Bid = "spann" } });
        await WriteJson(
            "GarbageBins",
            "Nedgravd_privat_20260101_01.json",
            new List<GarbageBinCollectionLine> { new() { PlaceNr = 2, Bid = "nedgravd" } });
        await WriteJson(
            "Fractions",
            $"{PlaceType}_20260101_01.json",
            new List<AgreementPlaceHistoryLine> { new() { PlaceNr = 3, ExternalAgreementId = "frac" } });

        var spannBins = await _sut.GetPreviousGarbageBinCollections(CustomerId, PlaceType);
        var nedgravdBins = await _sut.GetPreviousGarbageBinCollections(CustomerId, OtherPlaceType);
        var fractions = await _sut.GetPreviousFractionsHistory(CustomerId, PlaceType);

        Assert.Equal("spann", Assert.Single(spannBins).Bid);
        Assert.Equal("nedgravd", Assert.Single(nedgravdBins).Bid);
        Assert.Equal("frac", Assert.Single(fractions).ExternalAgreementId);
    }

    [Fact]
    public async Task GetPrevious_PicksLatestDateThenHighestIncrement()
    {
        await WriteJson(
            "GarbageBins",
            $"{PlaceType}_20260101_05.json",
            new List<GarbageBinCollectionLine> { new() { Bid = "older" } });
        await WriteJson(
            "GarbageBins",
            $"{PlaceType}_20260315_02.json",
            new List<GarbageBinCollectionLine> { new() { Bid = "latest" } });
        await WriteJson(
            "GarbageBins",
            $"{PlaceType}_20260315_01.json",
            new List<GarbageBinCollectionLine> { new() { Bid = "earlier-same-day" } });
        await WriteJson(
            "GarbageBins",
            $"{PlaceType}_20260201_01.json",
            new List<GarbageBinCollectionLine> { new() { Bid = "middle" } });

        var previous = await _sut.GetPreviousGarbageBinCollections(CustomerId, PlaceType);

        Assert.Equal("latest", Assert.Single(previous).Bid);
    }

    [Fact]
    public async Task SaveHistoricalData_SameDayWritesIncrementedFiles()
    {
        await _sut.SaveHistoricalData(CustomerId, PlaceType, new List<GarbageBinCollectionLine> { new() { Bid = "first" } });
        await _sut.SaveHistoricalData(CustomerId, PlaceType, new List<GarbageBinCollectionLine> { new() { Bid = "second" } });

        var date = DateTime.Now.ToString("yyyyMMdd");
        Assert.True(File.Exists(Path.Combine(_directory, "GarbageBins", $"{PlaceType}_{date}_01.json")));
        Assert.True(File.Exists(Path.Combine(_directory, "GarbageBins", $"{PlaceType}_{date}_02.json")));
        Assert.Equal("second", Assert.Single(await _sut.GetPreviousGarbageBinCollections(CustomerId, PlaceType)).Bid);
    }

    [Fact]
    public async Task SaveHistoricalData_WritesConnectionsAndExemptionsToUtilityConnectionsFolder()
    {
        await _sut.SaveHistoricalData(CustomerId, [new AgreementPlaceConnectionLine { Bid = "conn" }]);
        await _sut.SaveHistoricalData(CustomerId, [new AgreementExcemption { AgreementId = 42 }]);
        await _sut.SaveHistoricalData(CustomerId, PlaceType, new List<GarbageBinCollectionLine> { new() { Bid = "bin" } });

        var date = DateTime.Now.ToString("yyyyMMdd");
        Assert.True(File.Exists(Path.Combine(_directory, "UtilityConnections", $"UtilityConnections_{date}_01.json")));
        Assert.True(File.Exists(Path.Combine(_directory, "UtilityConnections", $"Exemptions_{date}_01.json")));
        Assert.True(File.Exists(Path.Combine(_directory, "GarbageBins", $"{PlaceType}_{date}_01.json")));
        Assert.Equal("conn", Assert.Single(await _sut.GetPreviousAllUtilityUnitConnections(CustomerId)).Bid);
        Assert.Equal(42, Assert.Single(await _sut.GetPreviousAllAgreementExcemptions(CustomerId)).AgreementId);
        Assert.Equal("bin", Assert.Single(await _sut.GetPreviousGarbageBinCollections(CustomerId, PlaceType)).Bid);
    }

    [Fact]
    public async Task SaveHistoricalData_DoesNotOverwriteOtherCollectionTypes()
    {
        await _sut.SaveHistoricalData(
            CustomerId,
            PlaceType,
            new List<AgreementPlaceHistoryLine> { new() { ExternalAgreementId = "frac" } });
        await _sut.SaveHistoricalData(
            CustomerId,
            PlaceType,
            new List<GarbageBinCollectionLine> { new() { Bid = "bin" } });

        Assert.Equal("frac", Assert.Single(await _sut.GetPreviousFractionsHistory(CustomerId, PlaceType)).ExternalAgreementId);
        Assert.Equal("bin", Assert.Single(await _sut.GetPreviousGarbageBinCollections(CustomerId, PlaceType)).Bid);
    }

    [Fact]
    public async Task SaveHistoricalData_WritesPlaceScopedLoglineFile()
    {
        await _sut.SaveHistoricalData(
            CustomerId,
            OtherPlaceType,
            new List<LoglineLine> { new() { LogLineId = 9, PlaceNr = 50 } });

        var previous = await _sut.GetPreviousLoglineLines(CustomerId, OtherPlaceType);

        Assert.Equal(9, Assert.Single(previous).LogLineId);
        Assert.True(File.Exists(Path.Combine(_directory, "Emptyings", $"Nedgravd_privat_{DateTime.Now:yyyyMMdd}_01.json")));
    }

    private async Task WriteJson<T>(string folder, string fileName, T value)
    {
        Directory.CreateDirectory(Path.Combine(_directory, folder));
        await File.WriteAllTextAsync(Path.Combine(_directory, folder, fileName), JsonSerializer.Serialize(value));
    }
}
