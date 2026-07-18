using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

/// <summary>Create payload for a Concept option (NIH-114). Status starts at Drafting.</summary>
public class CreateConceptOptionRequest
{
    [Required]
    public int DesignProjectId { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? Description { get; set; }

    [StringLength(4000)]
    public string? InternalNote { get; set; }

    public int? OwnerUserId { get; set; }

    public DateTime? PresentedAt { get; set; }
}

/// <summary>
/// Full-row update payload. Status transitions are handled by the dedicated
/// <c>/status</c> endpoint so a plain PUT is safe against workflow rules —
/// this only touches metadata.
/// </summary>
public class UpdateConceptOptionRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? Description { get; set; }

    [StringLength(4000)]
    public string? InternalNote { get; set; }

    public int? OwnerUserId { get; set; }

    public DateTime? PresentedAt { get; set; }
}

/// <summary>
/// Status transition payload. The server validates that the target is
/// reachable from the current state and enforces the "only one Finalized
/// per project" invariant.
/// </summary>
public class TransitionConceptOptionStatusRequest
{
    [Required]
    public string Status { get; set; } = string.Empty;
}

public class ConceptOptionListParams
{
    /// <summary>Scope the list to a single project — required in practice.</summary>
    public int? DesignProjectId { get; set; }

    /// <summary>Comma-separated <c>ConceptOptionStatus</c> values.</summary>
    public string? Status { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
