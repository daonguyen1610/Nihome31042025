using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Controllers;
using NihomeBackend.Data;

namespace NihomeBackend.Services;

public class UploadedImageCleanupService(
    IServiceScopeFactory scopeFactory,
    IWebHostEnvironment env,
    ILogger<UploadedImageCleanupService> logger) : BackgroundService
{
    private const string ManagedImagePrefix = "/images/upload/";
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

        var referencedUrls = await GetReferencedUrlsAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var totalFiles = 0;
        var deletedCount = 0;
        var skippedReferencedCount = 0;
        var skippedRecentCount = 0;
        var skippedUnscannedCount = 0;

        // Top-level files (legacy uploads, pre-bucket scheme).
        foreach (var filePath in Directory.EnumerateFiles(uploadDir))
        {
            cancellationToken.ThrowIfCancellationRequested();
            totalFiles++;
            var managedUrl = $"{ManagedImagePrefix}{Path.GetFileName(filePath)}";
            EvaluateFile(filePath, managedUrl, referencedUrls, now,
                ref deletedCount, ref skippedReferencedCount, ref skippedRecentCount);
        }

        // Files inside owned buckets.
        foreach (var bucket in SystemController.AllowedUploadBuckets)
        {
            var bucketDir = Path.Combine(uploadDir, bucket);
            if (!Directory.Exists(bucketDir)) continue;

            foreach (var filePath in Directory.EnumerateFiles(bucketDir))
            {
                cancellationToken.ThrowIfCancellationRequested();
                totalFiles++;
                var managedUrl = $"{ManagedImagePrefix}{bucket}/{Path.GetFileName(filePath)}";
                EvaluateFile(filePath, managedUrl, referencedUrls, now,
                    ref deletedCount, ref skippedReferencedCount, ref skippedRecentCount);
            }
        }

        // Count unowned sub-folders (e.g. "documents/" managed by MeController) so
        // the log is honest but we don't touch them.
        foreach (var subDir in Directory.EnumerateDirectories(uploadDir))
        {
            var bucketName = Path.GetFileName(subDir);
            if (SystemController.AllowedUploadBuckets.Contains(bucketName)) continue;
            skippedUnscannedCount += Directory.EnumerateFiles(subDir).Count();
        }

        logger.LogInformation(
            "Uploaded image cleanup completed. totalFiles={TotalFiles}, referenced={ReferencedCount}, recent={RecentCount}, unscanned={UnscannedCount}, deleted={DeletedCount}",
            totalFiles,
            skippedReferencedCount,
            skippedRecentCount,
            skippedUnscannedCount,
            deletedCount);
    }

    private void EvaluateFile(
        string filePath,
        string managedUrl,
        HashSet<string> referencedUrls,
        DateTime now,
        ref int deletedCount,
        ref int skippedReferencedCount,
        ref int skippedRecentCount)
    {
        // Skip dotfiles (e.g. .gitkeep) so the committed bucket structure is preserved.
        if (Path.GetFileName(filePath).StartsWith('.'))
        {
            skippedReferencedCount++;
            return;
        }

        if (referencedUrls.Contains(managedUrl))
        {
            skippedReferencedCount++;
            return;
        }

        var lastWriteUtc = File.GetLastWriteTimeUtc(filePath);
        if (now - lastWriteUtc < MinFileAgeToDelete)
        {
            skippedRecentCount++;
            return;
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

    private async Task<HashSet<string>> GetReferencedUrlsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddIfManaged(string? imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl) ||
                !imageUrl.StartsWith(ManagedImagePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            referenced.Add(imageUrl);
        }

        void AddGalleryJson(string? galleryJson)
        {
            if (string.IsNullOrWhiteSpace(galleryJson)) return;
            try
            {
                var gallery = JsonSerializer.Deserialize<string[]>(galleryJson) ?? [];
                foreach (var url in gallery) AddIfManaged(url);
            }
            catch (JsonException)
            {
                // Ignore malformed legacy data to keep cleanup resilient.
            }
        }

        var activityImages = await db.Activities
            .AsNoTracking()
            .Select(x => new { x.ImageUrl, x.GalleryJson })
            .ToListAsync(cancellationToken);
        foreach (var a in activityImages)
        {
            AddIfManaged(a.ImageUrl);
            AddGalleryJson(a.GalleryJson);
        }

        var newsImages = await db.NewsArticles
            .AsNoTracking()
            .Select(x => new { x.ImageUrl, x.GalleryJson })
            .ToListAsync(cancellationToken);
        foreach (var n in newsImages)
        {
            AddIfManaged(n.ImageUrl);
            AddGalleryJson(n.GalleryJson);
        }

        var projectImages = await db.Projects
            .AsNoTracking()
            .Select(x => new { x.ImageUrl, x.GalleryJson })
            .ToListAsync(cancellationToken);
        foreach (var p in projectImages)
        {
            AddIfManaged(p.ImageUrl);
            AddGalleryJson(p.GalleryJson);
        }

        var logoImageUrls = await db.ClientLogos
            .AsNoTracking()
            .Select(x => x.ImageUrl)
            .ToListAsync(cancellationToken);
        foreach (var imageUrl in logoImageUrls)
        {
            AddIfManaged(imageUrl);
        }

        var slideshowImageUrls = await db.SlideshowItems
            .AsNoTracking()
            .Select(x => x.ImageUrl)
            .ToListAsync(cancellationToken);
        foreach (var imageUrl in slideshowImageUrls)
        {
            AddIfManaged(imageUrl);
        }

        return referenced;
    }
}