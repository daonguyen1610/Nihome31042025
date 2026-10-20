using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public class ContractCareActivityRequest
{
    [Required]
    [Range(0, 3, ErrorMessage = "Activity type must be Call, Email, Meeting or Note.")]
    public CustomerActivityType? Type { get; set; }

    [Required]
    [StringLength(4000, MinimumLength = 1)]
    public string Content { get; set; } = string.Empty;

    public DateTimeOffset? OccurredAt { get; set; }
}
