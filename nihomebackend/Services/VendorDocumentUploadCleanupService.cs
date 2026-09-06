using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Services.Audit;

namespace NihomeBackend.Services;

public interface IVendorDocumentUploadCleanupService
{
    Task<int> CleanupAsync(DateTime cutoffUtc, CancellationToken ct = default);
}

public sealed class VendorDocumentUploadCleanupService(
    AppDbContext db,
    IBusinessDocumentStorageService storage,
    IAuditLogger audit) : IVendorDocumentUploadCleanupService
{
    public async Task<int> CleanupAsync(DateTime cutoffUtc, CancellationToken ct = default)
    {
        var tokens = await db.VendorDocumentUploads.AsNoTracking()
            .Where(item => item.VendorId == null && item.CreatedAt < cutoffUtc)
            .OrderBy(item => item.CreatedAt).Take(100).Select(item => item.Token).ToListAsync(ct);
        var removed = 0;
        foreach (var token in tokens)
        {
            await using var transaction = db.Database.IsRelational()
                ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct)
                : null;
            var upload = await db.VendorDocumentUploads.SingleOrDefaultAsync(item => item.Token == token, ct);
            if (upload is null || upload.VendorId.HasValue || upload.CreatedAt >= cutoffUtc)
            {
                if (transaction is not null) await transaction.CommitAsync(ct);
                continue;
            }

            if (!storage.Delete(upload.Path, BusinessDocumentArea.Vendors))
            {
                if (transaction is not null) await transaction.RollbackAsync(ct);
                continue;
            }
            db.VendorDocumentUploads.Remove(upload);
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            audit.Log(new AuditEvent
            {
                Action = "vendor-document.expire",
                ResourceType = EntityTypes.Vendor,
                ResourceId = upload.Path,
                Message = "Expired unclaimed vendor capability document removed.",
            });
            removed++;
        }
        return removed;
    }
}

public sealed class VendorDocumentUploadCleanupWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<VendorDocumentUploadCleanupWorker> logger) : BackgroundService
{
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(6);
    private static readonly TimeSpan ClaimLifetime = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var cleanup = scope.ServiceProvider.GetRequiredService<IVendorDocumentUploadCleanupService>();
                var count = await cleanup.CleanupAsync(DateTime.UtcNow - ClaimLifetime, stoppingToken);
                if (count > 0) logger.LogInformation("Removed {Count} expired Vendor document uploads", count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Vendor document upload cleanup failed");
            }

            try
            {
                await Task.Delay(RunInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }
}