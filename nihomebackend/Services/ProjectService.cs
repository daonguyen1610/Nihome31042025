using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class ProjectService(AppDbContext db)
{
    public async Task<List<ProjectResponse>> GetAllAsync()
    {
        var items = await db.Projects.AsNoTracking().OrderBy(p => p.SortOrder).ToListAsync();
        return items.Select(MapToResponse).ToList();
    }

    public async Task<ProjectResponse?> GetBySlugAsync(string slug)
    {
        var item = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Slug == slug);
        return item == null ? null : MapToResponse(item);
    }

    public async Task<ProjectResponse> CreateAsync(UpsertProjectRequest req)
    {
        var entity = new Project
        {
            Slug = req.Slug,
            ImageUrl = req.ImageUrl,
            GalleryJson = req.Gallery != null ? JsonSerializer.Serialize(req.Gallery) : null,
            Name = req.Name,
            Client = req.Client,
            Location = req.Location,
            Scale = req.Scale,
            Scope = req.Scope,
            Status = req.Status,
            Year = req.Year,
            Category = req.Category,
            Description = req.Description,
            ChallengesJson = req.Challenges != null ? JsonSerializer.Serialize(req.Challenges) : null,
            SolutionsJson = req.Solutions != null ? JsonSerializer.Serialize(req.Solutions) : null,
            HighlightsJson = req.Highlights.HasValue ? req.Highlights.Value.GetRawText() : null,
            SortOrder = req.SortOrder,
        };
        db.Projects.Add(entity);
        await db.SaveChangesAsync();
        return MapToResponse(entity);
    }

    public async Task<ProjectResponse?> UpdateAsync(int id, UpsertProjectRequest req)
    {
        var entity = await db.Projects.FindAsync(id);
        if (entity == null) return null;

        entity.Slug = req.Slug;
        entity.ImageUrl = req.ImageUrl;
        entity.GalleryJson = req.Gallery != null ? JsonSerializer.Serialize(req.Gallery) : null;
        entity.Name = req.Name;
        entity.Client = req.Client;
        entity.Location = req.Location;
        entity.Scale = req.Scale;
        entity.Scope = req.Scope;
        entity.Status = req.Status;
        entity.Year = req.Year;
        entity.Category = req.Category;
        entity.Description = req.Description;
        entity.ChallengesJson = req.Challenges != null ? JsonSerializer.Serialize(req.Challenges) : null;
        entity.SolutionsJson = req.Solutions != null ? JsonSerializer.Serialize(req.Solutions) : null;
        entity.HighlightsJson = req.Highlights.HasValue ? req.Highlights.Value.GetRawText() : null;
        entity.SortOrder = req.SortOrder;
        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return MapToResponse(entity);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await db.Projects.FindAsync(id);
        if (entity == null) return false;
        db.Projects.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }

    private static ProjectResponse MapToResponse(Project p) => new()
    {
        Id = p.Id,
        Slug = p.Slug,
        ImageUrl = p.ImageUrl,
        Gallery = string.IsNullOrEmpty(p.GalleryJson) ? null : JsonSerializer.Deserialize<string[]>(p.GalleryJson),
        Name = p.Name,
        Client = p.Client,
        Location = p.Location,
        Scale = p.Scale,
        Scope = p.Scope,
        Status = p.Status,
        Year = p.Year,
        Category = p.Category,
        Description = p.Description,
        Challenges = string.IsNullOrEmpty(p.ChallengesJson) ? null : JsonSerializer.Deserialize<string[]>(p.ChallengesJson),
        Solutions = string.IsNullOrEmpty(p.SolutionsJson) ? null : JsonSerializer.Deserialize<string[]>(p.SolutionsJson),
        Highlights = string.IsNullOrEmpty(p.HighlightsJson) ? null : JsonSerializer.Deserialize<JsonElement>(p.HighlightsJson),
    };
}
