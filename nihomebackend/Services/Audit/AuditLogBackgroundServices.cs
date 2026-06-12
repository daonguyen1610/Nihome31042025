using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;

namespace NihomeBackend.Services.Audit;

public sealed class AuditLogWriterService(
    AuditLogQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<AuditLogWriterService> logger) : BackgroundService
{
    private const int BatchSize = 200;
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reader = queue.Channel.Reader;
        var buffer = new List<AuditLogEntry>(BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var flushCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                flushCts.CancelAfter(FlushInterval);

                try
                {
                    while (buffer.Count < BatchSize && await reader.WaitToReadAsync(flushCts.Token))
                    {
                        while (buffer.Count < BatchSize && reader.TryRead(out var entry))
                        {
                            buffer.Add(entry);
                        }
                    }
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    // flush interval elapsed
                }

                if (buffer.Count > 0)
                {
                    await PersistAsync(buffer, stoppingToken);
                    buffer.Clear();
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "AuditLogWriter loop error");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        if (buffer.Count > 0)
        {
            try { await PersistAsync(buffer, CancellationToken.None); }
            catch (Exception ex) { logger.LogError(ex, "Final audit flush failed"); }
        }
    }

    private async Task PersistAsync(List<AuditLogEntry> entries, CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ChangeTracker.AutoDetectChangesEnabled = false;
            var rows = entries.Select(e => new AuditLog
            {
                AuditId = e.AuditId,
                CreatedAt = e.CreatedAt,
                Action = e.Action,
                ResourceType = e.ResourceType,
                ResourceId = e.ResourceId,
                Message = e.Message,
                ActorUserId = e.ActorUserId,
                ActorPhone = e.ActorPhone,
                ActorRole = e.ActorRole,
                ActorType = e.ActorType,
                SourceSystem = e.SourceSystem,
                TargetSystem = e.TargetSystem,
                Channel = e.Channel,
                IpAddress = e.IpAddress,
                UserAgent = e.UserAgent,
                Status = e.Status,
                FailureReason = e.FailureReason,
                CorrelationId = e.CorrelationId,
                RequestId = e.RequestId,
                OldValueJson = e.OldValueJson,
                NewValueJson = e.NewValueJson,
                MetadataJson = e.MetadataJson,
            }).ToList();
            await db.AuditLogs.AddRangeAsync(rows, ct);
            await db.SaveChangesAsync(ct);
            logger.LogDebug("Persisted {Count} audit entries", rows.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to persist {Count} audit entries", entries.Count);
        }
    }
}

public sealed class AuditLogRetentionService(
    IServiceScopeFactory scopeFactory,
    ILogger<AuditLogRetentionService> logger) : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(30);
    private const int DeleteBatchSize = 1_000;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial small delay so app startup is not blocked.
        try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "AuditLogRetention sweep error");
            }

            try { await Task.Delay(SweepInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await db.SiteSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var minutes = settings?.AuditLogRetentionMinutes ?? 0;
        if (minutes <= 0) return;

        var threshold = DateTime.UtcNow.AddMinutes(-minutes);
        int totalDeleted = 0;
        while (!ct.IsCancellationRequested)
        {
            var oldestIds = await db.AuditLogs
                .Where(a => a.CreatedAt < threshold)
                .OrderBy(a => a.Id)
                .Select(a => a.Id)
                .Take(DeleteBatchSize)
                .ToListAsync(ct);

            if (oldestIds.Count == 0) break;

            var deleted = await db.AuditLogs
                .Where(a => oldestIds.Contains(a.Id))
                .ExecuteDeleteAsync(ct);
            totalDeleted += deleted;
            if (deleted < DeleteBatchSize) break;
        }

        if (totalDeleted > 0)
        {
            logger.LogInformation("Audit retention deleted {Count} rows older than {Threshold}", totalDeleted, threshold);
        }
    }
}

