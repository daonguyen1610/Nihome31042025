namespace NihomeBackend.Models;

public enum ProjectScheduleRevisionStatus
{
    Draft = 0,
    PendingApproval = 1,
    Published = 2,
    Rejected = 3,
}

public sealed class ProjectScheduleRevision : IConcurrencyTracked
{
    public int Id { get; set; }
    public int OperationalProjectId { get; set; }
    public OperationalProject OperationalProject { get; set; } = null!;
    public int Version { get; set; }
    public ProjectScheduleRevisionStatus Status { get; set; } = ProjectScheduleRevisionStatus.Draft;
    public string? ChangeReason { get; set; }
    public int? ApproverUserId { get; set; }
    public ApplicationUser? ApproverUser { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public int? SubmittedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public int? ReviewedByUserId { get; set; }
    public string? ReviewComment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CreatedByUserId { get; set; }
    public DateTime? PublishedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public List<ProjectScheduleItem> Items { get; set; } = new();
}

public sealed class ProjectScheduleNode
{
    public Guid Id { get; set; }
    public int OperationalProjectId { get; set; }
    public OperationalProject OperationalProject { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class ProjectScheduleItem
{
    public int Id { get; set; }
    public int RevisionId { get; set; }
    public ProjectScheduleRevision Revision { get; set; } = null!;
    public Guid NodeId { get; set; }
    public ProjectScheduleNode Node { get; set; } = null!;
    public Guid? ParentNodeId { get; set; }
    public ProjectScheduleNode? ParentNode { get; set; }
    public string WbsCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateOnly PlannedStart { get; set; }
    public DateOnly PlannedEnd { get; set; }
    public int SortOrder { get; set; }
}
