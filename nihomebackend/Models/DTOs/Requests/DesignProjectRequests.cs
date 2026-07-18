using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

/// <summary>
/// Create payload for M2 Design Project (NIH-113). Code is auto-generated
/// server-side and never accepted from the client.
/// </summary>
public class CreateDesignProjectRequest
{
    [Required]
    [StringLength(300, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public int CustomerId { get; set; }

    public int? ContractId { get; set; }
    public int? ProjectManagerUserId { get; set; }
    public int? DesignLeadUserId { get; set; }

    public DateTime? StartDate { get; set; }
    public DateTime? Deadline { get; set; }

    [StringLength(4000)]
    public string? Note { get; set; }
}

/// <summary>
/// Update payload. Same fields as create plus lifecycle knobs the create
/// form does not expose (stage + status). Both are optional so partial
/// updates from the header dropdown work without shipping the full form.
/// </summary>
public class UpdateDesignProjectRequest : CreateDesignProjectRequest
{
    /// <summary>Concept / BasicDesign / ShopDrawing / Completed.</summary>
    public string? CurrentStage { get; set; }

    /// <summary>Active / OnHold / Completed / Cancelled.</summary>
    public string? Status { get; set; }
}

/// <summary>
/// Query parameters for the list surface. Kept parallel to the survey /
/// tender list params so the FE table shell can reuse its filter primitives.
/// </summary>
public class DesignProjectListParams
{
    public int? CustomerId { get; set; }
    public int? ContractId { get; set; }
    public int? ProjectManagerUserId { get; set; }
    public int? DesignLeadUserId { get; set; }

    /// <summary>Multi-select — comma-separated enum names.</summary>
    public string? Stage { get; set; }
    public string? Status { get; set; }

    /// <summary>Inclusive lower bound of <c>Deadline</c>.</summary>
    public DateTime? DeadlineFrom { get; set; }
    /// <summary>Inclusive upper bound of <c>Deadline</c>.</summary>
    public DateTime? DeadlineTo { get; set; }

    /// <summary>Substring match against <c>Name</c> + <c>ProjectCode</c>.</summary>
    public string? Search { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
