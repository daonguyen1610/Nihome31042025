namespace NihomeBackend.Models;

public sealed class HseViolation : IConcurrencyTracked
{
    public int Id { get; set; }
    public int OperationalProjectId { get; set; }
    public OperationalProject OperationalProject { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string OfflineClientId { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public HseViolationSeverity Severity { get; set; } = HseViolationSeverity.Low;
    public string Description { get; set; } = string.Empty;
    public string? RegulatoryReference { get; set; }
    public string EvidenceDocumentsJson { get; set; } = "[]";
    public int ResponsibleSiteUserId { get; set; }
    public ApplicationUser ResponsibleSiteUser { get; set; } = null!;
    public int? RemediationOwnerUserId { get; set; }
    public ApplicationUser? RemediationOwnerUser { get; set; }
    public DateTime? RemediationDeadline { get; set; }
    public string? RemediationNote { get; set; }
    public string? PenaltyReference { get; set; }
    public decimal? PenaltyAmount { get; set; }
    public HseViolationStatus Status { get; set; } = HseViolationStatus.Draft;
    public DateTime? ReportedAt { get; set; }
    public int? ReportedByUserId { get; set; }
    public ApplicationUser? ReportedBy { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public int? ConfirmedByUserId { get; set; }
    public ApplicationUser? ConfirmedBy { get; set; }
    public DateTime? ClosedAt { get; set; }
    public int? ClosedByUserId { get; set; }
    public ApplicationUser? ClosedBy { get; set; }
    public string? DecisionReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CreatedByUserId { get; set; }
    public ApplicationUser CreatedBy { get; set; } = null!;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int UpdatedByUserId { get; set; }
    public ApplicationUser UpdatedBy { get; set; } = null!;
    public byte[] RowVersion { get; set; } = [];
    public List<HseViolationEvent> Events { get; set; } = [];
}

public sealed class HseViolationEvent
{
    public int Id { get; set; }
    public int HseViolationId { get; set; }
    public HseViolation HseViolation { get; set; } = null!;
    public HseViolationEventType Type { get; set; }
    public HseViolationStatus? FromStatus { get; set; }
    public HseViolationStatus ToStatus { get; set; }
    public string? Reason { get; set; }
    public string SnapshotJson { get; set; } = "{}";
    public int CreatedByUserId { get; set; }
    public ApplicationUser CreatedBy { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum HseViolationStatus
{
    Draft = 0,
    Reported = 1,
    Confirmed = 2,
    Remediated = 3,
    Closed = 4,
    Rejected = 5,
    Cancelled = 6,
}

public enum HseViolationSeverity
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3,
}

public enum HseViolationEventType
{
    Created = 0,
    Transition = 1,
    Correction = 2,
}