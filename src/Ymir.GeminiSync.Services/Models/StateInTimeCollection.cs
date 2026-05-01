using Ymir.GeminiSync.Domain;

namespace Ymir.GeminiSync.Services.Models;

/// <summary>
/// Intermediate class between bin collection lines and Gemini Dto
/// </summary>
public class StateInTimeCollection
{
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public List<GarbageBinCollectionLine> Lines { get; set; } = new List<GarbageBinCollectionLine>();
}
