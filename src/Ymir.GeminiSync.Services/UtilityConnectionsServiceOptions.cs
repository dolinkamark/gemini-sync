using Ymir.GeminiSync.Services.Models;

namespace Ymir.GeminiSync.Services;

public class UtilityConnectionsServiceOptions
{
    public List<string> PublicContainerNames { get; set; }

    public List<string> NotConnectedToPickupSystem { get; set; }

    public List<ExemptionMap> ExemptionMaps { get; set; }
}
