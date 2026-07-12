using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>
/// Generic master-data catalogue for CRM / Design / Permitting dropdowns.
///
/// Read paths are cached for 5 minutes via <see cref="IMemoryCache"/>.
/// Every write invalidates the affected category and the
/// <c>categories</c> summary entry, so admins see edits immediately.
/// </summary>
public class MasterDataService(
    AppDbContext db,
    IMemoryCache cache,
    ILogger<MasterDataService> logger) : IMasterDataService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private const string CategoriesCacheKey = "master-data:categories";

    public async Task<List<MasterDataCategoryResponse>> GetCategoriesAsync(CancellationToken ct = default)
    {
        if (cache.TryGetValue(CategoriesCacheKey, out List<MasterDataCategoryResponse>? cached) && cached != null)
        {
            return cached;
        }

        var rows = await db.MasterDataOptions
            .AsNoTracking()
            .GroupBy(o => o.Category)
            .Select(g => new MasterDataCategoryResponse
            {
                Category = g.Key,
                TotalCount = g.Count(),
                ActiveCount = g.Count(o => o.IsActive),
            })
            .OrderBy(r => r.Category)
            .ToListAsync(ct);

        cache.Set(CategoriesCacheKey, rows, CacheTtl);
        return rows;
    }

    public async Task<List<MasterDataOptionResponse>> GetByCategoryAsync(
        string category,
        bool includeInactive = false,
        CancellationToken ct = default)
    {
        var normalized = NormalizeCategory(category);
        var cacheKey = BuildCategoryCacheKey(normalized, includeInactive);
        if (cache.TryGetValue(cacheKey, out List<MasterDataOptionResponse>? cached) && cached != null)
        {
            return cached;
        }

        var query = db.MasterDataOptions
            .AsNoTracking()
            .Where(o => o.Category == normalized);

        if (!includeInactive)
        {
            query = query.Where(o => o.IsActive);
        }

        var items = await query
            .OrderBy(o => o.SortOrder)
            .ThenBy(o => o.Name)
            .Select(o => MapToResponse(o))
            .ToListAsync(ct);

        cache.Set(cacheKey, items, CacheTtl);
        return items;
    }

    public async Task<MasterDataOptionResponse?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.MasterDataOptions
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, ct);
        return entity == null ? null : MapToResponse(entity);
    }

    public async Task<MasterDataOptionResponse> CreateAsync(
        string category,
        UpsertMasterDataOptionRequest req,
        CancellationToken ct = default)
    {
        var normalizedCategory = NormalizeCategory(category);
        var normalizedCode = NormalizeCode(req.Code);

        if (await CodeExistsAsync(normalizedCategory, normalizedCode, excludeId: null, ct))
        {
            throw new MasterDataDuplicateCodeException(normalizedCategory, normalizedCode);
        }

        var entity = new MasterDataOption
        {
            Category = normalizedCategory,
            Code = normalizedCode,
            Name = req.Name.Trim(),
            LabelKey = string.IsNullOrWhiteSpace(req.LabelKey) ? null : req.LabelKey.Trim(),
            Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim(),
            IsActive = req.IsActive,
            SortOrder = req.SortOrder,
        };

        db.MasterDataOptions.Add(entity);
        await db.SaveChangesAsync(ct);
        InvalidateCache(normalizedCategory);
        logger.LogInformation(
            "Created master-data option {Id} ({Category}/{Code})", entity.Id, entity.Category, entity.Code);
        return MapToResponse(entity);
    }

    public async Task<MasterDataOptionResponse?> UpdateAsync(
        int id,
        UpsertMasterDataOptionRequest req,
        CancellationToken ct = default)
    {
        var entity = await db.MasterDataOptions.FindAsync(new object?[] { id }, ct);
        if (entity == null)
        {
            return null;
        }

        var normalizedCode = NormalizeCode(req.Code);

        if (await CodeExistsAsync(entity.Category, normalizedCode, excludeId: id, ct))
        {
            throw new MasterDataDuplicateCodeException(entity.Category, normalizedCode);
        }

        entity.Code = normalizedCode;
        entity.Name = req.Name.Trim();
        entity.LabelKey = string.IsNullOrWhiteSpace(req.LabelKey) ? null : req.LabelKey.Trim();
        entity.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();
        entity.IsActive = req.IsActive;
        entity.SortOrder = req.SortOrder;
        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        InvalidateCache(entity.Category);
        logger.LogInformation(
            "Updated master-data option {Id} ({Category}/{Code})", entity.Id, entity.Category, entity.Code);
        return MapToResponse(entity);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.MasterDataOptions.FindAsync(new object?[] { id }, ct);
        if (entity == null)
        {
            return false;
        }

        db.MasterDataOptions.Remove(entity);
        await db.SaveChangesAsync(ct);
        InvalidateCache(entity.Category);
        logger.LogInformation(
            "Deleted master-data option {Id} ({Category}/{Code})", entity.Id, entity.Category, entity.Code);
        return true;
    }

    private async Task<bool> CodeExistsAsync(string category, string code, int? excludeId, CancellationToken ct)
    {
        return await db.MasterDataOptions
            .AsNoTracking()
            .AnyAsync(o => o.Category == category && o.Code == code && (excludeId == null || o.Id != excludeId), ct);
    }

    private void InvalidateCache(string category)
    {
        cache.Remove(CategoriesCacheKey);
        cache.Remove(BuildCategoryCacheKey(category, includeInactive: true));
        cache.Remove(BuildCategoryCacheKey(category, includeInactive: false));
    }

    private static string BuildCategoryCacheKey(string category, bool includeInactive) =>
        $"master-data:category:{category}:{(includeInactive ? "all" : "active")}";

    private static string NormalizeCategory(string value) =>
        (value ?? string.Empty).Trim().ToLowerInvariant();

    private static string NormalizeCode(string value) =>
        (value ?? string.Empty).Trim().ToLowerInvariant();

    private static MasterDataOptionResponse MapToResponse(MasterDataOption entity) => new()
    {
        Id = entity.Id,
        Category = entity.Category,
        Code = entity.Code,
        Name = entity.Name,
        LabelKey = entity.LabelKey,
        Description = entity.Description,
        IsActive = entity.IsActive,
        SortOrder = entity.SortOrder,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
    };
}
