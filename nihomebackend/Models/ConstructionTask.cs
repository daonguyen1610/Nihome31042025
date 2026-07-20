namespace NihomeBackend.Models;

/// <summary>
/// M4 Construction & Acceptance — schedule task (Tiến độ Gantt / NIH-141).
///
/// One row = one WBS-scoped construction activity on a
/// <see cref="DesignProject"/>. Slice 1 covers:
/// <list type="bullet">
///   <item>Planned vs. actual dates + progress % + owner.</item>
///   <item>Status roll-up (Planned → InProgress → Completed) with a
///     computed <c>Delayed</c> flag when we blow past the planned end.</item>
///   <item>Finish-to-Start dependencies through
///     <see cref="ConstructionTaskDependency"/> (predecessor / successor)
///     — enough to draw a Gantt chart and detect cycles.</item>
/// </list>
///
/// Slice 2 (deferred): baseline snapshots, resource loading,
/// critical-path, per-day % updates, notification when a predecessor slips.
/// </summary>
public class ConstructionTask
{
    public int Id { get; set; }

    public int DesignProjectId { get; set; }
    public DesignProject DesignProject { get; set; } = null!;

    /// <summary>Human-friendly code, unique inside a project (e.g. <c>T-001</c>).</summary>
    public string TaskCode { get; set; } = string.Empty;

    /// <summary>Optional WBS number (e.g. <c>1.2.3</c>) — free text.</summary>
    public string? Wbs { get; set; }

    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Free-text working note / scope description.</summary>
    public string? Description { get; set; }

    public DateOnly PlannedStart { get; set; }
    public DateOnly PlannedEnd { get; set; }
    public DateOnly? ActualStart { get; set; }
    public DateOnly? ActualEnd { get; set; }

    /// <summary>0-100 completion.</summary>
    public int ProgressPercent { get; set; }

    public int? OwnerUserId { get; set; }
    public ApplicationUser? Owner { get; set; }

    public ConstructionTaskStatus Status { get; set; } = ConstructionTaskStatus.Planned;

    /// <summary>Tasks this task cannot start before (Finish-to-Start).</summary>
    public List<ConstructionTaskDependency> Predecessors { get; set; } = new();

    // --- Audit ---
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedByUserId { get; set; }
}

/// <summary>Lifecycle of a <see cref="ConstructionTask"/>.</summary>
public enum ConstructionTaskStatus
{
    /// <summary>Scheduled but not started.</summary>
    Planned = 0,
    /// <summary>Work in progress on site.</summary>
    InProgress = 1,
    /// <summary>Finished — actual end recorded.</summary>
    Completed = 2,
    /// <summary>Aborted — not part of the delivered scope any more.</summary>
    Cancelled = 3,
}

/// <summary>
/// Finish-to-Start edge in the task graph: <see cref="TaskId"/> cannot
/// start until <see cref="PredecessorTaskId"/> is <c>Completed</c>.
/// One row per edge; unique on <c>(TaskId, PredecessorTaskId)</c>.
/// </summary>
public class ConstructionTaskDependency
{
    public int Id { get; set; }

    public int TaskId { get; set; }
    public ConstructionTask Task { get; set; } = null!;

    public int PredecessorTaskId { get; set; }
    public ConstructionTask PredecessorTask { get; set; } = null!;
}
