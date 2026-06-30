using System.ComponentModel.DataAnnotations;

namespace Ymir.GeminiSync.Domain;

public class AgreementExcemption
{
    [Key]
    public int Id { get; set; }

    public int GPSLSCustomerId { get; set; }

    public string PASystem { get; set; }

    public long AgreementId { get; set; }

    public int ExcemptionType { get; set; }

    public string ExternalCaseId { get; set; }

    [Required]
    public string RegisteredBy { get; set; }

    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    public DateTime RegisteredDate { get; set; }

    public ExcemptionType Type { get; set; }
}
