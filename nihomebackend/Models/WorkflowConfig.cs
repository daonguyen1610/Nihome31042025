using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models;

/// <summary>
/// Reusable approval flow definition. NIH-225 covers the configuration side
/// only — runtime evaluation of these flows against real records happens in
/// later stories.
///
/// A workflow is scoped to a <see cref="Module"/> / <see cref="Action"/>
/// pair (e.g. <c>quotes</c> + <c>approve</c>). The ordered chain of
/// approval steps is stored as JSON in <see cref="StepsJson"/> to avoid
/// spawning an extra table for what is essentially a small ordered list.
/// </summary>
public class WorkflowConfig
{
    public int Id { get; set; }

    /// <summary>Business module the workflow applies to, e.g. <c>quotes</c>.</summary>
    [Required, MaxLength(60)]
    public string Module { get; set; } = string.Empty;

    /// <summary>Action within the module, e.g. <c>approve</c>, <c>sign</c>.</summary>
    [Required, MaxLength(60)]
    public string Action { get; set; } = string.Empty;

    /// <summary>Human-readable name (Vietnamese default).</summary>
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    /// <summary>
    /// JSON-serialized ordered array of <c>WorkflowStep</c> objects. Kept
    /// as a string to allow lightweight config edits without extra joins.
    /// </summary>
    [Required]
    public string StepsJson { get; set; } = "[]";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
