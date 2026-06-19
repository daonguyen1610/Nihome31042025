using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class NewsCategoryService(AppDbContext db)
{
    private ILogger<NewsCategoryService> Logger => db.GetService<ILoggerFactory>().CreateLogger<NewsCategoryService>();

    public async Task<List<NewsCategoryResponse>> GetAllAsync(bool includeInactive = false)
    {
        await SeedFromNewsIfEmptyAsync();

        var query = db.NewsCategories.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        var items = await query
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToListAsync();

        Logger.LogDebug("Fetched {Count} news categories (includeInactive={IncludeInactive})", items.Count, includeInactive);
        return items.Select(MapToResponse).ToList();
    }

    public async Task<NewsCategoryResponse> CreateAsync(UpsertNewsCategoryRequest req)
    {
        var normalizedName = NormalizeName(req.Name);
        await EnsureNameUniqueAsync(normalizedName);

        var entity = new NewsCategory
        {
            Name = normalizedName,
            IsActive = req.IsActive,
            SortOrder = req.SortOrder,
        };

        db.NewsCategories.Add(entity);
        await db.SaveChangesAsync();

        Logger.LogInformation("Created news category {CategoryId} ({CategoryName})", entity.Id, entity.Name);
        return MapToResponse(entity);
    }

    public async Task<NewsCategoryResponse?> UpdateAsync(int id, UpsertNewsCategoryRequest req)
    {
        var entity = await db.NewsCategories.FindAsync(id);
        if (entity == null)
        {
            Logger.LogWarning("Cannot update news category. Id {CategoryId} not found", id);
            return null;
        }

        var previousName = entity.Name;
        var normalizedName = NormalizeName(req.Name);
        await EnsureNameUniqueAsync(normalizedName, id);

        entity.Name = normalizedName;
        entity.IsActive = req.IsActive;
        entity.SortOrder = req.SortOrder;
        entity.UpdatedAt = DateTime.UtcNow;

        await UpdateNewsForRenamedCategoryAsync(id, previousName, normalizedName);
        await db.SaveChangesAsync();

        Logger.LogInformation("Updated news category {CategoryId} ({CategoryName})", entity.Id, entity.Name);
        return MapToResponse(entity);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await db.NewsCategories.FindAsync(id);
        if (entity == null)
        {
            Logger.LogWarning("Cannot delete news category. Id {CategoryId} not found", id);
            return false;
        }

        var inUse = await db.NewsArticles
            .AsNoTracking()
            .AnyAsync(n => n.NewsCategoryId == id);

        if (inUse)
        {
            throw new InvalidOperationException("Danh mục đang được sử dụng trong tin tức, không thể xóa.");
        }

        db.NewsCategories.Remove(entity);
        await db.SaveChangesAsync();

        Logger.LogInformation("Deleted news category {CategoryId} ({CategoryName})", entity.Id, entity.Name);
        return true;
    }

    private async Task SeedFromNewsIfEmptyAsync()
    {
        if (await db.NewsCategories.AsNoTracking().AnyAsync())
        {
            return;
        }

        var categories = await db.NewsArticles
            .AsNoTracking()
            .Select(n => n.Category)
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
            .Select((name, index) => new NewsCategory
            {
                Name = name,
                IsActive = true,
                SortOrder = index + 1,
            })
            .ToList();

        db.NewsCategories.AddRange(entities);
        await db.SaveChangesAsync();

        Logger.LogInformation("Seeded {Count} news categories from news data", entities.Count);
    }

    private async Task EnsureNameUniqueAsync(string name, int? excludingId = null)
    {
        var normalized = name.ToLower();

        var exists = await db.NewsCategories
            .AsNoTracking()
            .AnyAsync(c => c.Name.ToLower() == normalized && (!excludingId.HasValue || c.Id != excludingId.Value));

        if (exists)
        {
            throw new InvalidOperationException("Danh mục đã tồn tại.");
        }
    }

    private async Task UpdateNewsForRenamedCategoryAsync(int categoryId, string previousName, string nextName)
    {
        if (string.Equals(previousName, nextName, StringComparison.Ordinal))
        {
            return;
        }

        var news = await db.NewsArticles
            .Where(n => n.NewsCategoryId == categoryId)
            .ToListAsync();

        foreach (var item in news)
        {
            item.Category = nextName;
            item.UpdatedAt = DateTime.UtcNow;
        }

        if (news.Count > 0)
        {
            Logger.LogInformation(
                "Updated {Count} news articles from category {PreviousCategoryName} to {NextCategoryName}",
                news.Count,
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

    private static NewsCategoryResponse MapToResponse(NewsCategory item) => new()
    {
        Id = item.Id,
        Name = item.Name,
        IsActive = item.IsActive,
        SortOrder = item.SortOrder,
    };
}
