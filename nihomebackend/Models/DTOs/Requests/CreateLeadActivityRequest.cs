using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public class CreateLeadActivityRequest
{
    [Required]
    public LeadActivityType Type { get; set; }

    [Required]
    [StringLength(2000, MinimumLength = 1)]
    public string Content { get; set; } = string.Empty;
}
