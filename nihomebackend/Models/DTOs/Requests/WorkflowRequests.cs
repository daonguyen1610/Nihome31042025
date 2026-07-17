using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

/// <summary>
/// One approval step inside a <c>WorkflowConfig</c>. Serialized inside the
/// entity's <c>StepsJson</c> column and re-serialized on every upsert.
/// Runtime evaluation (routing a real record through these steps) is out
/// of scope for NIH-225 — this contract is intentionally forgiving:
/// <c>SlaHours</c> = 0 means "no SLA" and
/// <c>ConditionExpression</c> is a free-text hint for the next story.
/// </summary>
public class WorkflowStepRequest
{
    [Range(1, 99)]
    public int Order { get; set; }

    [Required, StringLength(120, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Role code (matches <c>role.Code</c> in RBAC).</summary>
    [Required, StringLength(60, MinimumLength = 1)]
    public string ApproverRoleCode { get; set; } = string.Empty;

    [Range(0, 24 * 30)]
    public int SlaHours { get; set; }

    public bool RequireAllApprovers { get; set; }

    [StringLength(500)]
    public string? ConditionExpression { get; set; }
}

/// <summary>Payload used by both create and update of a workflow config.</summary>
public class UpsertWorkflowConfigRequest
{
    [Required, StringLength(60, MinimumLength = 1)]
    public string Module { get; set; } = string.Empty;

    [Required, StringLength(60, MinimumLength = 1)]
    public string Action { get; set; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    [Required, MinLength(1)]
    public List<WorkflowStepRequest> Steps { get; set; } = new();
}
