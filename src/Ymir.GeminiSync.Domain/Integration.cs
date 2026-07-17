using System.ComponentModel.DataAnnotations;

namespace Ymir.GeminiSync.Domain;

public class Integration
{
    public int Id { get; set; }

    public int CustomerId { get; set; }

    [Required]
    [StringLength(50)]
    public string Name { get; set; }

    [Required]
    [StringLength(50)]
    public string IntegrationType { get; set; }

    public bool Enabled { get; set; }

    public string Configuration { get; set; }

    public int? Status { get; set; }

    public DateTime CreatedAt { get; set; }

    [StringLength(50)]
    public string CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    [StringLength(50)]
    public string UpdatedBy { get; set; }
}
