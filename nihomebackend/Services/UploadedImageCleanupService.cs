using System.Text.Json;
using Microsoft.EntityFrameworkCore;
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

        var referencedFiles = await GetReferencedFileNamesAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var totalFiles = 0;
        var deletedCount = 0;
        var skippedReferencedCount = 0;
        var skippedRecentCount = 0;

        foreach (var filePath in Directory.EnumerateFiles(uploadDir))
        {
            cancellationToken.ThrowIfCancellationRequested();
            totalFiles++;

            var fileName = Path.GetFileName(filePath);
            if (referencedFiles.Contains(fileName))
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

    private async Task<HashSet<string>> GetReferencedFileNamesAsync(CancellationToken cancellationToken)
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

            var fileName = Path.GetFileName(imageUrl);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                referenced.Add(fileName);
            }
        }

        var activityImageUrls = await db.Activities
            .AsNoTracking()
            .Select(x => x.ImageUrl)
            .ToListAsync(cancellationToken);
        foreach (var imageUrl in activityImageUrls)
        {
            AddIfManaged(imageUrl);
        }

        var newsImageUrls = await db.NewsArticles
            .AsNoTracking()
            .Select(x => x.ImageUrl)
            .ToListAsync(cancellationToken);
        foreach (var imageUrl in newsImageUrls)
        {
            AddIfManaged(imageUrl);
        }

        var projectImages = await db.Projects
            .AsNoTracking()
            .Select(x => new { x.ImageUrl, x.GalleryJson })
            .ToListAsync(cancellationToken);
        foreach (var project in projectImages)
        {
            AddIfManaged(project.ImageUrl);

            if (string.IsNullOrWhiteSpace(project.GalleryJson))
            {
                continue;
            }

            try
            {
                var gallery = JsonSerializer.Deserialize<string[]>(project.GalleryJson) ?? [];
                foreach (var galleryImageUrl in gallery)
                {
                    AddIfManaged(galleryImageUrl);
                }
            }
            catch (JsonException)
            {
                // Ignore malformed legacy data to keep cleanup resilient.
            }
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