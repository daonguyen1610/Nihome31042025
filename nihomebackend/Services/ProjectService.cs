using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class ProjectService(
    AppDbContext db,
    EntityTranslationService translationSvc,
    HostedImageService hostedImageService,
    ProjectCategoryService categorySvc,
    ILogger<ProjectService> logger)
{

    public async Task<List<ProjectResponse>> GetAllAsync(string lang = "vi")
    {
        var items = await db.Projects.AsNoTracking().OrderBy(p => p.SortOrder).ToListAsync();
        logger.LogDebug("Fetched {Count} projects (lang={Lang})", items.Count, lang);
        var translations = await translationSvc.GetBatchTranslationsAsync(
            EntityTypes.Project, items.Select(p => p.Id), lang);

        return items.Select(p =>
        {
            var t = translations.GetValueOrDefault(p.Id, new Dictionary<string, string>());
            return MapToResponse(p, t);
        }).ToList();
    }

    public async Task<ProjectResponse?> GetBySlugAsync(string slug, string lang = "vi")
    {
        var item = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Slug == slug);
        if (item == null)
        {
            logger.LogWarning("Project not found by slug {Slug}", slug);
            return null;
        }

        var t = await translationSvc.GetEntityTranslationsAsync(EntityTypes.Project, item.Id, lang);
        logger.LogDebug("Fetched project {ProjectId} by slug {Slug} (lang={Lang})", item.Id, slug, lang);
        return MapToResponse(item, t);
    }

    public async Task<ProjectResponse> CreateAsync(UpsertProjectRequest req)
    {
        var (categoryId, categoryName) = await categorySvc.ResolveAsync(req.CategoryId, req.Category);
        var normalizedImageUrl = hostedImageService.NormalizeImageUrl(req.ImageUrl);
        var entity = new Project
        {
            Slug = req.Slug,
            ImageUrl = normalizedImageUrl ?? string.Empty,
            GalleryJson = SerializeGallery(req.Gallery),
            Name = req.Name,
            Client = req.Client,
            Location = req.Location,
            Scale = req.Scale,
            Scope = req.Scope,
            Status = req.Status,
            Year = req.Year,
            Category = string.IsNullOrWhiteSpace(categoryName) ? null : categoryName,
            ProjectCategoryId = categoryId,
            Description = req.Description,
            ChallengesJson = req.Challenges != null ? JsonSerializer.Serialize(req.Challenges) : null,
            SolutionsJson = req.Solutions != null ? JsonSerializer.Serialize(req.Solutions) : null,
            HighlightsJson = req.Highlights.HasValue ? req.Highlights.Value.GetRawText() : null,
            SortOrder = req.SortOrder,
        };
        db.Projects.Add(entity);
        await db.SaveChangesAsync();
        logger.LogInformation("Created project {ProjectId} (slug={Slug})", entity.Id, entity.Slug);
        return MapToResponse(entity, new Dictionary<string, string>());
    }

    public async Task<ProjectResponse?> UpdateAsync(int id, UpsertProjectRequest req)
    {
        var entity = await db.Projects.FindAsync(id);
        if (entity == null)
        {
            logger.LogWarning("Cannot update project. Id {ProjectId} not found", id);
            return null;
        }

        var previousImageUrl = hostedImageService.NormalizeImageUrl(entity.ImageUrl);
        var nextImageUrl = hostedImageService.NormalizeImageUrl(req.ImageUrl);
        var previousGallery = DeserializeGallery(entity.GalleryJson);

        var (categoryId, categoryName) = await categorySvc.ResolveAsync(req.CategoryId, req.Category);

        entity.Slug = req.Slug;
        entity.ImageUrl = nextImageUrl ?? string.Empty;
        entity.GalleryJson = SerializeGallery(req.Gallery);
        entity.Name = req.Name;
        entity.Client = req.Client;
        entity.Location = req.Location;
        entity.Scale = req.Scale;
        entity.Scope = req.Scope;
        entity.Status = req.Status;
        entity.Year = req.Year;
        entity.Category = string.IsNullOrWhiteSpace(categoryName) ? null : categoryName;
        entity.ProjectCategoryId = categoryId;
        entity.Description = req.Description;
        entity.ChallengesJson = req.Challenges != null ? JsonSerializer.Serialize(req.Challenges) : null;
        entity.SolutionsJson = req.Solutions != null ? JsonSerializer.Serialize(req.Solutions) : null;
        entity.HighlightsJson = req.Highlights.HasValue ? req.Highlights.Value.GetRawText() : null;
        entity.SortOrder = req.SortOrder;
        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        if (!string.Equals(previousImageUrl, entity.ImageUrl, StringComparison.OrdinalIgnoreCase))
        {
            hostedImageService.DeleteIfManagedUpload(previousImageUrl);
            logger.LogInformation("Updated project {ProjectId} image from {OldImageUrl} to {NewImageUrl}", id, previousImageUrl, entity.ImageUrl);
        }
        DeleteRemovedGalleryImages(previousGallery, DeserializeGallery(entity.GalleryJson));
        logger.LogInformation("Updated project {ProjectId} (slug={Slug})", id, entity.Slug);
        return MapToResponse(entity, new Dictionary<string, string>());
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await db.Projects.FindAsync(id);
        if (entity == null)
        {
            logger.LogWarning("Cannot delete project. Id {ProjectId} not found", id);
            return false;
        }
        var imageUrl = entity.ImageUrl;
        var gallery = DeserializeGallery(entity.GalleryJson);
        db.Projects.Remove(entity);
        await db.SaveChangesAsync();
        await translationSvc.DeleteEntityTranslationsAsync(EntityTypes.Project, id);
        hostedImageService.DeleteIfManagedUpload(imageUrl);
        foreach (var url in gallery)
        {
            hostedImageService.DeleteIfManagedUpload(url);
        }
        logger.LogInformation("Deleted project {ProjectId}", id);
        return true;
    }

    private static ProjectResponse MapToResponse(Project p, Dictionary<string, string> t) => new()
    {
        Id = p.Id,
        Slug = p.Slug,
        ImageUrl = p.ImageUrl,
        Gallery = string.IsNullOrEmpty(p.GalleryJson) ? null : JsonSerializer.Deserialize<string[]>(p.GalleryJson),
        Name = t.GetValueOrDefault("Name", p.Name),
        Client = p.Client,
        Location = p.Location,
        Scale = p.Scale,
        Scope = p.Scope,
        Status = p.Status,
        Year = p.Year,
        Category = p.Category,
        CategoryId = p.ProjectCategoryId,
        Description = t.GetValueOrDefault("Description", p.Description),
        Challenges = DeserializeStringArray(t.GetValueOrDefault("Challenges", p.ChallengesJson)),
        Solutions = DeserializeStringArray(t.GetValueOrDefault("Solutions", p.SolutionsJson)),
        Highlights = string.IsNullOrEmpty(p.HighlightsJson) ? null : JsonSerializer.Deserialize<JsonElement>(p.HighlightsJson),
    };

    private static string[]? DeserializeStringArray(string? json) =>
        string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<string[]>(json);

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
