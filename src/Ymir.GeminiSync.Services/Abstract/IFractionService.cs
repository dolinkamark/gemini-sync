using Ymir.GeminiSync.Domain;
using Ymir.GeminiSync.Services.Models;

namespace Ymir.GeminiSync.Services.Abstract;

public interface IFractionService
{
    List<FractionInTime> CreateFractionsInTime(List<PlaceAgreementInterval> intervals);

    List<AgreementFractionTimeline> CreateFractionTimelines(List<FractionInTime> intervals);

    List<PlaceAgreementInterval> BuildFractionIntervalsByDate(List<AgreementPlaceHistoryLine> lines);
}
