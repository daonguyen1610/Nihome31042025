using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class SlideshowService(AppDbContext db, EntityTranslationService translationSvc)
{
    public async Task<List<SlideshowResponse>> GetAllAsync(string lang = "vi", bool activeOnly = true)
    {
        var query = db.SlideshowItems.AsNoTracking().OrderBy(s => s.SortOrder).AsQueryable();
        if (activeOnly) query = query.Where(s => s.IsActive);

        var items = await query.ToListAsync();
        var translations = await translationSvc.GetBatchTranslationsAsync(
            EntityTypes.Slideshow, items.Select(s => s.Id), lang);

        return items.Select(s =>
        {
            var t = translations.GetValueOrDefault(s.Id, new Dictionary<string, string>());
            return MapToResponse(s, t);
        }).ToList();
    }

    public async Task<SlideshowResponse?> GetBySlugAsync(string slug, string lang = "vi")
    {
        var item = await db.SlideshowItems.AsNoTracking().FirstOrDefaultAsync(s => s.Slug == slug);
        if (item == null) return null;

        var t = await translationSvc.GetEntityTranslationsAsync(EntityTypes.Slideshow, item.Id, lang);
        return MapToResponse(item, t);
    }

    public async Task<SlideshowResponse> CreateAsync(UpsertSlideshowRequest req)
    {
        var entity = new SlideshowItem
        {
            Slug = req.Slug,
            ImageUrl = req.ImageUrl,
            Title = req.Title,
            Subtitle = req.Subtitle,
            LinkUrl = req.LinkUrl,
            LinkText = req.LinkText,
            IsActive = req.IsActive,
            SortOrder = req.SortOrder,
        };
        db.SlideshowItems.Add(entity);
        await db.SaveChangesAsync();
        return MapToResponse(entity, new Dictionary<string, string>());
    }

    public async Task<SlideshowResponse?> UpdateAsync(int id, UpsertSlideshowRequest req)
    {
        var entity = await db.SlideshowItems.FindAsync(id);
        if (entity == null) return null;

        entity.Slug = req.Slug;
        entity.ImageUrl = req.ImageUrl;
        entity.Title = req.Title;
        entity.Subtitle = req.Subtitle;
        entity.LinkUrl = req.LinkUrl;
        entity.LinkText = req.LinkText;
        entity.IsActive = req.IsActive;
        entity.SortOrder = req.SortOrder;
        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return MapToResponse(entity, new Dictionary<string, string>());
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await db.SlideshowItems.FindAsync(id);
        if (entity == null) return false;
        db.SlideshowItems.Remove(entity);
        await db.SaveChangesAsync();
        await translationSvc.DeleteEntityTranslationsAsync(EntityTypes.Slideshow, id);
        return true;
    }

    private static SlideshowResponse MapToResponse(SlideshowItem s, Dictionary<string, string> t) => new()
    {
        Id = s.Id,
        Slug = s.Slug,
        ImageUrl = s.ImageUrl,
        Title = t.GetValueOrDefault("Title", s.Title),
        Subtitle = t.TryGetValue("Subtitle", out var sub) ? sub : s.Subtitle,
        LinkUrl = s.LinkUrl,
        LinkText = t.TryGetValue("LinkText", out var lt) ? lt : s.LinkText,
        IsActive = s.IsActive,
        SortOrder = s.SortOrder,
    };
}
