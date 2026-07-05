using Ymir.GeminiSync.Domain;
using Ymir.GeminiSync.Services.Models;

namespace Ymir.GeminiSync.Services.Abstract;

public interface IUtilityConnectionsService
{
    public List<UtilityUnitConnectionUpdateDto> CreateUtilityUnitTimelines(
        List<AgreementPlaceConnectionLine> connectionLines,
        List<AgreementExcemption> exemptions
    );
}
