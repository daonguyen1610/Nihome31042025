namespace NihomeBackend.Models;

/// <summary>
/// Transactional outbox row for audit events. Written inside the same
/// SaveChangesAsync as the business mutation so a successful business commit
/// implies the audit record is durable. A background drain worker
/// (<c>AuditOutboxDrainService</c>) moves rows into <c>audit_logs</c> and
/// deletes the outbox row. Idempotent via the unique <c>AuditId</c> index on
/// <c>audit_logs</c>.
/// </summary>
public class AuditOutbox
{
    public long Id { get; set; }
    public string AuditId { get; set; } = string.Empty;        // matches AuditLog.AuditId
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // enqueue time
    public string Payload { get; set; } = string.Empty;        // JSON of AuditLogEntry
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}
