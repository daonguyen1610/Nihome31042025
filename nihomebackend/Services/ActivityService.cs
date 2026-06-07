using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class ActivityService(
    AppDbContext db,
    EntityTranslationService translationSvc,
    HostedImageService hostedImageService)
{
    private ILogger<ActivityService> Logger => db.GetService<ILoggerFactory>().CreateLogger<ActivityService>();

    public async Task<List<ActivityResponse>> GetAllAsync(string lang = "vi")
    {
        var items = await db.Activities.AsNoTracking().OrderBy(a => a.SortOrder).ToListAsync();
        Logger.LogDebug("Fetched {Count} activities (lang={Lang})", items.Count, lang);
        var translations = await translationSvc.GetBatchTranslationsAsync(
            EntityTypes.Activity, items.Select(a => a.Id), lang);

        return items.Select(a =>
        {
            var t = translations.GetValueOrDefault(a.Id, new Dictionary<string, string>());
            return MapToResponse(a, t);
        }).ToList();
    }

    public async Task<ActivityResponse?> GetBySlugAsync(string slug, string lang = "vi")
    {
        var item = await db.Activities.AsNoTracking().FirstOrDefaultAsync(a => a.Slug == slug);
        if (item == null)
        {
            Logger.LogWarning("Activity not found by slug {Slug}", slug);
            return null;
        }

        var t = await translationSvc.GetEntityTranslationsAsync(EntityTypes.Activity, item.Id, lang);
        Logger.LogDebug("Fetched activity {ActivityId} by slug {Slug} (lang={Lang})", item.Id, slug, lang);
        return MapToResponse(item, t);
    }

    public async Task<ActivityResponse> CreateAsync(UpsertActivityRequest req)
    {
        await EnsureCategoryExistsAsync(req.Category);
        var normalizedImageUrl = hostedImageService.NormalizeImageUrl(req.ImageUrl);

        var entity = new Activity
        {
            Slug = req.Slug,
            Date = req.Date,
            ImageUrl = normalizedImageUrl ?? string.Empty,
            GalleryJson = SerializeGallery(req.Gallery),
            Category = req.Category,
            Author = req.Author,
            Title = req.Title,
            Excerpt = req.Excerpt,
            ContentJson = JsonSerializer.Serialize(req.Content),
            SortOrder = req.SortOrder,
        };
        db.Activities.Add(entity);
        await db.SaveChangesAsync();
        Logger.LogInformation("Created activity {ActivityId} (slug={Slug})", entity.Id, entity.Slug);
        return MapToResponse(entity, new Dictionary<string, string>());
    }

    public async Task<ActivityResponse?> UpdateAsync(int id, UpsertActivityRequest req)
    {
        var entity = await db.Activities.FindAsync(id);
        if (entity == null)
        {
            Logger.LogWarning("Cannot update activity. Id {ActivityId} not found", id);
            return null;
        }

        var previousImageUrl = hostedImageService.NormalizeImageUrl(entity.ImageUrl);
        var nextImageUrl = hostedImageService.NormalizeImageUrl(req.ImageUrl);
        var previousGallery = DeserializeGallery(entity.GalleryJson);

        await EnsureCategoryExistsAsync(req.Category);

        entity.Slug = req.Slug;
        entity.Date = req.Date;
        entity.ImageUrl = nextImageUrl ?? string.Empty;
        entity.GalleryJson = SerializeGallery(req.Gallery);
        entity.Category = req.Category;
        entity.Author = req.Author;
        entity.Title = req.Title;
        entity.Excerpt = req.Excerpt;
        entity.ContentJson = JsonSerializer.Serialize(req.Content);
        entity.SortOrder = req.SortOrder;
        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        if (!string.Equals(previousImageUrl, entity.ImageUrl, StringComparison.OrdinalIgnoreCase))
        {
            hostedImageService.DeleteIfManagedUpload(previousImageUrl);
            Logger.LogInformation("Updated activity {ActivityId} image from {OldImageUrl} to {NewImageUrl}", id, previousImageUrl, entity.ImageUrl);
        }
        DeleteRemovedGalleryImages(previousGallery, DeserializeGallery(entity.GalleryJson));
        Logger.LogInformation("Updated activity {ActivityId} (slug={Slug})", id, entity.Slug);
        return MapToResponse(entity, new Dictionary<string, string>());
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await db.Activities.FindAsync(id);
        if (entity == null)
        {
            Logger.LogWarning("Cannot delete activity. Id {ActivityId} not found", id);
            return false;
        }
        var imageUrl = entity.ImageUrl;
        var gallery = DeserializeGallery(entity.GalleryJson);
        db.Activities.Remove(entity);
        await db.SaveChangesAsync();
        hostedImageService.DeleteIfManagedUpload(imageUrl);
        foreach (var url in gallery)
        {
            hostedImageService.DeleteIfManagedUpload(url);
        }
        await translationSvc.DeleteEntityTranslationsAsync(EntityTypes.Activity, id);
        Logger.LogInformation("Deleted activity {ActivityId}", id);
        return true;
    }

    private static ActivityResponse MapToResponse(Activity a, Dictionary<string, string> t) => new()
    {
        Id = a.Id,
        Slug = a.Slug,
        Date = a.Date,
        ImageUrl = a.ImageUrl,
        Gallery = string.IsNullOrEmpty(a.GalleryJson) ? null : JsonSerializer.Deserialize<string[]>(a.GalleryJson),
        Category = a.Category,
        Author = a.Author,
        Title = t.GetValueOrDefault("Title", a.Title),
        Excerpt = t.GetValueOrDefault("Excerpt", a.Excerpt),
        Content = t.TryGetValue("Content", out var c)
            ? JsonSerializer.Deserialize<string[]>(c) ?? []
            : JsonSerializer.Deserialize<string[]>(a.ContentJson) ?? [],
    };

    private string? SerializeGallery(string[]? gallery)
    {
        if (gallery == null || gallery.Length == 0)
        {
            return null;
        }
        var normalized = gallery
            .Select(url => hostedImageService.NormalizeImageUrl(url) ?? string.Empty)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .ToArray();
        return normalized.Length == 0 ? null : JsonSerializer.Serialize(normalized);
    }

    private static string[] DeserializeGallery(string? galleryJson)
    {
        if (string.IsNullOrEmpty(galleryJson))
        {
            return [];
        }
        return JsonSerializer.Deserialize<string[]>(galleryJson) ?? [];
    }

    private void DeleteRemovedGalleryImages(string[] previous, string[] current)
    {
        var kept = new HashSet<string>(current, StringComparer.OrdinalIgnoreCase);
        foreach (var url in previous)
        {
            if (!kept.Contains(url))
            {
                hostedImageService.DeleteIfManagedUpload(url);
            }
        }
    }

    private async Task EnsureCategoryExistsAsync(string categoryName)
    {
        var normalizedName = (categoryName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return;
        }

        var exists = await db.ActivityCategories
            .AsNoTracking()
            .AnyAsync(c => c.Name.ToLower() == normalizedName.ToLower());

        if (exists)
        {
            return;
        }

        var maxSortOrder = await db.ActivityCategories
            .AsNoTracking()
            .Select(c => (int?)c.SortOrder)
            .MaxAsync() ?? 0;

        db.ActivityCategories.Add(new ActivityCategory
        {
            Name = normalizedName,
            IsActive = true,
            SortOrder = maxSortOrder + 1,
        });

        await db.SaveChangesAsync();
        Logger.LogInformation("Auto-created activity category {CategoryName} from activity payload", normalizedName);
    }
}
