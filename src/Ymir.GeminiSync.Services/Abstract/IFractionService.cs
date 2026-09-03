using Ymir.GeminiSync.Domain;
using Ymir.GeminiSync.Services.Models;

namespace Ymir.GeminiSync.Services.Abstract;

public interface IFractionService
{
    List<PlaceAgreementInterval> BuildFractionIntervalsByDate(List<AgreementPlaceHistoryLine> lines);

    List<(int, List<AgreementFractionTimeline>)> CreateFractionTimelines(List<PlaceAgreementInterval> intervals);

    bool AreFractionTimelinesEqual(List<AgreementFractionTimeline> first, List<AgreementFractionTimeline> second);

    List<(int, List<AgreementFractionTimeline>)> GetChangedTimelines(
        List<(int, List<AgreementFractionTimeline>)> currentTimelines,
        List<(int, List<AgreementFractionTimeline>)> previousTimelines);
}
