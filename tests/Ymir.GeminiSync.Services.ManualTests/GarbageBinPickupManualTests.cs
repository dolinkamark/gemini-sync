using Microsoft.Extensions.Options;
using NSubstitute;
using System.Text.Json;
using Ymir.GeminiSync.Common;
using Ymir.GeminiSync.Domain;
using Ymir.GeminiSync.Domain.Repositories;
using Ymir.GeminiSync.Services.Models;
using Ymir.GeminiSync.Services.Settings;

namespace Ymir.GeminiSync.Services.ManualTests;

public class ContainerPickupManualTests
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

    public ContainerPickupManualTests()
    {
        _httpClientFactory
            .CreateClient(Arg.Any<string>())
            .Returns(_ => new HttpClient());

        _syncReportOptions.Value.Returns(_reportOptions);
        _syncReportRepository = new SyncReportFileRepository(_syncReportOptions);
    }

    [Fact(Skip = "Manual test only")]
    public async Task SyncGarbageBinPickups()
    {
        //Arrange
        const string filePath = "E:\\Temp\\Ymir_Sync\\pickups\\logline_lines_Spann_20260827.json";

        const int customerId = 1;
        const string placeType = "Spann";

        var testGeminiClient = new GeminiClient(_settings, _httpClientFactory);

        var loglinesLines = await FileUtils.ReadFileContent<List<LoglineLine>>(filePath);

        //Act
        var syncReport = new SyncReport();

        var pickups = new List<GarbagePickupDto>();
        foreach (var logline in loglinesLines)
        {
            pickups.Add(new GarbagePickupDto
            {
                GarbageBinCollectionId = logline.PlaceNr,
                GarbageBinPickUpId = (int)logline.LogLineId,
                UtilityUnitType = GarbageBinUtilityUnitType.Housing,
                ExecutedDate = logline.Time.Value,
                GarbageBins = new List<GarbageSingleBinPickupDto>
                {
                    new GarbageSingleBinPickupDto
                    {
                        GarbageBinId = (int)logline.AgreementLineId,
                        BinSize = logline.ShortName.Value,
                        GarbageBinCategory = GarbageBinCategory.Bio
                    }
                }
            });
        }

        var updateCount = 0;
        foreach (var pickup in pickups)
        {
            try
            {
                var isSuccessful = await testGeminiClient.AddGarbageBinPickup(pickup);
                if (isSuccessful)
                {
                    updateCount++;
                }
                else
                {
                    syncReport.Errors.Add(new SyncError
                    {
                        AgreementId = pickup.GarbageBinPickUpId,
                        Description = $"Update failed for dto: {JsonSerializer.Serialize(pickup)}"
                    });
                }
            }
            catch (Exception ex)
            {
                syncReport.Errors.Add(new SyncError
                {
                    PlaceNr = pickup.GarbageBinCollectionId,
                    Description = ex.ToString()
                });
            }
        }

        syncReport.TotalCount = pickups.Count;
        syncReport.UpdatedCount = updateCount;

        //Assert
        Assert.Fail("Manual test only");
    }
}
