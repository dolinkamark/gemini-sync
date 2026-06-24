using System.ComponentModel.DataAnnotations;

namespace Ymir.GeminiSync.Domain;

public class ExcemptionType
{
    public int Id { get; set; }

    public int CustomerId { get; set; }

    [MaxLength(100)]
    public string Name { get; set; }

    public int Status { get; set; }

    public int? Category { get; set; }

    public string ExternalId { get; set; }
}
