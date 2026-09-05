namespace NihomeBackend.Models.DTOs.Responses;

public sealed class DesignScheduleResponse
{
    public int OperationalProjectId { get; set; }
    public int DesignProjectId { get; set; }
    public bool CanManage { get; set; }
    public bool BaselineReady { get; set; }
    public decimal? ProgressPercent { get; set; }
    public string RollupPolicyVersion { get; set; } = "design-schedule-weighted-v1";
    public List<DesignSchedulePhaseResponse> Phases { get; set; } = new();
    public List<DesignSchedulePhaseRollupResponse> RollupSources { get; set; } = new();
    public PagedDesignScheduleTasksResponse Tasks { get; set; } = new();
}

public sealed class DesignSchedulePhaseResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public DateOnly PlannedStart { get; set; }
    public DateOnly PlannedEnd { get; set; }
    public DateOnly? ActualStart { get; set; }
    public DateOnly? ActualEnd { get; set; }
    public string Status { get; set; } = string.Empty;
    public int ProgressPercent { get; set; }
    public int Weight { get; set; }
    public bool Overdue { get; set; }
    public bool BaselineReady { get; set; }
    public decimal? RolledUpProgressPercent { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class DesignScheduleTaskResponse
{
    public int Id { get; set; }
    public int PhaseId { get; set; }
    public string PhaseCode { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DepartmentCode { get; set; } = string.Empty;
    public int AssigneeMemberId { get; set; }
    public bool IsMilestone { get; set; }
    public DateOnly PlannedStart { get; set; }
    public DateOnly PlannedEnd { get; set; }
    public DateOnly? ActualStart { get; set; }
    public DateOnly? ActualEnd { get; set; }
    public string Status { get; set; } = string.Empty;
    public int ProgressPercent { get; set; }
    public int Weight { get; set; }
    public bool Overdue { get; set; }
    public List<int> PredecessorTaskIds { get; set; } = new();
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class DesignScheduleTaskRollupSourceResponse
{
    public int TaskId { get; set; }
    public int Weight { get; set; }
    public int ProgressPercent { get; set; }
    public decimal WeightedValue { get; set; }
}

public sealed class DesignSchedulePhaseRollupResponse
{
    public int PhaseId { get; set; }
    public int Weight { get; set; }
    public bool BaselineReady { get; set; }
    public decimal? ProgressPercent { get; set; }
    public decimal? WeightedValue { get; set; }
    public List<DesignScheduleTaskRollupSourceResponse> TaskSources { get; set; } = new();
}

public sealed class PagedDesignScheduleTasksResponse
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public List<DesignScheduleTaskResponse> Items { get; set; } = new();
}