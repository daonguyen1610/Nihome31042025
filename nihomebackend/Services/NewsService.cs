using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class NewsService(
    AppDbContext db,
    EntityTranslationService translationSvc,
    HostedImageService hostedImageService)
{
    private ILogger<NewsService> Logger => db.GetService<ILoggerFactory>().CreateLogger<NewsService>();

    public async Task<List<NewsResponse>> GetAllAsync(string lang = "vi")
    {
        var items = await db.NewsArticles.AsNoTracking().OrderBy(a => a.SortOrder).ToListAsync();
        Logger.LogDebug("Fetched {Count} news articles (lang={Lang})", items.Count, lang);
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
            Logger.LogWarning("News not found by slug {Slug}", slug);
            return null;
        }

        var t = await translationSvc.GetEntityTranslationsAsync(EntityTypes.News, item.Id, lang);
        Logger.LogDebug("Fetched news {NewsId} by slug {Slug} (lang={Lang})", item.Id, slug, lang);
        return MapToResponse(item, t);
    }

    public async Task<NewsResponse> CreateAsync(UpsertNewsRequest req)
    {
        var normalizedImageUrl = hostedImageService.NormalizeImageUrl(req.ImageUrl);
        var entity = new NewsArticle
        {
            Slug = req.Slug,
            Date = req.Date,
            ImageUrl = normalizedImageUrl ?? string.Empty,
            GalleryJson = SerializeGallery(req.Gallery),
            Category = req.Category,
            Title = req.Title,
            Excerpt = req.Excerpt,
            ContentJson = JsonSerializer.Serialize(req.Content),
            SortOrder = req.SortOrder,
        };
        db.NewsArticles.Add(entity);
        await db.SaveChangesAsync();
        Logger.LogInformation("Created news article {NewsId} (slug={Slug})", entity.Id, entity.Slug);
        return MapToResponse(entity, new Dictionary<string, string>());
    }

    public async Task<NewsResponse?> UpdateAsync(int id, UpsertNewsRequest req)
    {
        var entity = await db.NewsArticles.FindAsync(id);
        if (entity == null)
        {
            Logger.LogWarning("Cannot update news. Id {NewsId} not found", id);
            return null;
        }

        var previousImageUrl = hostedImageService.NormalizeImageUrl(entity.ImageUrl);
        var nextImageUrl = hostedImageService.NormalizeImageUrl(req.ImageUrl);
        var previousGallery = DeserializeGallery(entity.GalleryJson);

        entity.Slug = req.Slug;
        entity.Date = req.Date;
        entity.ImageUrl = nextImageUrl ?? string.Empty;
        entity.GalleryJson = SerializeGallery(req.Gallery);
        entity.Category = req.Category;
        entity.Title = req.Title;
        entity.Excerpt = req.Excerpt;
        entity.ContentJson = JsonSerializer.Serialize(req.Content);
        entity.SortOrder = req.SortOrder;
        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        if (!string.Equals(previousImageUrl, entity.ImageUrl, StringComparison.OrdinalIgnoreCase))
        {
            hostedImageService.DeleteIfManagedUpload(previousImageUrl);
            Logger.LogInformation("Updated news {NewsId} image from {OldImageUrl} to {NewImageUrl}", id, previousImageUrl, entity.ImageUrl);
        }
        DeleteRemovedGalleryImages(previousGallery, DeserializeGallery(entity.GalleryJson));
        Logger.LogInformation("Updated news article {NewsId} (slug={Slug})", id, entity.Slug);
        return MapToResponse(entity, new Dictionary<string, string>());
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await db.NewsArticles.FindAsync(id);
        if (entity == null)
        {
            Logger.LogWarning("Cannot delete news. Id {NewsId} not found", id);
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
        Logger.LogInformation("Deleted news article {NewsId}", id);
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
}
