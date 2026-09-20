using Ymir.GeminiSync.Domain;
using Ymir.GeminiSync.Domain.Repositories;
using Ymir.GeminiSync.Services.Abstract;
using Ymir.GeminiSync.Services.Models;

namespace Ymir.GeminiSync.Services;

public class FractionsSyncService(
    IAgreementPlacesRepository agreementPlacesRepository,
    IFractionService fractionService,
    ISyncReportRepository reportRepository,
    IGeminiClient geminiClient) : IFractionsSyncService
{
    public async Task<SyncReport> SyncFractionsInTime(
        int customerId,
        string placeTypeDescription,
        List<AgreementPlaceHistoryLine> previousPlaceLines = null)
    {
        var syncReport = new SyncReport();

        //Step 1) Get things to sync
        var placeLines = await agreementPlacesRepository.GetFractionsHistory(customerId, placeTypeDescription);

        //Step 2) Build the dto list to send
        var timelines = BuildTimelines(placeLines);
        var totalCount = timelines.Count;

        if (previousPlaceLines != null)
        {
            timelines = fractionService.GetChangedTimelines(timelines, BuildTimelines(previousPlaceLines));
        }

        //Step 3) Sync changed parts
        var updatedCount = 0;

        foreach (var (placeNr, agreementTimelines) in timelines)
        {
            try
            {
                //Adjust hours to avoid dayshift by timezone
                foreach (var entry in agreementTimelines.SelectMany(t => t.FractionsInTime))
                {
                    entry.DateFrom = entry.DateFrom.AddHours(12);
                    entry.DateTo = entry.DateTo?.AddHours(12);
                }

                var isSuccessful = await geminiClient.UpdateFractionsInTime(placeNr, agreementTimelines);
                if (!isSuccessful)
                {
                    syncReport.Errors.Add(new SyncError
                    {
                        PlaceNr = placeNr,
                        Description = "Gemini client Fractions update call failed",
                    });
                }
                else
                {
                    updatedCount++;
                }
            }
            catch (Exception ex)
            {
                syncReport.Errors.Add(new SyncError
                {
                    PlaceNr = placeNr,
                    Description = ex.ToString(),
                });
            }
        }

        syncReport.TotalCount = totalCount;
        syncReport.UpdatedCount = updatedCount;

        //Step 4) Save report
        await reportRepository.SaveReport(syncReport);

        return syncReport;
    }

    /// <summary>
    /// Lines without an ExternalAgreementId cannot be mapped to a Gemini agreement, so they are dropped.
    /// </summary>
    private List<(int, List<AgreementFractionTimeline>)> BuildTimelines(List<AgreementPlaceHistoryLine> lines)
    {
        var mappableLines = (lines ?? new())
            .Where(l => !String.IsNullOrWhiteSpace(l.ExternalAgreementId))
            .ToList();

        return fractionService.CreateFractionTimelines(
            fractionService.BuildFractionIntervalsByDate(mappableLines));
    }
}
