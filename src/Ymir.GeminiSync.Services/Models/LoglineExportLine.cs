namespace Ymir.GeminiSync.Services.Models;

public class LoglineExportLine
{
    public int LogLineId { get; set; }

    public int AgreementLineId { get; set; }

    public DateTime Time { get; set; }

    public string Message { get; set; }

    public int PlaceNr { get; set; }

    public int UnitId { get; set; }

    public string Name { get; set; }

    public string ShortName { get; set; }

    public string Description { get; set; }
}
