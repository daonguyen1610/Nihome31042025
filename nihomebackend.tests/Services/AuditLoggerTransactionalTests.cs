using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NihomeBackend.Services.Audit;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public class AuditLoggerTransactionalTests
{
    [Fact]
    public async Task LogTransactional_AddsOutboxRow_OnSave()
    {
        using var db = DbContextFactory.Create();
        var sut = new AuditLogger(new AuditLogQueue(), new HttpContextAccessor(),
            NullLogger<AuditLogger>.Instance);

        sut.LogTransactional(new AuditEvent
        {
            Action = "process.create",
            ResourceType = "ProcessDocument",
            ResourceId = "42",
            Message = "test",
            NewValue = new { id = 42, title = "x" },
        }, db);

        // Before SaveChanges nothing is persisted.
        Assert.Equal(0, await CountAsync(db));

        await db.SaveChangesAsync();

        var rows = db.AuditOutbox.ToList();
        var row = Assert.Single(rows);
        Assert.False(string.IsNullOrEmpty(row.AuditId));
        Assert.False(string.IsNullOrEmpty(row.Payload));

        var entry = JsonSerializer.Deserialize<AuditLogEntry>(row.Payload)!;
        Assert.Equal("process.create", entry.Action);
        Assert.Equal("ProcessDocument", entry.ResourceType);
        Assert.Equal("42", entry.ResourceId);
        Assert.Equal(AuditStatus.Success, entry.Status);
        Assert.Equal(row.AuditId, entry.AuditId);
        Assert.False(string.IsNullOrEmpty(entry.NewValueJson));
    }

    [Fact]
    public async Task LogTransactional_RolledBack_LeavesNoRow()
    {
        using var db = DbContextFactory.Create();
        var sut = new AuditLogger(new AuditLogQueue(), new HttpContextAccessor(),
            NullLogger<AuditLogger>.Instance);

        sut.LogTransactional(new AuditEvent
        {
            Action = "x",
            ResourceType = "y",
        }, db);

        // Simulate caller failure: discard tracked changes instead of saving.
        foreach (var entry in db.ChangeTracker.Entries().ToList())
        {
            entry.State = Microsoft.EntityFrameworkCore.EntityState.Detached;
        }

        Assert.Equal(0, await CountAsync(db));
    }

    private static Task<int> CountAsync(NihomeBackend.Data.AppDbContext db)
        => Task.FromResult(db.AuditOutbox.Count());
}
