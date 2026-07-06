using Ymir.GeminiSync.Domain;
using Ymir.GeminiSync.Services.Models;

namespace Ymir.GeminiSync.Services.Abstract;

public interface IFractionService
{
    List<PlaceAgreementInterval> BuildFractionIntervalsByDate(List<AgreementPlaceHistoryLine> lines);

    List<(int, List<AgreementFractionTimeline>)> CreateFractionTimelines(List<PlaceAgreementInterval> intervals);
}
