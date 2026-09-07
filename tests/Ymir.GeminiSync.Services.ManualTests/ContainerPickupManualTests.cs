using Microsoft.Extensions.Options;
using NSubstitute;
using System.Text.Json;
using Ymir.GeminiSync.Common;
using Ymir.GeminiSync.Domain;
using Ymir.GeminiSync.Domain.Repositories;
using Ymir.GeminiSync.Services.Models;
using Ymir.GeminiSync.Services.Settings;

namespace Ymir.GeminiSync.Services.ManualTests;

public class GarbageBinPickupManualTests
{
    private readonly IHttpClientFactory _httpClientFactory = Substitute.For<IHttpClientFactory>();
    private readonly IOptions<SyncReportOptions> _syncReportOptions = Substitute.For<IOptions<SyncReportOptions>>();

    private readonly ISyncReportRepository _syncReportRepository;

    private readonly GeminiSettings _settings = new GeminiSettings
    {
        BaseUrl = "https://pfpublicapi.geminisuite.com/public",
        MunicipalityNo = "stavanger",
        SubscriptionKey = "f714fceb470744ffa6017cfb050ffcbb"
    };

    private readonly SyncReportOptions _reportOptions = new SyncReportOptions
    {
        FilePath = "E:\\Temp\\Ymir"
    };

    public GarbageBinPickupManualTests()
    {
        _httpClientFactory
            .CreateClient(Arg.Any<string>())
            .Returns(_ => new HttpClient());

        _syncReportOptions.Value.Returns(_reportOptions);
        _syncReportRepository = new SyncReportFileRepository(_syncReportOptions);
    }

    [Fact(Skip = "Manual test only")]
    public async Task SyncContainerPickups()
    {
        //Arrange
        const string previousFilePath = "E:\\Temp\\Ymir_Sync\\pickups_20260827\\logline_lines_Nedgravd_privat_20260827.json";
        const string filePath = "E:\\Temp\\Ymir_Sync\\sync_20260902\\logline_lines_Nedgravd_privat_20260902.json";

        const int testCustomerId = 1;
        const string placeType = "Nedgravd privat";

        var invoiceStartTime = new DateTime(2026, 7, 1);

        var testGeminiClient = new GeminiClient(_settings, _httpClientFactory);

        var previousLoglines = await FileUtils.ReadFileContent<List<LoglineLine>>(previousFilePath);
        var loglineLines = await FileUtils.ReadFileContent<List<LoglineLine>>(filePath);

        var loglineFractions = loglineLines
            .GroupBy(l => l.FractionName)
            .Select(l => l.Key)
            .ToList();

        var newLoglines = loglineLines
            .Where(l => l.Time >= invoiceStartTime
                   && !previousLoglines.Any(pl => l.LogLineId == pl.LogLineId))
            .ToList();

        //Act
        var syncReport = new SyncReport();
        var pickups = new List<ContainerPickupDto>();

        foreach (var logline in newLoglines)
        {
            pickups.Add(new ContainerPickupDto
            {
                GarbagePrivateContainerPickupId = (int)logline.LogLineId,
                ExecutedDate = logline.Time.Value,
                GarbagePrivateContainerGroupId = (int)logline.PlaceNr,
                WasteType = ToGarbageBinCategory(logline.FractionName)
            });
        }

        var updateCount = 0;
        foreach (var pickup in pickups)
        {
            try
            {
                var isSuccesful = await testGeminiClient.AddPrivateContainerPickup(pickup);
                if(isSuccesful)
                {
                    updateCount++;
                }
                else
                {
                    syncReport.Errors.Add(new SyncError
                    {
                        PlaceNr = pickup.GarbagePrivateContainerGroupId,
                        Description = $"Update failed for dto: {JsonSerializer.Serialize(pickup)}"
                    });
                }
            }
            catch(Exception ex)
            {
                syncReport.Errors.Add(new SyncError
                {
                    PlaceNr = pickup.GarbagePrivateContainerGroupId,
                    Description = ex.ToString()
                });
            }
        }

        syncReport.TotalCount = pickups.Count;
        syncReport.UpdatedCount = updateCount;

        //Assert
        Assert.Fail("Manual test only");
    }

    [Fact(Skip = "Manual test only")]
    public async Task DeleteWrongPickups()
    {
        //Arrange
        const string previousFilePath = "E:\\Temp\\Ymir_Sync\\pickups_20260827\\logline_lines_Nedgravd_privat_20260827.json";
        const string filePath = "E:\\Temp\\Ymir_Sync\\sync_20260902\\logline_lines_Nedgravd_privat_20260902.json";

        const int testCustomerId = 1;
        const string placeType = "Nedgravd privat";

        var testGeminiClient = new GeminiClient(_settings, _httpClientFactory);

        var previousLoglines = await FileUtils.ReadFileContent<List<LoglineLine>>(previousFilePath);
        var loglineLines = await FileUtils.ReadFileContent<List<LoglineLine>>(filePath);

        var loglineFractions = loglineLines
            .GroupBy(l => l.FractionName)
            .Select(l => l.Key)
            .ToList();

        var loglinesToRemove = previousLoglines
            .Where(pl => !loglineLines.Any(l => l.LogLineId == pl.LogLineId))
            .ToList();

        //Act
        var syncReport = new SyncReport();

        var updateCount = 0;
        var checkedCount = 0;

        foreach (var logline in loglinesToRemove)
        {
            checkedCount++;

            try
            {
                var currentPickups = await testGeminiClient.GetPrivateContainerPickups(logline.PlaceNr);

                if(currentPickups.Any(l => l.Id == logline.LogLineId))
                {
                    var isSuccesful = await testGeminiClient.DeletePrivateContainerPickup(logline.PlaceNr, (int)logline.LogLineId);
                    if (isSuccesful)
                    {
                        updateCount++;
                    }
                    else
                    {
                        syncReport.Errors.Add(new SyncError
                        {
                            PlaceNr = (int)logline.LogLineId,
                            Description = $"Delete failed for logline id: {(int)logline.LogLineId}"
                        });
                    }

                    await Task.Delay(3000);
                }
            }
            catch (Exception ex)
            {
                syncReport.Errors.Add(new SyncError
                {
                    PlaceNr = (int)logline.LogLineId,
                    Description = ex.ToString()
                });
            }
        }

        syncReport.TotalCount = loglineLines.Count;
        syncReport.UpdatedCount = updateCount;

        //Assert
        Assert.Fail("Manual test only");
    }

    private GarbageBinCategory ToGarbageBinCategory(string fractionName)
    {
        return fractionName?.Trim().ToLowerInvariant() switch
        {
            "papp/papir" => GarbageBinCategory.Paper,

            "mat" or
            "bio" or
            "hage" => GarbageBinCategory.Bio,

            "plast" or
            "tøy" or
            "glass" => GarbageBinCategory.GlassAndMetal,

            "restavfall" or
            "restavfall vask" => GarbageBinCategory.OtherWaste,

            null or "" => throw new ArgumentException(
                "Fraction name cannot be empty.",
                nameof(fractionName)),

            _ => throw new ArgumentOutOfRangeException(
                nameof(fractionName),
                fractionName,
                "Unknown garbage fraction.")
        };
    }
}
