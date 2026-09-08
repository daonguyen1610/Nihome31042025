namespace NihomeBackend.Models;

public sealed class MaterialAlert : IConcurrencyTracked
{
    public int Id { get; set; }
    public int OperationalProjectId { get; set; }
    public OperationalProject OperationalProject { get; set; } = null!;
    public int? ProjectBoqLineId { get; set; }
    public ProjectBoqLine? ProjectBoqLine { get; set; }
    public string Code { get; set; } = string.Empty;
    public MaterialAlertType Type { get; set; }
    public MaterialAlertStatus Status { get; set; } = MaterialAlertStatus.Open;
    public MaterialAlertSeverity Severity { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal BoqAllowance { get; set; }
    public decimal RequiredQuantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal IssuedQuantity { get; set; }
    public decimal OnHandQuantity { get; set; }
    public decimal VarianceQuantity { get; set; }
    public string SourceEntityType { get; set; } = string.Empty;
    public int SourceEntityId { get; set; }
    public int AssignedToUserId { get; set; }
    public ApplicationUser AssignedToUser { get; set; } = null!;
    public DateTime DetectedAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public int? AcknowledgedByUserId { get; set; }
    public ApplicationUser? AcknowledgedBy { get; set; }
    public string? AcknowledgementNote { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime LastEvaluatedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = [];
    public List<MaterialAlertEvent> Events { get; set; } = [];
}

public sealed class MaterialAlertEvent
{
    public int Id { get; set; }
    public int MaterialAlertId { get; set; }
    public MaterialAlert MaterialAlert { get; set; } = null!;
    public MaterialAlertEventType Type { get; set; }
    public MaterialAlertStatus? FromStatus { get; set; }
    public MaterialAlertStatus ToStatus { get; set; }
    public string? Reason { get; set; }
    public string SnapshotJson { get; set; } = "{}";
    public int ChangedByUserId { get; set; }
    public ApplicationUser ChangedBy { get; set; } = null!;
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}

public enum MaterialAlertType
{
    OverBoq = 0,
    Shortage = 1,
}

public enum MaterialAlertStatus
{
    Open = 0,
    Acknowledged = 1,
    Resolved = 2,
}

public enum MaterialAlertSeverity
{
    Warning = 0,
    Critical = 1,
}

public enum MaterialAlertEventType
{
    Detected = 0,
    MetricsUpdated = 1,
    Acknowledged = 2,
    AutoResolved = 3,
    Reopened = 4,
}
