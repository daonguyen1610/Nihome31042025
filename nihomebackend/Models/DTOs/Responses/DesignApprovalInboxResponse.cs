namespace NihomeBackend.Models.DTOs.Responses;

/// <summary>Counts for the inbox tabs and the dossiers that currently have work waiting on the caller.</summary>
public sealed class DesignApprovalInboxSummaryResponse
{
    public int TaskCount { get; init; }
    public int ReviewCount { get; init; }
    public IReadOnlyList<DesignApprovalInboxDossierResponse> Dossiers { get; init; } = [];
}

public sealed class DesignApprovalInboxDossierResponse
{
    public int Id { get; init; }
    public string ProjectCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

public sealed class DesignApprovalInboxPageResponse<T>
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int Total { get; init; }
    public IReadOnlyList<T> Items { get; init; } = [];
}

public sealed class DesignApprovalInboxTaskResponse
{
    public int TaskId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string PhaseCode { get; init; } = string.Empty;
    public int DesignProjectId { get; init; }
    public string DesignProjectCode { get; init; } = string.Empty;
    public string DesignProjectName { get; init; } = string.Empty;
    public string? AssigneeName { get; init; }
    public DateOnly PlannedEnd { get; init; }
    public DateTime? SubmittedAt { get; init; }
}

public sealed class DesignApprovalInboxReviewResponse
{
    public int ReviewId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Stage { get; init; } = string.Empty;
    public int RevisionNumber { get; init; }
    public int DesignProjectId { get; init; }
    public string DesignProjectCode { get; init; } = string.Empty;
    public string DesignProjectName { get; init; } = string.Empty;
    public string? DesignManagerName { get; init; }
    public DateTime? SubmittedAt { get; init; }
    public bool HasMyComment { get; init; }
}
