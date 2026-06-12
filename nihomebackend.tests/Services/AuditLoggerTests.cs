using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NihomeBackend.Services.Audit;

namespace nihomebackend.tests.Services;

public class AuditLoggerTests
{
    private static (AuditLogger logger, AuditLogQueue queue, HttpContextAccessor accessor) Build()
    {
        var queue = new AuditLogQueue();
        var accessor = new HttpContextAccessor();
        var logger = new AuditLogger(queue, accessor, NullLogger<AuditLogger>.Instance);
        return (logger, queue, accessor);
    }

    private static AuditLogEntry ReadOne(AuditLogQueue queue)
    {
        Assert.True(queue.Channel.Reader.TryRead(out var entry));
        return entry!;
    }

    [Fact]
    public void Log_QuickHelper_EnqueuesEntryWithDefaults()
    {
        var (sut, queue, _) = Build();

        sut.Log("test.action", "Resource", "42", "happy path");
        var entry = ReadOne(queue);

        Assert.Equal("test.action", entry.Action);
        Assert.Equal("Resource", entry.ResourceType);
        Assert.Equal("42", entry.ResourceId);
        Assert.Equal("happy path", entry.Message);
        Assert.Equal(AuditStatus.Success, entry.Status);
        Assert.Equal(AuditActorType.System, entry.ActorType); // no HttpContext
        Assert.Equal("nihomebackend", entry.SourceSystem);
        Assert.False(string.IsNullOrEmpty(entry.AuditId));
    }

    [Fact]
    public void Log_NoHttpContext_MarksActorAsSystem()
    {
        var (sut, queue, _) = Build();

        sut.Log(new AuditEvent { Action = "system.tick", ResourceType = "Job" });
        var entry = ReadOne(queue);

        Assert.Equal(AuditActorType.System, entry.ActorType);
        Assert.Null(entry.ActorUserId);
        Assert.Null(entry.ActorPhone);
        Assert.Null(entry.ActorRole);
    }

    [Fact]
    public void Log_AuthenticatedUser_PopulatesActorFromClaims()
    {
        var (sut, queue, accessor) = Build();
        var ctx = new DefaultHttpContext();
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("uid", "123"),
            new Claim("phone", "0335240370"),
            new Claim(ClaimTypes.Role, "SUPER_ADMIN"),
        }, authenticationType: "test"));
        ctx.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.5");
        ctx.Request.Headers.UserAgent = "xunit/1.0";
        accessor.HttpContext = ctx;

        sut.Log(new AuditEvent
        {
            Action = "user.login",
            ResourceType = "User",
            ResourceId = "123",
            Message = "ok",
        });
        var entry = ReadOne(queue);

        Assert.Equal(AuditActorType.User, entry.ActorType);
        Assert.Equal(123, entry.ActorUserId);
        Assert.Equal("0335240370", entry.ActorPhone);
        Assert.Equal("SUPER_ADMIN", entry.ActorRole);
        Assert.Equal("10.0.0.5", entry.IpAddress);
        Assert.Equal("xunit/1.0", entry.UserAgent);
        Assert.False(string.IsNullOrEmpty(entry.CorrelationId));
        Assert.False(string.IsNullOrEmpty(entry.RequestId));
    }

    [Fact]
    public void Log_PrefersXForwardedFor_OverRemoteIp()
    {
        var (sut, queue, accessor) = Build();
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.5");
        ctx.Request.Headers["X-Forwarded-For"] = "203.0.113.7, 10.0.0.1";
        accessor.HttpContext = ctx;

        sut.Log(new AuditEvent { Action = "x", ResourceType = "y" });
        var entry = ReadOne(queue);

        Assert.Equal("203.0.113.7", entry.IpAddress);
    }

    [Fact]
    public void Log_UsesExplicitCorrelationIdHeader()
    {
        var (sut, queue, accessor) = Build();
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["X-Correlation-Id"] = "corr-abc-123";
        accessor.HttpContext = ctx;

        sut.Log(new AuditEvent { Action = "x", ResourceType = "y" });
        var entry = ReadOne(queue);

        Assert.Equal("corr-abc-123", entry.CorrelationId);
    }

    [Fact]
    public void Log_MasksSensitiveFieldsInPayloads()
    {
        var (sut, queue, _) = Build();
        var oldVal = new { password = "old" };
        var newVal = new { password = "new", phone = "0335240370" };

        sut.Log(new AuditEvent
        {
            Action = "user.update",
            ResourceType = "User",
            OldValue = oldVal,
            NewValue = newVal,
        });
        var entry = ReadOne(queue);

        Assert.NotNull(entry.OldValueJson);
        Assert.NotNull(entry.NewValueJson);
        Assert.Contains("\"password\":\"***\"", entry.OldValueJson);
        Assert.Contains("\"password\":\"***\"", entry.NewValueJson);
        Assert.Contains("\"phone\":\"0335240370\"", entry.NewValueJson);
    }

    [Fact]
    public void Log_FailureEvent_RecordsStatusAndReason()
    {
        var (sut, queue, _) = Build();

        sut.Log(new AuditEvent
        {
            Action = "auth.login",
            ResourceType = "User",
            Status = AuditStatus.Failure,
            FailureReason = "invalid_credentials",
            Metadata = new { phoneNumber = "0000" },
        });
        var entry = ReadOne(queue);

        Assert.Equal(AuditStatus.Failure, entry.Status);
        Assert.Equal("invalid_credentials", entry.FailureReason);
        Assert.NotNull(entry.MetadataJson);
        using var doc = JsonDocument.Parse(entry.MetadataJson!);
        Assert.Equal("0000", doc.RootElement.GetProperty("phoneNumber").GetString());
    }

    [Fact]
    public void Log_TrimsOversizedStrings()
    {
        var (sut, queue, _) = Build();
        var hugeMessage = new string('x', 1000);

        sut.Log(new AuditEvent
        {
            Action = new string('a', 250), // > 100
            ResourceType = new string('b', 200), // > 80
            ResourceId = new string('c', 300), // > 100
            Message = hugeMessage, // > 500
        });
        var entry = ReadOne(queue);

        Assert.Equal(100, entry.Action.Length);
        Assert.Equal(80, entry.ResourceType.Length);
        Assert.Equal(100, entry.ResourceId!.Length);
        Assert.Equal(500, entry.Message.Length);
    }
}
