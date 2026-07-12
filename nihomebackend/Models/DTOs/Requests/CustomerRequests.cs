using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public class UpsertCustomerContactRequest
{
    /// <summary>When present, updates the existing contact; otherwise creates a new one.</summary>
    public int? Id { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string FullName { get; set; } = string.Empty;

    [StringLength(150)]
    public string? Position { get; set; }

    [StringLength(30)]
    public string? Phone { get; set; }

    [StringLength(150)]
    [EmailAddress]
    public string? Email { get; set; }

    public bool IsPrimary { get; set; }
}

public class CreateCustomerRequest
{
    [Required]
    public CustomerType Type { get; set; } = CustomerType.Individual;

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(30)]
    public string? TaxId { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }

    [StringLength(200)]
    public string? RepresentativeName { get; set; }

    [Required]
    [StringLength(60, MinimumLength = 1)]
    public string SourceCode { get; set; } = string.Empty;

    /// <summary>Optional explicit owner. If null, service assigns caller (when sales) or leaves unassigned.</summary>
    public int? OwnerUserId { get; set; }

    [StringLength(4000)]
    public string? Note { get; set; }

    /// <summary>
    /// Initial primary contact — required so a customer always has ≥1 contact
    /// with a primary flag. Additional contacts can be added via
    /// <see cref="UpsertCustomerContactRequest"/> on the customer detail.
    /// </summary>
    [Required]
    public UpsertCustomerContactRequest PrimaryContact { get; set; } = new();

    /// <summary>
    /// Optional caller-supplied reason for saving despite a detected duplicate
    /// (matching TaxId for Company or matching primary phone for Individual).
    /// When null and a duplicate exists the service returns a 409-style
    /// error with the collision detail.
    /// </summary>
    [StringLength(500)]
    public string? DuplicateOverrideReason { get; set; }
}

public class UpdateCustomerRequest
{
    [Required]
    public CustomerType Type { get; set; } = CustomerType.Individual;

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(30)]
    public string? TaxId { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }

    [StringLength(200)]
    public string? RepresentativeName { get; set; }

    [Required]
    [StringLength(60, MinimumLength = 1)]
    public string SourceCode { get; set; } = string.Empty;

    [Required]
    public CustomerRelationshipStatus RelationshipStatus { get; set; }

    public int? OwnerUserId { get; set; }

    [StringLength(4000)]
    public string? Note { get; set; }

    [StringLength(500)]
    public string? DuplicateOverrideReason { get; set; }
}

public class CreateCustomerActivityRequest
{
    [Required]
    public CustomerActivityType Type { get; set; }

    [Required]
    [StringLength(4000, MinimumLength = 1)]
    public string Content { get; set; } = string.Empty;

    /// <summary>Optional back-dated occurrence time; defaults to now on the server.</summary>
    public DateTime? OccurredAt { get; set; }
}
