using Ymir.GeminiSync.Domain;
using Ymir.GeminiSync.Services.Models;

namespace Ymir.GeminiSync.Services.Abstract;

public interface IUtilityConnectionsService
{
    public List<(long agreementId, UtilityUnitConnectionUpdateDto updateDto)> CreateUtilityUnitTimelines(
        List<AgreementPlaceConnectionLine> connectionLines,
        List<AgreementExcemption> exemptions
    );

    bool AreTimelinesEqual(List<ConnectionTimelineDto> firstTimeline, List<ConnectionTimelineDto> secondTimeline);
}
