namespace NihomeBackend.Models;

using System.ComponentModel.DataAnnotations.Schema;

/// <summary>
/// Append-only audit record. Never updated in place; removed only by retention
/// sweep or explicit SUPER_ADMIN action (which is itself audited).
/// </summary>
public class AuditLog
{
    // ── Identity / time
    public long Id { get; set; }
    public string AuditId { get; set; } = string.Empty;   // GUID, externally referenceable
    public DateTime CreatedAt { get; set; }                // UTC

    // ── Actor (Who)
    public int? ActorUserId { get; set; }
    public string? ActorPhone { get; set; }
    public string? ActorRole { get; set; }
    public string ActorType { get; set; } = "user";        // user | system | service | anonymous

    // ── Action (What)
    public string Action { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string? ResourceId { get; set; }
    public string Message { get; set; } = string.Empty;

    // ── Context (Where / How)
    public string SourceSystem { get; set; } = "nihomebackend";
    public string? TargetSystem { get; set; }
    public string Channel { get; set; } = "http";          // http | grpc | job | cli
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    // ── Result
    public string Status { get; set; } = "success";        // success | failure | denied
    public string? FailureReason { get; set; }

    // ── Correlation
    public string? CorrelationId { get; set; }             // W3C trace id / batch id
    public string? RequestId { get; set; }                 // HttpContext.TraceIdentifier

    // ── Payload (JSON; sensitive fields masked at write-time)
    public string? OldValueJson { get; set; }
    public string? NewValueJson { get; set; }
    public string? MetadataJson { get; set; }

    // ── Legacy aliases kept for back-compat with existing rows / queries
    [NotMapped]
    public string EntityType
    {
        get => ResourceType;
        set => ResourceType = value;
    }
    [NotMapped]
    public string? EntityId
    {
        get => ResourceId;
        set => ResourceId = value;
    }
}
