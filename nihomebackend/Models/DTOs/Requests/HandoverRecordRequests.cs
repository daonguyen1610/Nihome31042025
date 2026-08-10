namespace NihomeBackend.Models.DTOs.Requests;

public class HandoverRecordListParams
{
    public int? DesignProjectId { get; set; }
    public int? ResponsibleUserId { get; set; }
    public string? Status { get; set; }
    public DateOnly? PlannedFrom { get; set; }
    public DateOnly? PlannedTo { get; set; }
    public string? Search { get; set; }
    public bool ReadyOnly { get; set; }
    public string? SortBy { get; set; }
    public string? SortDirection { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class HandoverChecklistItemRequest
{
    public string Name { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public string? Note { get; set; }
}

public class CreateHandoverRecordRequest
{
    public int DesignProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly PlannedHandoverDate { get; set; }
    public string? Location { get; set; }
    public int ResponsibleUserId { get; set; }
    public bool CommissioningCompleted { get; set; }
    public string? CommissioningNotes { get; set; }
    public List<HandoverChecklistItemRequest> ChecklistItems { get; set; } = new();
    public List<string> Documents { get; set; } = new();
    public List<string> Signatories { get; set; } = new();
}

public class UpdateHandoverRecordRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly PlannedHandoverDate { get; set; }
    public string? Location { get; set; }
    public int ResponsibleUserId { get; set; }
    public bool CommissioningCompleted { get; set; }
    public string? CommissioningNotes { get; set; }
    public List<HandoverChecklistItemRequest> ChecklistItems { get; set; } = new();
    public List<string> Documents { get; set; } = new();
    public List<string> Signatories { get; set; } = new();
}

public class TransitionHandoverStatusRequest
{
    public string Status { get; set; } = string.Empty;
    public string? Note { get; set; }
}