using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class ActivityCategoryService(AppDbContext db, ILogger<ActivityCategoryService> logger)
{

    public async Task<List<ActivityCategoryResponse>> GetAllAsync(bool includeInactive = false)
    {
        await SeedFromActivitiesIfEmptyAsync();

        var query = db.ActivityCategories.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        var items = await query
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToListAsync();

        logger.LogDebug("Fetched {Count} activity categories (includeInactive={IncludeInactive})", items.Count, includeInactive);
        return items.Select(MapToResponse).ToList();
    }

    public async Task<ActivityCategoryResponse> CreateAsync(UpsertActivityCategoryRequest req)
    {
        var nameVi = NormalizeName(!string.IsNullOrWhiteSpace(req.NameVi) ? req.NameVi : req.Name);
        await EnsureNameUniqueAsync(nameVi);

        var entity = new ActivityCategory
        {
            Name = nameVi,
            NameVi = nameVi,
            NameEn = (req.NameEn ?? "").Trim(),
            NameZh = (req.NameZh ?? "").Trim(),
            NameJa = (req.NameJa ?? "").Trim(),
            IsActive = req.IsActive,
            SortOrder = req.SortOrder,
        };

        db.ActivityCategories.Add(entity);
        await db.SaveChangesAsync();

        logger.LogInformation("Created activity category {CategoryId} ({CategoryName})", entity.Id, entity.Name);
        return MapToResponse(entity);
    }

    public async Task<ActivityCategoryResponse?> UpdateAsync(int id, UpsertActivityCategoryRequest req)
    {
        var entity = await db.ActivityCategories.FindAsync(id);
        if (entity == null)
        {
            logger.LogWarning("Cannot update activity category. Id {CategoryId} not found", id);
            return null;
        }

        var previousName = entity.Name;
        var nameVi = NormalizeName(!string.IsNullOrWhiteSpace(req.NameVi) ? req.NameVi : req.Name);
        await EnsureNameUniqueAsync(nameVi, id);

        entity.Name = nameVi;
        entity.NameVi = nameVi;
        entity.NameEn = (req.NameEn ?? "").Trim();
        entity.NameZh = (req.NameZh ?? "").Trim();
        entity.NameJa = (req.NameJa ?? "").Trim();
        entity.IsActive = req.IsActive;
        entity.SortOrder = req.SortOrder;
        entity.UpdatedAt = DateTime.UtcNow;

        await UpdateActivitiesForRenamedCategoryAsync(id, previousName, nameVi);

        await db.SaveChangesAsync();

        logger.LogInformation("Updated activity category {CategoryId} ({CategoryName})", entity.Id, entity.Name);
        return MapToResponse(entity);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await db.ActivityCategories.FindAsync(id);
        if (entity == null)
        {
            logger.LogWarning("Cannot delete activity category. Id {CategoryId} not found", id);
            return false;
        }

        var inUse = await db.Activities
            .AsNoTracking()
            .AnyAsync(a => a.ActivityCategoryId == id);

        if (inUse)
        {
            throw new InvalidOperationException("Danh mục đang được sử dụng trong bài đăng, không thể xóa.");
        }

        db.ActivityCategories.Remove(entity);
        await db.SaveChangesAsync();

        logger.LogInformation("Deleted activity category {CategoryId} ({CategoryName})", entity.Id, entity.Name);
        return true;
    }

    public async Task<(int? Id, string Name)> ResolveAsync(int? categoryId, string? categoryName)
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

        var trimmed = (categoryName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return (null, string.Empty);
        }

        var existing = await db.ActivityCategories
            .FirstOrDefaultAsync(c => c.Name.ToLower() == trimmed.ToLower());

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
            Name = trimmed,
            NameVi = trimmed,
            IsActive = true,
            SortOrder = maxSortOrder + 1,
        };
        db.ActivityCategories.Add(created);
        await db.SaveChangesAsync();
        logger.LogInformation("Auto-created activity category {CategoryName} from activity payload", trimmed);
        return (created.Id, created.Name);
    }

    private async Task SeedFromActivitiesIfEmptyAsync()
    {
        if (await db.ActivityCategories.AsNoTracking().AnyAsync())
        {
            return;
        }

        var categories = await db.Activities
            .AsNoTracking()
            .Select(a => a.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToListAsync();

        if (categories.Count == 0)
        {
            return;
        }

        var entities = categories
            .Select(NormalizeName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select((name, index) => new ActivityCategory
            {
                Name = name,
                NameVi = name,
                IsActive = true,
                SortOrder = index + 1,
            })
            .ToList();

        db.ActivityCategories.AddRange(entities);
        await db.SaveChangesAsync();

        logger.LogInformation("Seeded {Count} activity categories from activities data", entities.Count);
    }

    private async Task EnsureNameUniqueAsync(string name, int? excludingId = null)
    {
        var normalized = name.ToLower();

        var exists = await db.ActivityCategories
            .AsNoTracking()
            .AnyAsync(c => c.Name.ToLower() == normalized && (!excludingId.HasValue || c.Id != excludingId.Value));

        if (exists)
        {
            throw new InvalidOperationException("Danh mục đã tồn tại.");
        }
    }

    private async Task UpdateActivitiesForRenamedCategoryAsync(int categoryId, string previousName, string nextName)
    {
        if (string.Equals(previousName, nextName, StringComparison.Ordinal))
        {
            return;
        }

        var activities = await db.Activities
            .Where(a => a.ActivityCategoryId == categoryId)
            .ToListAsync();

        foreach (var activity in activities)
        {
            activity.Category = nextName;
            activity.UpdatedAt = DateTime.UtcNow;
        }

        if (activities.Count > 0)
        {
            logger.LogInformation(
                "Updated {Count} activities from category {PreviousCategoryName} to {NextCategoryName}",
                activities.Count,
                previousName,
                nextName);
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

    private static ActivityCategoryResponse MapToResponse(ActivityCategory item) => new()
    {
        Id = item.Id,
        Name = item.Name,
        NameVi = string.IsNullOrWhiteSpace(item.NameVi) ? item.Name : item.NameVi,
        NameEn = item.NameEn ?? "",
        NameZh = item.NameZh ?? "",
        NameJa = item.NameJa ?? "",
        IsActive = item.IsActive,
        SortOrder = item.SortOrder,
    };
}
