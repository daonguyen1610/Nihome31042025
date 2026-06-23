using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class ProjectCategoryService(AppDbContext db, ILogger<ProjectCategoryService> logger)
{

    public async Task<List<ProjectCategoryResponse>> GetAllAsync(bool includeInactive = false)
    {
        await SeedFromProjectsIfEmptyAsync();

        var query = db.ProjectCategories.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        var items = await query
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToListAsync();

        logger.LogDebug("Fetched {Count} project categories (includeInactive={IncludeInactive})", items.Count, includeInactive);
        return items.Select(MapToResponse).ToList();
    }

    public async Task<ProjectCategoryResponse> CreateAsync(UpsertProjectCategoryRequest req)
    {
        var normalizedName = NormalizeName(req.Name);
        await EnsureNameUniqueAsync(normalizedName);

        var entity = new ProjectCategory
        {
            Name = normalizedName,
            IsActive = req.IsActive,
            SortOrder = req.SortOrder,
        };

        db.ProjectCategories.Add(entity);
        await db.SaveChangesAsync();

        logger.LogInformation("Created project category {CategoryId} ({CategoryName})", entity.Id, entity.Name);
        return MapToResponse(entity);
    }

    public async Task<ProjectCategoryResponse?> UpdateAsync(int id, UpsertProjectCategoryRequest req)
    {
        var entity = await db.ProjectCategories.FindAsync(id);
        if (entity == null)
        {
            logger.LogWarning("Cannot update project category. Id {CategoryId} not found", id);
            return null;
        }

        var previousName = entity.Name;
        var normalizedName = NormalizeName(req.Name);
        await EnsureNameUniqueAsync(normalizedName, id);

        entity.Name = normalizedName;
        entity.IsActive = req.IsActive;
        entity.SortOrder = req.SortOrder;
        entity.UpdatedAt = DateTime.UtcNow;

        await UpdateProjectsForRenamedCategoryAsync(id, previousName, normalizedName);

        await db.SaveChangesAsync();

        logger.LogInformation("Updated project category {CategoryId} ({CategoryName})", entity.Id, entity.Name);
        return MapToResponse(entity);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await db.ProjectCategories.FindAsync(id);
        if (entity == null)
        {
            logger.LogWarning("Cannot delete project category. Id {CategoryId} not found", id);
            return false;
        }

        var inUse = await db.Projects
            .AsNoTracking()
            .AnyAsync(p => p.ProjectCategoryId == id);

        if (inUse)
        {
            throw new InvalidOperationException("Danh mục đang được sử dụng trong dự án, không thể xóa.");
        }

        db.ProjectCategories.Remove(entity);
        await db.SaveChangesAsync();

        logger.LogInformation("Deleted project category {CategoryId} ({CategoryName})", entity.Id, entity.Name);
        return true;
    }

    public async Task<(int? Id, string Name)> ResolveAsync(int? categoryId, string? categoryName)
    {
        if (categoryId.HasValue)
        {
            var byId = await db.ProjectCategories.FindAsync(categoryId.Value);
            if (byId == null)
            {
                throw new InvalidOperationException("Danh mục dự án không tồn tại.");
            }
            return (byId.Id, byId.Name);
        }

        var trimmed = (categoryName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return (null, string.Empty);
        }

        var existing = await db.ProjectCategories
            .FirstOrDefaultAsync(c => c.Name.ToLower() == trimmed.ToLower());

        if (existing != null)
        {
            return (existing.Id, existing.Name);
        }

        var maxSortOrder = await db.ProjectCategories
            .AsNoTracking()
            .Select(c => (int?)c.SortOrder)
            .MaxAsync() ?? 0;

        var created = new ProjectCategory
        {
            Name = trimmed,
            IsActive = true,
            SortOrder = maxSortOrder + 1,
        };
        db.ProjectCategories.Add(created);
        await db.SaveChangesAsync();
        logger.LogInformation("Auto-created project category {CategoryName} from project payload", trimmed);
        return (created.Id, created.Name);
    }

    private async Task SeedFromProjectsIfEmptyAsync()
    {
        if (await db.ProjectCategories.AsNoTracking().AnyAsync())
        {
            return;
        }

        var categories = await db.Projects
            .AsNoTracking()
            .Select(p => p.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToListAsync();

        if (categories.Count == 0)
        {
            return;
        }

        var entities = categories
            .Select(c => (c ?? string.Empty).Trim())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select((name, index) => new ProjectCategory
            {
                Name = name,
                IsActive = true,
                SortOrder = index + 1,
            })
            .ToList();

        db.ProjectCategories.AddRange(entities);
        await db.SaveChangesAsync();

        logger.LogInformation("Seeded {Count} project categories from projects data", entities.Count);
    }

    private async Task EnsureNameUniqueAsync(string name, int? excludingId = null)
    {
        var normalized = name.ToLower();

        var exists = await db.ProjectCategories
            .AsNoTracking()
            .AnyAsync(c => c.Name.ToLower() == normalized && (!excludingId.HasValue || c.Id != excludingId.Value));

        if (exists)
        {
            throw new InvalidOperationException("Danh mục đã tồn tại.");
        }
    }

    private async Task UpdateProjectsForRenamedCategoryAsync(int categoryId, string previousName, string nextName)
    {
        if (string.Equals(previousName, nextName, StringComparison.Ordinal))
        {
            return;
        }

        var projects = await db.Projects
            .Where(p => p.ProjectCategoryId == categoryId)
            .ToListAsync();

        foreach (var project in projects)
        {
            project.Category = nextName;
            project.UpdatedAt = DateTime.UtcNow;
        }

        if (projects.Count > 0)
        {
            logger.LogInformation(
                "Updated {Count} projects from category {PreviousCategoryName} to {NextCategoryName}",
                projects.Count, previousName, nextName);
        }
    }

    private static string NormalizeName(string name)
    {
        var normalized = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Tên danh mục không được để trống.");
        }
        return normalized;
    }

    private static ProjectCategoryResponse MapToResponse(ProjectCategory item) => new()
    {
        Id = item.Id,
        Name = item.Name,
        IsActive = item.IsActive,
        SortOrder = item.SortOrder,
    };
}
