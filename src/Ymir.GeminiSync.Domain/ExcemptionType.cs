using System.ComponentModel.DataAnnotations;

namespace Ymir.GeminiSync.Domain;

public class ExcemptionType
{
    [Key]
    public int Id { get; set; }

    public int CustomerId { get; set; }

    [MaxLength(100)]
    public string Name { get; set; }

    public int Status { get; set; }

    //Defines if it is Fritak or not
    //1 = Fritak -> complete exemption, don't send anything
    //2 = Compost rebate
    public int? Category { get; set; }

    //The Id for Gemini API
    public string ExternalId { get; set; }
}
