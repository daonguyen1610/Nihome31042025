using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public class CreateLeadRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(200)]
    public string? CompanyName { get; set; }

    [StringLength(30)]
    public string? Phone { get; set; }

    [StringLength(150)]
    [EmailAddress]
    public string? Email { get; set; }

    /// <summary>Master-data code from category <c>customer_source</c>.</summary>
    [Required]
    [StringLength(60, MinimumLength = 1)]
    public string SourceCode { get; set; } = string.Empty;

    /// <summary>Optional explicit owner. If null, service auto-assigns round-robin.</summary>
    public int? OwnerUserId { get; set; }

    [StringLength(4000)]
    public string? Note { get; set; }
}
