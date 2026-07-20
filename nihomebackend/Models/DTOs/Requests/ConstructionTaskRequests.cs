namespace NihomeBackend.Models.DTOs.Requests;

/// <summary>
/// List/filter parameters for
/// <c>GET /api/construction-tasks</c> — matches the M4 Gantt list DoD
/// (search + project/status/owner filters + overdue-only flag + pagination).
/// </summary>
public class ConstructionTaskListParams
{
    public int? DesignProjectId { get; set; }
    public int? OwnerUserId { get; set; }
    /// <summary>Comma-separated status names, e.g. <c>Planned,InProgress</c>.</summary>
    public string? Status { get; set; }
    /// <summary>Match against task code / name / WBS.</summary>
    public string? Search { get; set; }
    /// <summary>Restrict to tasks that have blown past <see cref="PlannedEnd"/>.</summary>
    public bool? OverdueOnly { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class CreateConstructionTaskRequest
{
    public int DesignProjectId { get; set; }
    public string? TaskCode { get; set; }
    public string? Wbs { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly PlannedStart { get; set; }
    public DateOnly PlannedEnd { get; set; }
    public int? OwnerUserId { get; set; }
    /// <summary>Optional predecessors (existing task ids in the same project).</summary>
    public List<int>? PredecessorTaskIds { get; set; }
}

public class UpdateConstructionTaskRequest
{
    public string? Wbs { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly PlannedStart { get; set; }
    public DateOnly PlannedEnd { get; set; }
    public DateOnly? ActualStart { get; set; }
    public DateOnly? ActualEnd { get; set; }
    public int ProgressPercent { get; set; }
    public int? OwnerUserId { get; set; }
    public string Status { get; set; } = "Planned";
}

public class UpdateConstructionTaskProgressRequest
{
    public int ProgressPercent { get; set; }
    public DateOnly? ActualStart { get; set; }
    public DateOnly? ActualEnd { get; set; }
    public string? Status { get; set; }
}

public class SetConstructionTaskPredecessorsRequest
{
    public List<int> PredecessorTaskIds { get; set; } = new();
}

public class BulkDeleteConstructionTasksRequest
{
    public List<int> Ids { get; set; } = new();
}
