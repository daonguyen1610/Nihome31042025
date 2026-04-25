using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class ServiceItemService(AppDbContext db)
{
    public async Task<List<ServiceResponse>> GetAllAsync()
    {
        var items = await db.ServiceItems.AsNoTracking().OrderBy(s => s.SortOrder).ToListAsync();
        return items.Select(MapToResponse).ToList();
    }

    public async Task<ServiceResponse?> GetBySlugAsync(string slug)
    {
        var item = await db.ServiceItems.AsNoTracking().FirstOrDefaultAsync(s => s.Slug == slug);
        return item == null ? null : MapToResponse(item);
    }

    public async Task<ServiceResponse> CreateAsync(UpsertServiceRequest req)
    {
        var entity = new ServiceItem
        {
            Slug = req.Slug,
            Title = req.Title,
            ShortTitle = req.ShortTitle,
            Tagline = req.Tagline,
            Intro = req.Intro,
            SectionsJson = req.Sections.GetRawText(),
            HighlightsJson = JsonSerializer.Serialize(req.Highlights),
            SortOrder = req.SortOrder,
        };
        db.ServiceItems.Add(entity);
        await db.SaveChangesAsync();
        return MapToResponse(entity);
    }

    public async Task<ServiceResponse?> UpdateAsync(int id, UpsertServiceRequest req)
    {
        var entity = await db.ServiceItems.FindAsync(id);
        if (entity == null) return null;

        entity.Slug = req.Slug;
        entity.Title = req.Title;
        entity.ShortTitle = req.ShortTitle;
        entity.Tagline = req.Tagline;
        entity.Intro = req.Intro;
        entity.SectionsJson = req.Sections.GetRawText();
        entity.HighlightsJson = JsonSerializer.Serialize(req.Highlights);
        entity.SortOrder = req.SortOrder;
        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return MapToResponse(entity);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await db.ServiceItems.FindAsync(id);
        if (entity == null) return false;
        db.ServiceItems.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }

    private static ServiceResponse MapToResponse(ServiceItem s) => new()
    {
        Id = s.Id,
        Slug = s.Slug,
        Title = s.Title,
        ShortTitle = s.ShortTitle,
        Tagline = s.Tagline,
        Intro = s.Intro,
        Sections = JsonSerializer.Deserialize<JsonElement>(s.SectionsJson),
        Highlights = JsonSerializer.Deserialize<string[]>(s.HighlightsJson) ?? [],
    };
}
