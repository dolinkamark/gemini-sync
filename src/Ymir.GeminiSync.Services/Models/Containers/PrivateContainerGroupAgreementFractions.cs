namespace Ymir.GeminiSync.Services.Models.Containers;

public class PrivateContainerGroupAgreementFractions
{
    public long AgreementId { get; set; }

    public List<PrivateContainerGroupFractionInTime> FractionsInTime { get; set; } = new();
}
