using System.Security.Claims;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http;
using NihomeBackend.Data;
using NihomeBackend.Models;

namespace NihomeBackend.Services.Audit;

public sealed class AuditLogQueue
{
    public Channel<AuditLogEntry> Channel { get; } =
        System.Threading.Channels.Channel.CreateBounded<AuditLogEntry>(
            new BoundedChannelOptions(capacity: 10_000)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
            });
}

public sealed class AuditLogger(
    AuditLogQueue queue,
    IHttpContextAccessor accessor,
    ILogger<AuditLogger> logger) : IAuditLogger
{
    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public void Log(string action, string resourceType, string? resourceId, string message)
        => Log(new AuditEvent
        {
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            Message = message,
        });

    public void Log(AuditEvent evt)
    {
        try
        {
            var entry = BuildEntry(evt);
            if (!queue.Channel.Writer.TryWrite(entry))
            {
                logger.LogWarning("Audit log queue rejected entry {Action}/{ResourceType}",
                    evt.Action, evt.ResourceType);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to enqueue audit log {Action}", evt.Action);
        }
    }

    public void LogTransactional(AuditEvent evt, AppDbContext db)
    {
        try
        {
            var entry = BuildEntry(evt);
            var row = new AuditOutbox
            {
                AuditId = entry.AuditId,
                CreatedAt = entry.CreatedAt,
                Payload = JsonSerializer.Serialize(entry, PayloadOptions),
            };
            db.AuditOutbox.Add(row);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to enqueue transactional audit log {Action}", evt.Action);
        }
    }

    internal AuditLogEntry BuildEntry(AuditEvent evt)
    {
        var ctx = accessor.HttpContext;
        int? userId = null;
        string? phone = null;
        string? role = null;
        string? ip = null;
        string? ua = null;
        string? correlationId = null;
        string? requestId = null;
        var actorType = AuditActorType.Anonymous;

        if (ctx is not null)
        {
            var user = ctx.User;
            if (user?.Identity?.IsAuthenticated == true)
            {
                var uidStr = user.FindFirst("uid")?.Value
                             ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(uidStr, out var uid)) userId = uid;
                phone = user.FindFirst("phone")?.Value;
                role = user.FindFirst(ClaimTypes.Role)?.Value;
                actorType = AuditActorType.User;
            }

            ip = ResolveIp(ctx);
            ua = TrimNullable(ctx.Request.Headers.UserAgent.ToString(), 300);
            correlationId = ResolveCorrelationId(ctx);
            requestId = TrimNullable(ctx.TraceIdentifier, 80);
        }
        else
        {
            actorType = AuditActorType.System;
        }

        return new AuditLogEntry
        {
            CreatedAt = DateTime.UtcNow,
            Action = Trim(evt.Action, 100),
            ResourceType = Trim(evt.ResourceType, 80),
            ResourceId = TrimNullable(evt.ResourceId, 100),
            Message = Trim(evt.Message, 500),
            ActorUserId = userId,
            ActorPhone = TrimNullable(phone, 30),
            ActorRole = TrimNullable(role, 30),
            ActorType = actorType,
            SourceSystem = "nihomebackend",
            TargetSystem = TrimNullable(evt.TargetSystem, 40),
            Channel = Trim(evt.Channel, 20),
            IpAddress = ip,
            UserAgent = ua,
            Status = Trim(string.IsNullOrWhiteSpace(evt.Status) ? AuditStatus.Success : evt.Status, 20),
            FailureReason = TrimNullable(evt.FailureReason, 500),
            CorrelationId = correlationId,
            RequestId = requestId,
            OldValueJson = SensitiveDataMasker.Serialize(evt.OldValue),
            NewValueJson = SensitiveDataMasker.Serialize(evt.NewValue),
            MetadataJson = SensitiveDataMasker.Serialize(evt.Metadata),
        };
    }

    private static string? ResolveCorrelationId(HttpContext ctx)
    {
        var header = ctx.Request.Headers["X-Correlation-Id"].FirstOrDefault()
                     ?? ctx.Request.Headers["X-Request-Id"].FirstOrDefault()
                     ?? ctx.Request.Headers["traceparent"].FirstOrDefault();
        return TrimNullable(string.IsNullOrWhiteSpace(header) ? ctx.TraceIdentifier : header, 80);
    }

    private static string? ResolveIp(HttpContext ctx)
    {
        var forwarded = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            var first = forwarded.Split(',')[0].Trim();
            return TrimNullable(first, 64);
        }
        return TrimNullable(ctx.Connection.RemoteIpAddress?.ToString(), 64);
    }

    private static string Trim(string value, int max) =>
        value.Length <= max ? value : value[..max];

    private static string? TrimNullable(string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) return null;
        return value.Length <= max ? value : value[..max];
    }
}
