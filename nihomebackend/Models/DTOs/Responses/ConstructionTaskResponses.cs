namespace NihomeBackend.Models.DTOs.Responses;

/// <summary>Wire shape returned for a single <c>ConstructionTask</c>.</summary>
public class ConstructionTaskResponse
{
    public int Id { get; set; }
    public int DesignProjectId { get; set; }
    public string? DesignProjectCode { get; set; }
    public string? DesignProjectName { get; set; }

    public string TaskCode { get; set; } = string.Empty;
    public string? Wbs { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public DateOnly PlannedStart { get; set; }
    public DateOnly PlannedEnd { get; set; }
    public DateOnly? ActualStart { get; set; }
    public DateOnly? ActualEnd { get; set; }

    public int ProgressPercent { get; set; }
    public int? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }

    /// <summary>Enum name (Planned/InProgress/Completed/Cancelled).</summary>
    public string Status { get; set; } = "Planned";

    /// <summary>
    /// Derived flag: task is past <see cref="PlannedEnd"/> and not yet
    /// Completed / Cancelled. Computed server-side so the UI never has to
    /// re-do the today comparison.
    /// </summary>
    public bool IsOverdue { get; set; }

    public List<ConstructionTaskDependencyResponse> Predecessors { get; set; } = new();

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ConstructionTaskDependencyResponse
{
    public int Id { get; set; }
    public int PredecessorTaskId { get; set; }
    public string? PredecessorTaskCode { get; set; }
    public string? PredecessorTaskName { get; set; }
    public string? PredecessorStatus { get; set; }
}

public class ConstructionTaskListResponse
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<ConstructionTaskResponse> Items { get; set; } = new();
    /// <summary>Per-status roll-up on the current filter scope.</summary>
    public Dictionary<string, int> StatusCounts { get; set; } = new();
    /// <summary>Count of overdue tasks on the current filter scope.</summary>
    public int OverdueCount { get; set; }
}

public class ConstructionTaskBulkDeleteResponse
{
    public int Requested { get; set; }
    public int Deleted { get; set; }
    public List<ConstructionTaskBulkDeleteFailure> Failures { get; set; } = new();
}

public class ConstructionTaskBulkDeleteFailure
{
    public int Id { get; set; }
    public string Message { get; set; } = string.Empty;
}
