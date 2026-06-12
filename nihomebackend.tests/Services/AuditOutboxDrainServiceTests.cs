using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Services.Audit;

namespace nihomebackend.tests.Services;

public class AuditOutboxDrainServiceTests
{
    private static (AuditOutboxDrainService svc, IServiceProvider sp, string dbName) BuildHarness()
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));
        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        var svc = new AuditOutboxDrainService(scopeFactory,
            NullLogger<AuditOutboxDrainService>.Instance);
        return (svc, sp, dbName);
    }

    private static AuditOutbox MakeRow(string auditId, string action = "process.create")
    {
        var entry = new AuditLogEntry
        {
            AuditId = auditId,
            CreatedAt = DateTime.UtcNow,
            Action = action,
            ResourceType = "ProcessDocument",
            ResourceId = "1",
            Message = "ok",
            Status = AuditStatus.Success,
        };
        return new AuditOutbox
        {
            AuditId = entry.AuditId,
            CreatedAt = entry.CreatedAt,
            Payload = JsonSerializer.Serialize(entry),
        };
    }

    [Fact]
    public async Task DrainOnce_PromotesRows_AndDeletesOutbox()
    {
        var (svc, sp, _) = BuildHarness();
        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AuditOutbox.AddRange(
                MakeRow("a-1"),
                MakeRow("a-2"),
                MakeRow("a-3"));
            await db.SaveChangesAsync();
        }

        var drained = await svc.DrainOnceAsync(CancellationToken.None);

        Assert.Equal(3, drained);
        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(3, db.AuditLogs.Count());
            Assert.Empty(db.AuditOutbox);
        }
    }

    [Fact]
    public async Task DrainOnce_IsIdempotent_WhenAuditLogAlreadyExists()
    {
        var (svc, sp, _) = BuildHarness();

        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AuditLogs.Add(new AuditLog
            {
                AuditId = "dup-1",
                CreatedAt = DateTime.UtcNow,
                Action = "process.create",
                ResourceType = "ProcessDocument",
                Status = AuditStatus.Success,
                ActorType = AuditActorType.System,
            });
            await db.SaveChangesAsync();
            db.AuditOutbox.Add(MakeRow("dup-1"));
            db.AuditOutbox.Add(MakeRow("fresh-1"));
            await db.SaveChangesAsync();
        }

        await svc.DrainOnceAsync(CancellationToken.None);

        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // Both outbox rows removed; audit_logs has dup-1 + fresh-1 (no duplicates).
            Assert.Empty(db.AuditOutbox);
            Assert.Equal(2, db.AuditLogs.Count());
            Assert.Contains(db.AuditLogs, l => l.AuditId == "dup-1");
            Assert.Contains(db.AuditLogs, l => l.AuditId == "fresh-1");
        }
    }

    [Fact]
    public async Task DrainOnce_ReturnsZero_WhenEmpty()
    {
        var (svc, _, _) = BuildHarness();
        var drained = await svc.DrainOnceAsync(CancellationToken.None);
        Assert.Equal(0, drained);
    }
}
