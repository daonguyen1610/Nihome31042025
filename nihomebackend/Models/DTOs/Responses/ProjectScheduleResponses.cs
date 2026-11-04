namespace NihomeBackend.Models.DTOs.Responses;

public sealed class ProjectScheduleResponse
{
    public int OperationalProjectId { get; set; }
    public bool HasSignedUpstreamContract { get; set; }
    public int? PublishedRevisionId { get; set; }
    public List<ProjectScheduleApproverResponse> ApproverOptions { get; set; } = new();
    public List<ProjectScheduleRevisionResponse> Revisions { get; set; } = new();
}

public sealed class ProjectScheduleApproverResponse
{
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
}

public sealed class ProjectScheduleRevisionResponse
{
    public int Id { get; set; }
    public int Version { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ChangeReason { get; set; }
    public int? ApproverUserId { get; set; }
    public string? ApproverName { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public int? ReviewedByUserId { get; set; }
    public string? ReviewComment { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public List<ProjectScheduleItemResponse> Items { get; set; } = new();
}

public sealed class ProjectScheduleItemResponse
{
    public Guid NodeId { get; set; }
    public Guid? ParentNodeId { get; set; }
    public string WbsCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateOnly PlannedStart { get; set; }
    public DateOnly PlannedEnd { get; set; }
    public int SortOrder { get; set; }
}
