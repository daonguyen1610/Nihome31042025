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
