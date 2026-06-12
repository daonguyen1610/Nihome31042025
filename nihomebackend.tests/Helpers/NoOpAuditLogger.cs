using NihomeBackend.Services.Audit;

namespace nihomebackend.tests.Helpers;

/// <summary>No-op IAuditLogger for controller unit tests.</summary>
public sealed class NoOpAuditLogger : IAuditLogger
{
    public void Log(string action, string resourceType, string? resourceId, string message) { }
    public void Log(AuditEvent evt) { }
}
