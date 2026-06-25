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

        var referencedPaths = await GetReferencedRelativePathsAsync(cancellationToken);
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

    private async Task<HashSet<string>> GetReferencedRelativePathsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Store paths relative to the upload directory so they can be compared
        // with Path.GetRelativePath() results from the filesystem scan.
        // Flat files:     "uuid.jpg"
        // Organised:      "projects/nha-may-bma/uuid.jpg"
        var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddIfManaged(string? imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl) ||
                !imageUrl.StartsWith(ManagedImagePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Strip the /images/upload/ prefix to get the relative path
            var relPath = imageUrl[ManagedImagePrefix.Length..].TrimStart('/');
            if (!string.IsNullOrWhiteSpace(relPath))
            {
                referenced.Add(relPath);
            }
        }

        var activityImages = await db.Activities
            .AsNoTracking()
            .Select(x => new { x.ImageUrl, x.GalleryJson })
            .ToListAsync(cancellationToken);
        foreach (var activity in activityImages)
        {
            AddIfManaged(activity.ImageUrl);
            AddGalleryUrls(activity.GalleryJson, AddIfManaged);
        }

        var newsImages = await db.NewsArticles
            .AsNoTracking()
            .Select(x => new { x.ImageUrl, x.GalleryJson })
            .ToListAsync(cancellationToken);
        foreach (var article in newsImages)
        {
            AddIfManaged(article.ImageUrl);
            AddGalleryUrls(article.GalleryJson, AddIfManaged);
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

        // About sections: main ImageUrl + nested imageUrl values inside ItemsJson
        // (cert images, org-chart images stored as { imageUrl: "..." } in JSON)
        var aboutSections = await db.AboutSectionContents
            .AsNoTracking()
            .Select(x => new { x.ImageUrl, x.ItemsJson })
            .ToListAsync(cancellationToken);
        foreach (var section in aboutSections)
        {
            AddIfManaged(section.ImageUrl);
            AddJsonImageUrls(section.ItemsJson, AddIfManaged);
        }

        return referenced;
    }

    // Deserialise a JSON string[] gallery and protect each URL.
    private static void AddGalleryUrls(string? galleryJson, Action<string?> addIfManaged)
    {
        if (string.IsNullOrWhiteSpace(galleryJson)) return;
        try
        {
            var urls = JsonSerializer.Deserialize<string[]>(galleryJson) ?? [];
            foreach (var url in urls)
                addIfManaged(url);
        }
        catch (JsonException) { }
    }

    // Walk arbitrary JSON and protect every property named "imageUrl".
    private static void AddJsonImageUrls(string? json, Action<string?> addIfManaged)
    {
        if (string.IsNullOrWhiteSpace(json)) return;
        try
        {
            using var doc = JsonDocument.Parse(json);
            WalkElement(doc.RootElement, addIfManaged);
        }
        catch (JsonException) { }
    }

    private static void WalkElement(JsonElement element, Action<string?> addIfManaged)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    if (prop.Name == "imageUrl" && prop.Value.ValueKind == JsonValueKind.String)
                        addIfManaged(prop.Value.GetString());
                    else
                        WalkElement(prop.Value, addIfManaged);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    WalkElement(item, addIfManaged);
                break;
        }
    }
}