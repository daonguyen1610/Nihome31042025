namespace NihomeBackend.Models.DTOs.Responses;

public class HandoverChecklistItemResponse
{
    public string Name { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public string? Note { get; set; }
}

public class HandoverReadinessResponse
{
    public int ApprovedRequiredAsBuiltCategories { get; set; }
    public int RequiredAsBuiltCategories { get; set; }
    public int UnresolvedPunchItems { get; set; }
    public int ApprovedAcceptanceRecords { get; set; }
    public bool CommissioningCompleted { get; set; }
    public bool ChecklistCompleted { get; set; }
    public bool IsReady { get; set; }
}

public class HandoverStatusHistoryResponse
{
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public string? Note { get; set; }
    public int ChangedByUserId { get; set; }
    public string ChangedByName { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
}

public class HandoverRecordResponse
{
    public int Id { get; set; }
    public int DesignProjectId { get; set; }
    public string DesignProjectName { get; set; } = string.Empty;
    public string HandoverCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly PlannedHandoverDate { get; set; }
    public DateOnly? ActualHandoverDate { get; set; }
    public string? Location { get; set; }
    public int ResponsibleUserId { get; set; }
    public string ResponsibleUserName { get; set; } = string.Empty;
    public bool CommissioningCompleted { get; set; }
    public string? CommissioningNotes { get; set; }
    public List<HandoverChecklistItemResponse> ChecklistItems { get; set; } = new();
    public List<string> Documents { get; set; } = new();
    public List<string> Signatories { get; set; } = new();
    public string? ResolutionNote { get; set; }
    public string Status { get; set; } = string.Empty;
    public HandoverReadinessResponse Readiness { get; set; } = new();
    public List<HandoverStatusHistoryResponse> StatusHistory { get; set; } = new();
    public DateTime? SubmittedAt { get; set; }
    public string? SubmittedByName { get; set; }
    public DateTime? HandedOverAt { get; set; }
    public string? HandedOverByName { get; set; }
    public int ReopenCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class HandoverRecordListResponse
{
    public List<HandoverRecordResponse> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public Dictionary<string, int> StatusCounts { get; set; } = new();
    public int ReadyCount { get; set; }
}