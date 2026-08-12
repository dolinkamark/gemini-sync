using System.ComponentModel.DataAnnotations;

namespace Ymir.GeminiSync.Domain;

public class IntegrationExecution
{
    [Key]
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public int IntegrationId { get; set; }

    public ExecutionStatus Status { get; set; }

    [Required]
    public string Parameters { get; set; }

    public string ExecutionLog { get; set; }

    public bool HasErrors { get; set; }

    public string ErrorLog { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
