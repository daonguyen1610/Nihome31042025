using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Authorization;
using NihomeBackend.Data;
using NihomeBackend.Services.Audit;

namespace NihomeBackend.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Route("api/v1/audit-logs")]
[Authorize]
[RequirePermission("system.audit", "view")]
public class AuditLogsController(
    AppDbContext db,
    IAuditLogger audit) : ControllerBase
{
    public sealed class AuditLogItem
    {
        public long Id { get; set; }
        public string AuditId { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public string Action { get; set; } = "";
        public string ResourceType { get; set; } = "";
        public string? ResourceId { get; set; }
        public string Message { get; set; } = "";
        public int? ActorUserId { get; set; }
        public string? ActorPhone { get; set; }
        public string? ActorRole { get; set; }
        public string ActorType { get; set; } = "";
        public string SourceSystem { get; set; } = "";
        public string? TargetSystem { get; set; }
        public string Channel { get; set; } = "";
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public string Status { get; set; } = "";
        public string? FailureReason { get; set; }
        public string? CorrelationId { get; set; }
        public string? RequestId { get; set; }
        public string? OldValueJson { get; set; }
        public string? NewValueJson { get; set; }
        public string? MetadataJson { get; set; }
    }

    public sealed class AuditLogPage
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int Total { get; set; }
        public List<string> Actions { get; set; } = [];
        public List<AuditLogItem> Items { get; set; } = [];
    }

    public sealed class AuditConfigDto
    {
        public int RetentionMinutes { get; set; }
    }

    [HttpGet]
    public async Task<ActionResult<AuditLogPage>> Get(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? action,
        [FromQuery] string? actorPhone,
        [FromQuery] string? ip,
        [FromQuery] string? status,
        [FromQuery] string? resourceType,
        [FromQuery] string? resourceId,
        [FromQuery] string? correlationId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;
        if (pageSize > 200) pageSize = 200;

        var q = db.AuditLogs.AsNoTracking().AsQueryable();
        if (from.HasValue) q = q.Where(a => a.CreatedAt >= from.Value);
        if (to.HasValue) q = q.Where(a => a.CreatedAt < to.Value);
        if (!string.IsNullOrWhiteSpace(action)) q = q.Where(a => a.Action == action);
        if (!string.IsNullOrWhiteSpace(actorPhone)) q = q.Where(a => a.ActorPhone == actorPhone);
        if (!string.IsNullOrWhiteSpace(ip)) q = q.Where(a => a.IpAddress != null && a.IpAddress.Contains(ip));
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(a => a.Status == status);
        if (!string.IsNullOrWhiteSpace(resourceType)) q = q.Where(a => a.ResourceType == resourceType);
        if (!string.IsNullOrWhiteSpace(resourceId)) q = q.Where(a => a.ResourceId == resourceId);
        if (!string.IsNullOrWhiteSpace(correlationId)) q = q.Where(a => a.CorrelationId == correlationId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(a =>
                a.Message.Contains(s) ||
                a.Action.Contains(s) ||
                a.ResourceType.Contains(s) ||
                (a.ActorPhone != null && a.ActorPhone.Contains(s)) ||
                (a.ResourceId != null && a.ResourceId.Contains(s)) ||
                (a.CorrelationId != null && a.CorrelationId.Contains(s)));
        }

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogItem
            {
                Id = a.Id,
                AuditId = a.AuditId,
                CreatedAt = a.CreatedAt,
                Action = a.Action,
                ResourceType = a.ResourceType,
                ResourceId = a.ResourceId,
                Message = a.Message,
                ActorUserId = a.ActorUserId,
                ActorPhone = a.ActorPhone,
                ActorRole = a.ActorRole,
                ActorType = a.ActorType,
                SourceSystem = a.SourceSystem,
                TargetSystem = a.TargetSystem,
                Channel = a.Channel,
                IpAddress = a.IpAddress,
                UserAgent = a.UserAgent,
                Status = a.Status,
                FailureReason = a.FailureReason,
                CorrelationId = a.CorrelationId,
                RequestId = a.RequestId,
                OldValueJson = a.OldValueJson,
                NewValueJson = a.NewValueJson,
                MetadataJson = a.MetadataJson,
            })
            .ToListAsync();

        var actions = await db.AuditLogs.AsNoTracking()
            .Select(a => a.Action)
            .Distinct()
            .OrderBy(a => a)
            .Take(100)
            .ToListAsync();

        return Ok(new AuditLogPage
        {
            Page = page,
            PageSize = pageSize,
            Total = total,
            Items = items,
            Actions = actions,
        });
    }

    [HttpDelete("{id:long}")]
    [RequirePermission("system.audit", "manage")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct = default)
    {
        var deleted = await db.AuditLogs.Where(a => a.Id == id).ExecuteDeleteAsync(ct);
        if (deleted == 0) return NotFound();
        audit.Log("audit.delete", "AuditLog", id.ToString(), $"Deleted audit log entry {id}");
        return NoContent();
    }

    [HttpDelete]
    [RequirePermission("system.audit", "manage")]
    public async Task<ActionResult<object>> DeleteRange(
        [FromQuery] DateTime? before,
        [FromQuery] string? action,
        CancellationToken ct = default)
    {
        var q = db.AuditLogs.AsQueryable();
        if (before.HasValue) q = q.Where(a => a.CreatedAt < before.Value);
        if (!string.IsNullOrWhiteSpace(action)) q = q.Where(a => a.Action == action);

        if (!before.HasValue && string.IsNullOrWhiteSpace(action))
        {
            return BadRequest(new { message = "Provide 'before' or 'action' filter." });
        }

        var deleted = await q.ExecuteDeleteAsync(ct);
        audit.Log("audit.delete_range", "AuditLog", null,
            $"Deleted {deleted} audit entries (before={before:o}, action={action})");
        return Ok(new { deleted });
    }

    [HttpGet("config")]
    public async Task<ActionResult<AuditConfigDto>> GetConfig(CancellationToken ct = default)
    {
        var s = await db.SiteSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        return Ok(new AuditConfigDto { RetentionMinutes = s?.AuditLogRetentionMinutes ?? 0 });
    }

    [HttpPut("config")]
    [RequirePermission("system.audit", "manage")]
    public async Task<ActionResult<AuditConfigDto>> UpdateConfig(
        [FromBody] AuditConfigDto body,
        CancellationToken ct = default)
    {
        if (body.RetentionMinutes < 0) return BadRequest(new { message = "RetentionMinutes must be >= 0" });

        var s = await db.SiteSettings.FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("SiteSettings not initialized.");
        s.AuditLogRetentionMinutes = body.RetentionMinutes;
        s.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        audit.Log("audit.config_update", "AuditLog", null,
            $"Set audit retention to {body.RetentionMinutes} minutes");
        return Ok(new AuditConfigDto { RetentionMinutes = s.AuditLogRetentionMinutes });
    }
}
