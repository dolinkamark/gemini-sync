namespace Ymir.GeminiSync.Services.Models.Containers;

public class PrivateContainerFractionsResponse
{
    public DateTimeOffset DateFrom { get; set; }

    public DateTimeOffset? DateTo { get; set; }

    public DateTimeOffset ModifiedAt { get; set; }

    public List<PrivateContainerAgreementsResponse> Agreements { get; set; }
}
