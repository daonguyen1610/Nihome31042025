using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class NewsService(
    AppDbContext db,
    EntityTranslationService translationSvc,
    HostedImageService hostedImageService,
    ILogger<NewsService> logger)
{

    public async Task<List<NewsResponse>> GetAllAsync(string lang = "vi")
    {
        var items = await db.NewsArticles.AsNoTracking().OrderBy(a => a.SortOrder).ToListAsync();
        logger.LogDebug("Fetched {Count} news articles (lang={Lang})", items.Count, lang);
        var translations = await translationSvc.GetBatchTranslationsAsync(
            EntityTypes.News, items.Select(a => a.Id), lang);

        return items.Select(a =>
        {
            var t = translations.GetValueOrDefault(a.Id, new Dictionary<string, string>());
            return MapToResponse(a, t);
        }).ToList();
    }

    public async Task<NewsResponse?> GetBySlugAsync(string slug, string lang = "vi")
    {
        var item = await db.NewsArticles.AsNoTracking().FirstOrDefaultAsync(a => a.Slug == slug);
        if (item == null)
        {
            logger.LogWarning("News not found by slug {Slug}", slug);
            return null;
        }

        var t = await translationSvc.GetEntityTranslationsAsync(EntityTypes.News, item.Id, lang);
        logger.LogDebug("Fetched news {NewsId} by slug {Slug} (lang={Lang})", item.Id, slug, lang);
        return MapToResponse(item, t);
    }

    public async Task<NewsResponse> CreateAsync(UpsertNewsRequest req)
    {
        var (categoryId, categoryName) = await ResolveCategoryAsync(req.NewsCategoryId, req.Category);
        var normalizedImageUrl = hostedImageService.NormalizeImageUrl(req.ImageUrl);
        var entity = new NewsArticle
        {
            Slug = req.Slug,
            Date = req.Date,
            ImageUrl = normalizedImageUrl ?? string.Empty,
            GalleryJson = SerializeGallery(req.Gallery),
            Category = categoryName,
            NewsCategoryId = categoryId,
            Title = req.Title,
            Excerpt = req.Excerpt,
            ContentJson = SerializeContent(req.Content),
            SortOrder = req.SortOrder,
        };
        db.NewsArticles.Add(entity);
        await db.SaveChangesAsync();
        logger.LogInformation("Created news article {NewsId} (slug={Slug})", entity.Id, entity.Slug);
        return MapToResponse(entity, new Dictionary<string, string>());
    }

    public async Task<NewsResponse?> UpdateAsync(int id, UpsertNewsRequest req)
    {
        var entity = await db.NewsArticles.FindAsync(id);
        if (entity == null)
        {
            logger.LogWarning("Cannot update news. Id {NewsId} not found", id);
            return null;
        }

        var previousImageUrl = hostedImageService.NormalizeImageUrl(entity.ImageUrl);
        var nextImageUrl = hostedImageService.NormalizeImageUrl(req.ImageUrl);
        var previousGallery = DeserializeGallery(entity.GalleryJson);
        var (categoryId, categoryName) = await ResolveCategoryAsync(req.NewsCategoryId, req.Category);

        entity.Slug = req.Slug;
        entity.Date = req.Date;
        entity.ImageUrl = nextImageUrl ?? string.Empty;
        entity.GalleryJson = SerializeGallery(req.Gallery);
        entity.Category = categoryName;
        entity.NewsCategoryId = categoryId;
        entity.Title = req.Title;
        entity.Excerpt = req.Excerpt;
        entity.ContentJson = SerializeContent(req.Content);
        entity.SortOrder = req.SortOrder;
        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        if (!string.Equals(previousImageUrl, entity.ImageUrl, StringComparison.OrdinalIgnoreCase))
        {
            hostedImageService.DeleteIfManagedUpload(previousImageUrl);
            logger.LogInformation("Updated news {NewsId} image from {OldImageUrl} to {NewImageUrl}", id, previousImageUrl, entity.ImageUrl);
        }
        DeleteRemovedGalleryImages(previousGallery, DeserializeGallery(entity.GalleryJson));
        logger.LogInformation("Updated news article {NewsId} (slug={Slug})", id, entity.Slug);
        return MapToResponse(entity, new Dictionary<string, string>());
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await db.NewsArticles.FindAsync(id);
        if (entity == null)
        {
            logger.LogWarning("Cannot delete news. Id {NewsId} not found", id);
            return false;
        }
        var imageUrl = entity.ImageUrl;
        var gallery = DeserializeGallery(entity.GalleryJson);
        db.NewsArticles.Remove(entity);
        await db.SaveChangesAsync();
        hostedImageService.DeleteIfManagedUpload(imageUrl);
        foreach (var url in gallery)
        {
            hostedImageService.DeleteIfManagedUpload(url);
        }
        await translationSvc.DeleteEntityTranslationsAsync(EntityTypes.News, id);
        logger.LogInformation("Deleted news article {NewsId}", id);
        return true;
    }

    private static NewsResponse MapToResponse(NewsArticle a, Dictionary<string, string> t) => new()
    {
        Id = a.Id,
        Slug = a.Slug,
        Date = a.Date,
        ImageUrl = a.ImageUrl,
        Gallery = string.IsNullOrEmpty(a.GalleryJson) ? null : JsonSerializer.Deserialize<string[]>(a.GalleryJson),
        Category = a.Category,
        NewsCategoryId = a.NewsCategoryId,
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
        {
            return [];
        }

        return JsonSerializer.Deserialize<object[]>(contentJson) ?? [];
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
            var byId = await db.NewsCategories.FindAsync(categoryId.Value);
            if (byId == null)
            {
                throw new InvalidOperationException("Danh mục tin tức không tồn tại.");
            }
            return (byId.Id, byId.Name);
        }

        var normalizedName = (categoryName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return (null, string.Empty);
        }

        var existing = await db.NewsCategories
            .FirstOrDefaultAsync(c => c.Name.ToLower() == normalizedName.ToLower());

        if (existing != null)
        {
            return (existing.Id, existing.Name);
        }

        var maxSortOrder = await db.NewsCategories
            .AsNoTracking()
            .Select(c => (int?)c.SortOrder)
            .MaxAsync() ?? 0;

        var created = new NewsCategory
        {
            Name = normalizedName,
            IsActive = true,
            SortOrder = maxSortOrder + 1,
        };
        db.NewsCategories.Add(created);
        await db.SaveChangesAsync();
        logger.LogInformation("Auto-created news category {CategoryName} from news payload", normalizedName);
        return (created.Id, created.Name);
    }
}
