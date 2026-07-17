namespace NihomeBackend.Models.DTOs.Responses;

public class TenderChecklistItemResponse
{
    public int Id { get; set; }
    public string? TemplateCode { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
    public DateTime? InternalDeadline { get; set; }
    public string? FilePath { get; set; }
    public string? OriginalFileName { get; set; }
    public int SortOrder { get; set; }
}

public class TenderResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;

    public DateTime? OpeningDate { get; set; }
    public DateTime SubmissionDeadline { get; set; }

    public int? PreparerUserId { get; set; }
    public string? PreparerName { get; set; }

    public string? InfoSource { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Note { get; set; }

    public int? WonOpportunityId { get; set; }
    public string? LostReasonCode { get; set; }
    public string? LostNote { get; set; }
    public DateTime? ClosedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<TenderChecklistItemResponse> ChecklistItems { get; set; } = new();

    /// <summary>Percent of checklist items in a completed state (Done/Submitted).</summary>
    public int ChecklistCompletionPercent { get; set; }

    /// <summary>True when SubmissionDeadline - now &lt;= 3 days and status is non-terminal.</summary>
    public bool IsDeadlineImminent { get; set; }
}

public class TenderListItemResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;

    public DateTime? OpeningDate { get; set; }
    public DateTime SubmissionDeadline { get; set; }

    public int? PreparerUserId { get; set; }
    public string? PreparerName { get; set; }

    public string Status { get; set; } = string.Empty;
    public int ChecklistCompletionPercent { get; set; }
    public bool IsDeadlineImminent { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public class TenderListResponse
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<TenderListItemResponse> Items { get; set; } = new();
}
