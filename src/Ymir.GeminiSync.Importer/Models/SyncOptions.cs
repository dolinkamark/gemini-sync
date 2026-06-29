namespace Ymir.GeminiSync.Importer.Models;

public class SyncOptions
{
    public const string SectionName = "Importer";

    //Possible values: GarbageBins,UtilityConnections,Fractions
    public string Entities { get; set; }

    public bool Delete { get; set; }

    public int CustomerId { get; set; }

    public string PlaceTypes { get; set; }

    public bool UseFileCache { get; set; }
}
