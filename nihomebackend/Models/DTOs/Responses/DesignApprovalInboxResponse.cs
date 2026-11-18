namespace NihomeBackend.Models.DTOs.Responses;

public sealed class DesignApprovalInboxResponse
{
    public IReadOnlyList<DesignApprovalInboxTaskResponse> Tasks { get; init; } = [];
    public IReadOnlyList<DesignApprovalInboxReviewResponse> Reviews { get; init; } = [];
}

public sealed class DesignApprovalInboxTaskResponse
{
    public int TaskId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int DesignProjectId { get; init; }
    public string DesignProjectName { get; init; } = string.Empty;
    public string? AssigneeName { get; init; }
    public DateTime? SubmittedAt { get; init; }
}

public sealed class DesignApprovalInboxReviewResponse
{
    public int ReviewId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Stage { get; init; } = string.Empty;
    public int DesignProjectId { get; init; }
    public string DesignProjectName { get; init; } = string.Empty;
    public DateTime? SubmittedAt { get; init; }
    public bool HasMyComment { get; init; }
}
