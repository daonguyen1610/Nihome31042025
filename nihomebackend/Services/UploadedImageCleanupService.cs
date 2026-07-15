using NihomeBackend.Data;

namespace NihomeBackend.Services;

public class UploadedImageCleanupService(
    IServiceScopeFactory scopeFactory,
    IWebHostEnvironment env,
    ILogger<UploadedImageCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(6);
    private static readonly TimeSpan MinFileAgeToDelete = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Uploaded image cleanup service started (interval: {IntervalHours}h, minAge: {MinAgeHours}h)",
            RunInterval.TotalHours,
            MinFileAgeToDelete.TotalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOrphanImagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Uploaded image cleanup job failed");
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

        logger.LogInformation("Uploaded image cleanup service stopped");
    }

    private async Task CleanupOrphanImagesAsync(CancellationToken cancellationToken)
    {
        var uploadDir = Path.Combine(env.ContentRootPath, "wwwroot", "images", "upload");
        Directory.CreateDirectory(uploadDir);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var referencedPaths = await ReferencedUploadedImages.GetAsync(db, cancellationToken);
        var now = DateTime.UtcNow;
        var totalFiles = 0;
        var deletedCount = 0;
        var skippedReferencedCount = 0;
        var skippedRecentCount = 0;

        // Scan recursively to handle organised subfolders (e.g. upload/projects/slug/uuid.jpg)
        foreach (var filePath in Directory.EnumerateFiles(uploadDir, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            totalFiles++;

            // Use path relative to uploadDir so it matches what the DB stores
            // e.g. "projects/nha-may-bma/uuid.jpg" or just "uuid.jpg"
            var relPath = Path.GetRelativePath(uploadDir, filePath).Replace('\\', '/');
            if (referencedPaths.Contains(relPath))
            {
                skippedReferencedCount++;
                continue;
            }

            var lastWriteUtc = File.GetLastWriteTimeUtc(filePath);
            if (now - lastWriteUtc < MinFileAgeToDelete)
            {
                skippedRecentCount++;
                continue;
            }

            try
            {
                File.Delete(filePath);
                deletedCount++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Unable to delete orphan uploaded image {FilePath}", filePath);
            }
        }

        logger.LogInformation(
            "Uploaded image cleanup completed. totalFiles={TotalFiles}, referenced={ReferencedCount}, recent={RecentCount}, deleted={DeletedCount}",
            totalFiles,
            skippedReferencedCount,
            skippedRecentCount,
            deletedCount);
    }
}