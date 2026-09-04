namespace NihomeBackend.Models;

public enum HardDeleteOperationStatus
{
    Preparing,
    Ready,
    Processing,
    Completed,
    ManualActionRequired,
    Failed,
}

public enum HardDeleteItemKind
{
    LocalFile,
    DriveFile,
    DriveFolder,
    DatabaseAggregate,
}

public enum HardDeleteItemStatus
{
    Pending,
    Quarantined,
    Completed,
    ManualActionRequired,
    Failed,
}

public sealed class HardDeleteOperation
{
    public Guid Id { get; set; }
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceLabel { get; set; } = string.Empty;
    public string PlanToken { get; set; } = string.Empty;
    public string Confirmation { get; set; } = string.Empty;
    public HardDeleteOperationStatus Status { get; set; }
    public string RequestedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public int AttemptCount { get; set; }
    public bool HasIrreversibleStep { get; set; }
    public string? LastErrorCode { get; set; }
    public string? LastErrorMessage { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public ICollection<HardDeleteItem> Items { get; set; } = [];
}

public sealed class HardDeleteItem
{
    public long Id { get; set; }
    public Guid OperationId { get; set; }
    public HardDeleteOperation Operation { get; set; } = null!;
    public HardDeleteItemKind Kind { get; set; }
    public HardDeleteItemStatus Status { get; set; }
    public string ActionIdentifier { get; set; } = string.Empty;
    public string? ExpectedParentId { get; set; }
    public string? ExpectedAppPropertiesJson { get; set; }
    public string? QuarantinePath { get; set; }
    public int Sequence { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? LastErrorCode { get; set; }
    public string? LastErrorMessage { get; set; }
}