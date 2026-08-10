namespace NihomeBackend.Models;

/// <summary>
/// Whole-project acceptance and handover record (NIH-144). One project
/// has at most one active handover aggregate; readiness is derived from
/// acceptance, punch-list and as-built records rather than copied here.
/// </summary>
public class HandoverRecord
{
    public int Id { get; set; }
    public int DesignProjectId { get; set; }
    public DesignProject DesignProject { get; set; } = null!;
    public string HandoverCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly PlannedHandoverDate { get; set; }
    public DateOnly? ActualHandoverDate { get; set; }
    public string? Location { get; set; }
    public int ResponsibleUserId { get; set; }
    public ApplicationUser ResponsibleUser { get; set; } = null!;
    public bool CommissioningCompleted { get; set; }
    public string? CommissioningNotes { get; set; }
    public string ChecklistItems { get; set; } = "[]";
    public bool ChecklistCompleted { get; set; }
    public string Documents { get; set; } = "[]";
    public string Signatories { get; set; } = "[]";
    public string? ResolutionNote { get; set; }
    public HandoverStatus Status { get; set; } = HandoverStatus.Draft;
    public DateTime? SubmittedAt { get; set; }
    public int? SubmittedByUserId { get; set; }
    public ApplicationUser? SubmittedBy { get; set; }
    public DateTime? HandedOverAt { get; set; }
    public int? HandedOverByUserId { get; set; }
    public ApplicationUser? HandedOverBy { get; set; }
    public int ReopenCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int UpdatedByUserId { get; set; }
    public List<HandoverStatusHistory> StatusHistory { get; set; } = new();
}

public class HandoverStatusHistory
{
    public int Id { get; set; }
    public int HandoverRecordId { get; set; }
    public HandoverRecord HandoverRecord { get; set; } = null!;
    public HandoverStatus? FromStatus { get; set; }
    public HandoverStatus ToStatus { get; set; }
    public string? Note { get; set; }
    public int ChangedByUserId { get; set; }
    public ApplicationUser ChangedByUser { get; set; } = null!;
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}

public enum HandoverStatus
{
    Draft = 0,
    ReadyForHandover = 1,
    HandedOver = 2,
    Reopened = 3,
    Cancelled = 4,
}