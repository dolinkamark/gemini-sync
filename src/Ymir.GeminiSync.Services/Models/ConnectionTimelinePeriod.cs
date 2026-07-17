using Ymir.GeminiSync.Domain;

namespace Ymir.GeminiSync.Services.Models;

public class ConnectionTimelinePeriod
{
    public DateTime StartDate { get; set; }

    public DateTime? ToDate { get; set; }

    public List<AgreementPlaceConnectionLine> Connections { get; set; } = new();
}
