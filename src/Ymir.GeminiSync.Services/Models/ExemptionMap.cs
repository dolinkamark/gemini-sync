namespace Ymir.GeminiSync.Services.Models;

public class ExemptionMap
{
    public int Id { get; set; }

    public CompostType? CompostType { get; set; }

    public bool IsFullExemption { get; set; }
}
