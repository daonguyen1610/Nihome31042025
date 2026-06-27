using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class ActivityService(
    AppDbContext db,
    EntityTranslationService translationSvc,
    HostedImageService hostedImageService,
    ILogger<ActivityService> logger)
{

    public async Task<List<ActivityResponse>> GetAllAsync(string lang = "vi")
    {
        var items = await db.Activities.AsNoTracking().OrderBy(a => a.SortOrder).ToListAsync();
        logger.LogDebug("Fetched {Count} activities (lang={Lang})", items.Count, lang);
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
            logger.LogWarning("Activity not found by slug {Slug}", slug);
            return null;
        }

        var t = await translationSvc.GetEntityTranslationsAsync(EntityTypes.Activity, item.Id, lang);
        logger.LogDebug("Fetched activity {ActivityId} by slug {Slug} (lang={Lang})", item.Id, slug, lang);
        return MapToResponse(item, t);
    }

    public async Task<ActivityResponse> CreateAsync(UpsertActivityRequest req)
    {
        var (categoryId, categoryName) = await ResolveCategoryAsync(req.CategoryId, req.Category);
        var normalizedImageUrl = hostedImageService.NormalizeImageUrl(req.ImageUrl);

        var entity = new Activity
        {
            Slug = req.Slug,
            Date = req.Date,
            ImageUrl = normalizedImageUrl ?? string.Empty,
            GalleryJson = SerializeGallery(req.Gallery),
            Category = categoryName,
            ActivityCategoryId = categoryId,
            Author = req.Author,
            Title = req.Title,
            Excerpt = req.Excerpt,
            ContentJson = SerializeContent(req.Content),
            SortOrder = req.SortOrder,
        };
        db.Activities.Add(entity);
        await db.SaveChangesAsync();
        logger.LogInformation("Created activity {ActivityId} (slug={Slug})", entity.Id, entity.Slug);
        return MapToResponse(entity, new Dictionary<string, string>());
    }

    public async Task<ActivityResponse?> UpdateAsync(int id, UpsertActivityRequest req)
    {
        var entity = await db.Activities.FindAsync(id);
        if (entity == null)
        {
            logger.LogWarning("Cannot update activity. Id {ActivityId} not found", id);
            return null;
        }

        var previousImageUrl = hostedImageService.NormalizeImageUrl(entity.ImageUrl);
        var nextImageUrl = hostedImageService.NormalizeImageUrl(req.ImageUrl);
        var previousGallery = DeserializeGallery(entity.GalleryJson);

        var (categoryId, categoryName) = await ResolveCategoryAsync(req.CategoryId, req.Category);

        entity.Slug = req.Slug;
        entity.Date = req.Date;
        entity.ImageUrl = nextImageUrl ?? string.Empty;
        entity.GalleryJson = SerializeGallery(req.Gallery);
        entity.Category = categoryName;
        entity.ActivityCategoryId = categoryId;
        entity.Author = req.Author;
        entity.Title = req.Title;
        entity.Excerpt = req.Excerpt;
        entity.ContentJson = SerializeContent(req.Content);
        entity.SortOrder = req.SortOrder;
        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        if (!string.Equals(previousImageUrl, entity.ImageUrl, StringComparison.OrdinalIgnoreCase))
        {
            hostedImageService.DeleteIfManagedUpload(previousImageUrl);
            logger.LogInformation("Updated activity {ActivityId} image from {OldImageUrl} to {NewImageUrl}", id, previousImageUrl, entity.ImageUrl);
        }
        DeleteRemovedGalleryImages(previousGallery, DeserializeGallery(entity.GalleryJson));
        logger.LogInformation("Updated activity {ActivityId} (slug={Slug})", id, entity.Slug);
        return MapToResponse(entity, new Dictionary<string, string>());
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await db.Activities.FindAsync(id);
        if (entity == null)
        {
            logger.LogWarning("Cannot delete activity. Id {ActivityId} not found", id);
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
        logger.LogInformation("Deleted activity {ActivityId}", id);
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
        CategoryId = a.ActivityCategoryId,
        Author = a.Author,
        Title = t.GetValueOrDefault("Title", a.Title),
        Excerpt = t.GetValueOrDefault("Excerpt", a.Excerpt),
        Content = t.TryGetValue("Content", out var c)
            ? DeserializeContent(c)
            : DeserializeContent(a.ContentJson),
    };

    private static string SerializeContent(object[] content) => JsonSerializer.Serialize(content ?? []);

    private static object[] DeserializeContent(string? contentJson)
    {
        if (string.IsNullOrWhiteSpace(contentJson))
            return [];

        var result = JsonSerializer.Deserialize<object[]>(contentJson) ?? [];

        // Auto-repair: previous admin UI bug stored the whole ContentItem JSON array
        // as a single escaped string element — e.g. ["[{\"type\":\"image\",...},\"text\"]"].
        // Unwrap and re-deserialize so images and text render correctly.
        if (result.Length == 1
            && result[0] is JsonElement el
            && el.ValueKind == JsonValueKind.String)
        {
            var inner = el.GetString() ?? "";
            if (inner.TrimStart().StartsWith('['))
            {
                try
                {
                    var repaired = JsonSerializer.Deserialize<object[]>(inner);
                    if (repaired is { Length: > 0 }) return repaired;
                }
                catch { /* not valid JSON — keep original */ }
            }
        }

        return result;
    }

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

    private async Task<(int? Id, string Name)> ResolveCategoryAsync(int? categoryId, string? categoryName)
    {
        if (categoryId.HasValue)
        {
            var byId = await db.ActivityCategories.FindAsync(categoryId.Value);
            if (byId == null)
            {
                throw new InvalidOperationException("Danh mục bài đăng không tồn tại.");
            }
            return (byId.Id, byId.Name);
        }

        var normalizedName = (categoryName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return (null, string.Empty);
        }

        var existing = await db.ActivityCategories
            .FirstOrDefaultAsync(c => c.Name.ToLower() == normalizedName.ToLower());

        if (existing != null)
        {
            return (existing.Id, existing.Name);
        }

        var maxSortOrder = await db.ActivityCategories
            .AsNoTracking()
            .Select(c => (int?)c.SortOrder)
            .MaxAsync() ?? 0;

        var created = new ActivityCategory
        {
            Name = normalizedName,
            IsActive = true,
            SortOrder = maxSortOrder + 1,
        };
        db.ActivityCategories.Add(created);
        await db.SaveChangesAsync();
        logger.LogInformation("Auto-created activity category {CategoryName} from activity payload", normalizedName);
        return (created.Id, created.Name);
    }
}
