using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class ActivityCategoryService(AppDbContext db)
{
    private ILogger<ActivityCategoryService> Logger => db.GetService<ILoggerFactory>().CreateLogger<ActivityCategoryService>();

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

        Logger.LogDebug("Fetched {Count} activity categories (includeInactive={IncludeInactive})", items.Count, includeInactive);
        return items.Select(MapToResponse).ToList();
    }

    public async Task<ActivityCategoryResponse> CreateAsync(UpsertActivityCategoryRequest req)
    {
        var normalizedName = NormalizeName(req.Name);
        await EnsureNameUniqueAsync(normalizedName);

        var entity = new ActivityCategory
        {
            Name = normalizedName,
            IsActive = req.IsActive,
            SortOrder = req.SortOrder,
        };

        db.ActivityCategories.Add(entity);
        await db.SaveChangesAsync();

        Logger.LogInformation("Created activity category {CategoryId} ({CategoryName})", entity.Id, entity.Name);
        return MapToResponse(entity);
    }

    public async Task<ActivityCategoryResponse?> UpdateAsync(int id, UpsertActivityCategoryRequest req)
    {
        var entity = await db.ActivityCategories.FindAsync(id);
        if (entity == null)
        {
            Logger.LogWarning("Cannot update activity category. Id {CategoryId} not found", id);
            return null;
        }

        var previousName = entity.Name;
        var normalizedName = NormalizeName(req.Name);
        await EnsureNameUniqueAsync(normalizedName, id);

        entity.Name = normalizedName;
        entity.IsActive = req.IsActive;
        entity.SortOrder = req.SortOrder;
        entity.UpdatedAt = DateTime.UtcNow;

        await UpdateActivitiesForRenamedCategoryAsync(id, previousName, normalizedName);

        await db.SaveChangesAsync();

        Logger.LogInformation("Updated activity category {CategoryId} ({CategoryName})", entity.Id, entity.Name);
        return MapToResponse(entity);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await db.ActivityCategories.FindAsync(id);
        if (entity == null)
        {
            Logger.LogWarning("Cannot delete activity category. Id {CategoryId} not found", id);
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

        Logger.LogInformation("Deleted activity category {CategoryId} ({CategoryName})", entity.Id, entity.Name);
        return true;
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
                IsActive = true,
                SortOrder = index + 1,
            })
            .ToList();

        db.ActivityCategories.AddRange(entities);
        await db.SaveChangesAsync();

        Logger.LogInformation("Seeded {Count} activity categories from activities data", entities.Count);
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
            Logger.LogInformation(
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
        IsActive = item.IsActive,
        SortOrder = item.SortOrder,
    };
}
