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

public enum DesignTaskApprovalStatus
{
    NotSubmitted = 0,
    Pending = 1,
    Approved = 2,
    Rejected = 3,
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
    public string? Description { get; set; }
    public string DepartmentCode { get; set; } = string.Empty;
    public int AssigneeMemberId { get; set; }
    public OperationalProjectMember AssigneeMember { get; set; } = null!;
    public int ApproverMemberId { get; set; }
    public OperationalProjectMember ApproverMember { get; set; } = null!;
    public int? ParentTaskId { get; set; }
    public DesignScheduleTask? ParentTask { get; set; }
    public List<DesignScheduleTask> Children { get; set; } = new();
    public string? ResourceSummary { get; set; }
    public int? VendorId { get; set; }
    public Vendor? Vendor { get; set; }
    public int? DownstreamContractId { get; set; }
    public Contract? DownstreamContract { get; set; }
    public bool IsCritical { get; set; }
    public bool IsMilestone { get; set; }
    public DateOnly PlannedStart { get; set; }
    public DateOnly PlannedEnd { get; set; }
    public DateOnly? ActualStart { get; set; }
    public DateOnly? ActualEnd { get; set; }
    public DesignScheduleStatus Status { get; set; }
    public int ProgressPercent { get; set; }
    public int Weight { get; set; }
    public DesignTaskApprovalStatus ApprovalStatus { get; set; } = DesignTaskApprovalStatus.NotSubmitted;
    public DateTime? SubmittedAt { get; set; }
    public int? SubmittedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public int? ReviewedByUserId { get; set; }
    public string? ReviewComment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int UpdatedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public List<DesignScheduleTaskDependency> Predecessors { get; set; } = new();
    public List<DesignScheduleTaskDependency> Successors { get; set; } = new();
    public List<DesignScheduleTaskCcMember> CcMembers { get; set; } = new();
}

public class DesignScheduleTaskCcMember
{
    public int TaskId { get; set; }
    public DesignScheduleTask Task { get; set; } = null!;
    public int MemberId { get; set; }
    public OperationalProjectMember Member { get; set; } = null!;
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
