namespace NihomeBackend.Models;

public enum DesignSchedulePhaseCode
{
    Concept = 0,
    BasicDesign = 1,
    ShopDrawing = 2,
}

public enum DesignScheduleStatus
{
    NotStarted = 0,
    InProgress = 1,
    Completed = 2,
    OnHold = 3,
    WaitingForDepartment = 4,
}

public class DesignSchedulePhase : IConcurrencyTracked
{
    public int Id { get; set; }
    public int OperationalProjectId { get; set; }
    public OperationalProject OperationalProject { get; set; } = null!;
    public int DesignProjectId { get; set; }
    public DesignProject DesignProject { get; set; } = null!;
    public DesignSchedulePhaseCode Code { get; set; }
    public DateOnly PlannedStart { get; set; }
    public DateOnly PlannedEnd { get; set; }
    public DateOnly? ActualStart { get; set; }
    public DateOnly? ActualEnd { get; set; }
    public DesignScheduleStatus Status { get; set; }
    public int ProgressPercent { get; set; }
    public int Weight { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int UpdatedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public List<DesignScheduleTask> Tasks { get; set; } = new();
}

public class DesignScheduleTask : IConcurrencyTracked
{
    public int Id { get; set; }
    public int OperationalProjectId { get; set; }
    public OperationalProject OperationalProject { get; set; } = null!;
    public int DesignProjectId { get; set; }
    public DesignProject DesignProject { get; set; } = null!;
    public int PhaseId { get; set; }
    public DesignSchedulePhase Phase { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DepartmentCode { get; set; } = string.Empty;
    public int AssigneeMemberId { get; set; }
    public OperationalProjectMember AssigneeMember { get; set; } = null!;
    public bool IsMilestone { get; set; }
    public DateOnly PlannedStart { get; set; }
    public DateOnly PlannedEnd { get; set; }
    public DateOnly? ActualStart { get; set; }
    public DateOnly? ActualEnd { get; set; }
    public DesignScheduleStatus Status { get; set; }
    public int ProgressPercent { get; set; }
    public int Weight { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int UpdatedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public List<DesignScheduleTaskDependency> Predecessors { get; set; } = new();
    public List<DesignScheduleTaskDependency> Successors { get; set; } = new();
}

public class DesignScheduleTaskDependency
{
    public int Id { get; set; }
    public int OperationalProjectId { get; set; }
    public int TaskId { get; set; }
    public DesignScheduleTask Task { get; set; } = null!;
    public int PredecessorTaskId { get; set; }
    public DesignScheduleTask PredecessorTask { get; set; } = null!;
}

public class DesignScheduleHistory
{
    public long Id { get; set; }
    public int OperationalProjectId { get; set; }
    public int DesignProjectId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string SnapshotJson { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public int ChangedByUserId { get; set; }
}