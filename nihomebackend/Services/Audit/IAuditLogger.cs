namespace NihomeBackend.Services.Audit;

/// <summary>Outcome of an audited operation.</summary>
public static class AuditStatus
{
    public const string Success = "success";
    public const string Failure = "failure";
    public const string Denied = "denied";
}

/// <summary>Classification of the actor performing the action.</summary>
public static class AuditActorType
{
    public const string User = "user";
    public const string System = "system";
    public const string Service = "service";
    public const string Anonymous = "anonymous";
}

/// <summary>
/// Snapshot describing one audited operation. Built by the controller / service
/// and pushed onto the in-process queue. Sensitive fields in the JSON payloads
/// are masked at serialization time by <c>SensitiveDataMasker</c>.
/// </summary>
public sealed record AuditLogEntry
{
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public string AuditId { get; init; } = Guid.NewGuid().ToString("N");

    public string Action { get; init; } = string.Empty;
    public string ResourceType { get; init; } = string.Empty;
    public string? ResourceId { get; init; }
    public string Message { get; init; } = string.Empty;

    public int? ActorUserId { get; init; }
    public string? ActorPhone { get; init; }
    public string? ActorRole { get; init; }
    public string ActorType { get; init; } = AuditActorType.User;

    public string SourceSystem { get; init; } = "nihomebackend";
    public string? TargetSystem { get; init; }
    public string Channel { get; init; } = "http";

    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }

    public string Status { get; init; } = AuditStatus.Success;
    public string? FailureReason { get; init; }

    public string? CorrelationId { get; init; }
    public string? RequestId { get; init; }

    public string? OldValueJson { get; init; }
    public string? NewValueJson { get; init; }
    public string? MetadataJson { get; init; }
}

/// <summary>
/// Fluent options bag used by <see cref="IAuditLogger.Log(AuditEvent)"/>. Keeps
/// the surface area at call sites small while supporting the full schema.
/// </summary>
public sealed class AuditEvent
{
    public string Action { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string? ResourceId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Status { get; set; } = AuditStatus.Success;
    public string? FailureReason { get; set; }
    public string? TargetSystem { get; set; }
    public string Channel { get; set; } = "http";
    public object? OldValue { get; set; }
    public object? NewValue { get; set; }
    public object? Metadata { get; set; }
}

public interface IAuditLogger
{
    /// <summary>Backwards-compatible quick-log helper.</summary>
    void Log(string action, string resourceType, string? resourceId, string message);

    /// <summary>Full-fidelity log call. Never throws.</summary>
    void Log(AuditEvent evt);
}