/// <summary>
/// Drains the transactional outbox: reads unprocessed <c>AuditOutbox</c> rows,
/// promotes them into <c>audit_logs</c>, and deletes the outbox row. Safe under
/// multiple replicas because <c>audit_logs.AuditId</c> is unique — a duplicate
/// promotion is detected via <see cref="DbUpdateException"/> and treated as
/// already-processed.
/// </summary>
public sealed class AuditOutboxDrainService(
    IServiceScopeFactory scopeFactory,
    ILogger<AuditOutboxDrainService> logger) : BackgroundService
{
    private const int BatchSize = 200;
    private const int MaxAttempts = 10;
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ErrorBackoff = TimeSpan.FromSeconds(5);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Small initial delay so startup is not blocked.
        try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            int drained;
            try
            {
                drained = await DrainOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "AuditOutboxDrain loop error");
                try { await Task.Delay(ErrorBackoff, stoppingToken); } catch { break; }
                continue;
            }

            if (drained == 0)
            {
                try { await Task.Delay(IdleDelay, stoppingToken); } catch { break; }
            }
        }
    }

    internal async Task<int> DrainOnceAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ChangeTracker.AutoDetectChangesEnabled = false;

        var rows = await db.AuditOutbox
            .OrderBy(o => o.Id)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (rows.Count == 0) return 0;

        var newLogs = new List<AuditLog>(rows.Count);
        var deletableIds = new List<long>(rows.Count);
        var faultedIds = new List<long>();

        foreach (var row in rows)
        {
            try
            {
                var entry = JsonSerializer.Deserialize<AuditLogEntry>(row.Payload, JsonOptions)
                            ?? throw new InvalidOperationException("payload deserialized to null");

                newLogs.Add(new AuditLog
                {
                    AuditId = entry.AuditId,
                    CreatedAt = entry.CreatedAt,
                    Action = entry.Action,
                    ResourceType = entry.ResourceType,
                    ResourceId = entry.ResourceId,
                    Message = entry.Message,
                    ActorUserId = entry.ActorUserId,
                    ActorPhone = entry.ActorPhone,
                    ActorRole = entry.ActorRole,
                    ActorType = entry.ActorType,
                    SourceSystem = entry.SourceSystem,
                    TargetSystem = entry.TargetSystem,
                    Channel = entry.Channel,
                    IpAddress = entry.IpAddress,
                    UserAgent = entry.UserAgent,
                    Status = entry.Status,
                    FailureReason = entry.FailureReason,
                    CorrelationId = entry.CorrelationId,
                    RequestId = entry.RequestId,
                    OldValueJson = entry.OldValueJson,
                    NewValueJson = entry.NewValueJson,
                    MetadataJson = entry.MetadataJson,
                });
                deletableIds.Add(row.Id);
            }
            catch (Exception ex)
            {
                // Bad payload: mark faulted so it doesn't block the queue.
                logger.LogError(ex, "Outbox row {Id} payload is unreadable; marking faulted", row.Id);
                row.Attempts++;
                row.LastError = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
                faultedIds.Add(row.Id);
            }
        }

        // Persist faulted rows (Attempts/LastError). Drop those that exceeded MaxAttempts.
        if (faultedIds.Count > 0)
        {
            db.ChangeTracker.DetectChanges();
            await db.SaveChangesAsync(ct);
            var dead = await db.AuditOutbox
                .Where(o => faultedIds.Contains(o.Id) && o.Attempts >= MaxAttempts)
                .ToListAsync(ct);
            if (dead.Count > 0)
            {
                db.AuditOutbox.RemoveRange(dead);
                await db.SaveChangesAsync(ct);
            }
        }

        if (newLogs.Count == 0) return rows.Count;

        // Pre-filter rows whose AuditId is already in audit_logs (idempotency
        // across replicas / re-runs). Works on every EF provider, unlike
        // relying on a unique-constraint violation.
        var candidateIds = newLogs.Select(l => l.AuditId).ToList();
        var existing = await db.AuditLogs
            .Where(l => candidateIds.Contains(l.AuditId))
            .Select(l => l.AuditId)
            .ToListAsync(ct);
        if (existing.Count > 0)
        {
            var existingSet = existing.ToHashSet(StringComparer.Ordinal);
            newLogs = newLogs.Where(l => !existingSet.Contains(l.AuditId)).ToList();
        }

        if (newLogs.Count > 0)
        {
            try
            {
                await db.AuditLogs.AddRangeAsync(newLogs, ct);
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
            {
                // Race with another replica that promoted the same AuditId
                // between our pre-filter and SaveChanges. Fall back per-row.
                logger.LogWarning(ex, "Batch insert into audit_logs failed; falling back to per-row");
                foreach (var log in newLogs)
                {
                    db.Entry(log).State = EntityState.Detached;
                }
                using var perRow = scopeFactory.CreateScope();
                var freshDb = perRow.ServiceProvider.GetRequiredService<AppDbContext>();
                freshDb.ChangeTracker.AutoDetectChangesEnabled = false;
                foreach (var log in newLogs)
                {
                    try
                    {
                        freshDb.AuditLogs.Add(log);
                        await freshDb.SaveChangesAsync(ct);
                        freshDb.Entry(log).State = EntityState.Detached;
                    }
                    catch (DbUpdateException dup)
                    {
                        logger.LogDebug(dup, "AuditId {AuditId} already in audit_logs; treating as processed", log.AuditId);
                        freshDb.Entry(log).State = EntityState.Detached;
                    }
                }
            }
        }

        // Delete the outbox rows that we successfully promoted (or were duplicates).
        if (deletableIds.Count > 0)
        {
            var toDelete = await db.AuditOutbox
                .Where(o => deletableIds.Contains(o.Id))
                .ToListAsync(ct);
            db.AuditOutbox.RemoveRange(toDelete);
            await db.SaveChangesAsync(ct);
        }

        logger.LogDebug("Drained {Count} outbox rows ({Faulted} faulted)", deletableIds.Count, faultedIds.Count);
        return rows.Count;
    }
}
