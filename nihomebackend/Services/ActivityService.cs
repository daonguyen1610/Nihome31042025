using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class ActivityService(AppDbContext db, EntityTranslationService translationSvc)
{
    public async Task<List<ActivityResponse>> GetAllAsync(string lang = "vi")
    {
        var items = await db.Activities.AsNoTracking().OrderBy(a => a.SortOrder).ToListAsync();
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
        if (item == null) return null;

        var t = await translationSvc.GetEntityTranslationsAsync(EntityTypes.Activity, item.Id, lang);
        return MapToResponse(item, t);
    }

    public async Task<ActivityResponse> CreateAsync(UpsertActivityRequest req)
    {
        var entity = new Activity
        {
            Slug = req.Slug,
            Date = req.Date,
            ImageUrl = req.ImageUrl,
            Category = req.Category,
            Author = req.Author,
            Title = req.Title,
            Excerpt = req.Excerpt,
            ContentJson = JsonSerializer.Serialize(req.Content),
            SortOrder = req.SortOrder,
        };
        db.Activities.Add(entity);
        await db.SaveChangesAsync();
        return MapToResponse(entity, new Dictionary<string, string>());
    }

    public async Task<ActivityResponse?> UpdateAsync(int id, UpsertActivityRequest req)
    {
        var entity = await db.Activities.FindAsync(id);
        if (entity == null) return null;

        entity.Slug = req.Slug;
        entity.Date = req.Date;
        entity.ImageUrl = req.ImageUrl;
        entity.Category = req.Category;
        entity.Author = req.Author;
        entity.Title = req.Title;
        entity.Excerpt = req.Excerpt;
        entity.ContentJson = JsonSerializer.Serialize(req.Content);
        entity.SortOrder = req.SortOrder;
        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return MapToResponse(entity, new Dictionary<string, string>());
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await db.Activities.FindAsync(id);
        if (entity == null) return false;
        db.Activities.Remove(entity);
        await db.SaveChangesAsync();
        await translationSvc.DeleteEntityTranslationsAsync(EntityTypes.Activity, id);
        return true;
    }

    private static ActivityResponse MapToResponse(Activity a, Dictionary<string, string> t) => new()
    {
        Id = a.Id,
        Slug = a.Slug,
        Date = a.Date,
        ImageUrl = a.ImageUrl,
        Category = a.Category,
        Author = a.Author,
        Title = t.GetValueOrDefault("Title", a.Title),
        Excerpt = t.GetValueOrDefault("Excerpt", a.Excerpt),
        Content = t.TryGetValue("Content", out var c)
            ? JsonSerializer.Deserialize<string[]>(c) ?? []
            : JsonSerializer.Deserialize<string[]>(a.ContentJson) ?? [],
    };
}
