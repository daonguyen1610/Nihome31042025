using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>
/// Service for managing as-built document categories (CRUD).
/// Replaces the old hardcoded <c>AsBuiltCategory</c> enum with dynamic categories.
/// </summary>
public class AsBuiltDocumentCategoryService(AppDbContext db, ILogger<AsBuiltDocumentCategoryService> logger)
{
    public async Task<List<AsBuiltDocumentCategoryResponse>> GetAllAsync(bool includeInactive = false)
    {
        await SeedDefaultCategoriesIfEmptyAsync();

        var query = db.AsBuiltDocumentCategories.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        var items = await query
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new
            {
                Category = c,
                DocumentCount = db.AsBuiltDocuments.Count(d => d.CategoryId == c.Id)
            })
            .ToListAsync();

        logger.LogDebug("Fetched {Count} as-built document categories (includeInactive={IncludeInactive})", items.Count, includeInactive);
        return items.Select(x => MapToResponse(x.Category, x.DocumentCount)).ToList();
    }

    public async Task<AsBuiltDocumentCategoryResponse?> GetByIdAsync(int id)
    {
        var entity = await db.AsBuiltDocumentCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);

        if (entity == null)
        {
            return null;
        }

        var documentCount = await db.AsBuiltDocuments.CountAsync(d => d.CategoryId == id);
        return MapToResponse(entity, documentCount);
    }

    public async Task<AsBuiltDocumentCategoryResponse> CreateAsync(UpsertAsBuiltDocumentCategoryRequest req)
    {
        var code = NormalizeCode(req.Code);
        var name = NormalizeName(req.Name, req.NameVi);

        await EnsureCodeUniqueAsync(code);
        await EnsureNameUniqueAsync(name);

        var entity = new AsBuiltDocumentCategory
        {
            Code = code,
            Name = name,
            NameVi = name,
            NameEn = ResolveLegacyTranslation(req.NameEn, name),
            NameZh = ResolveLegacyTranslation(req.NameZh, name),
            NameJa = ResolveLegacyTranslation(req.NameJa, name),
            IsRequired = req.IsRequired,
            IsActive = req.IsActive,
            SortOrder = req.SortOrder,
        };

        db.AsBuiltDocumentCategories.Add(entity);
        await db.SaveChangesAsync();

        logger.LogInformation("Created as-built document category {CategoryId} ({CategoryCode})", entity.Id, entity.Code);
        return MapToResponse(entity, 0);
    }

    public async Task<AsBuiltDocumentCategoryResponse?> UpdateAsync(int id, UpsertAsBuiltDocumentCategoryRequest req)
    {
        var entity = await db.AsBuiltDocumentCategories.FindAsync(id);
        if (entity == null)
        {
            logger.LogWarning("Cannot update as-built document category. Id {CategoryId} not found", id);
            return null;
        }

        var code = NormalizeCode(req.Code);
        if (!string.Equals(entity.Code, code, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Mã danh mục không thể thay đổi sau khi tạo.");
        }
        var previousSourceName = entity.NameVi;
        var name = NormalizeName(req.Name, req.NameVi);

        await EnsureCodeUniqueAsync(code, id);
        await EnsureNameUniqueAsync(name, id);

        entity.Name = name;
        entity.NameVi = name;
        entity.NameEn = req.NameEn == null
            ? SynchronizeFallbackTranslation(entity.NameEn, previousSourceName, name)
            : req.NameEn.Trim();
        entity.NameZh = req.NameZh == null
            ? SynchronizeFallbackTranslation(entity.NameZh, previousSourceName, name)
            : req.NameZh.Trim();
        entity.NameJa = req.NameJa == null
            ? SynchronizeFallbackTranslation(entity.NameJa, previousSourceName, name)
            : req.NameJa.Trim();
        entity.IsRequired = req.IsRequired;
        entity.IsActive = req.IsActive;
        entity.SortOrder = req.SortOrder;
        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        var documentCount = await db.AsBuiltDocuments.CountAsync(d => d.CategoryId == id);
        logger.LogInformation("Updated as-built document category {CategoryId} ({CategoryCode})", entity.Id, entity.Code);
        return MapToResponse(entity, documentCount);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await db.AsBuiltDocumentCategories.FindAsync(id);
        if (entity == null)
        {
            logger.LogWarning("Cannot delete as-built document category. Id {CategoryId} not found", id);
            return false;
        }

        var inUse = await db.AsBuiltDocuments
            .AsNoTracking()
            .AnyAsync(d => d.CategoryId == id);

        if (inUse)
        {
            throw new InvalidOperationException("Danh mục đang được sử dụng trong hồ sơ hoàn công, không thể xóa. Vui lòng vô hiệu hóa thay vì xóa.");
        }

        db.AsBuiltDocumentCategories.Remove(entity);
        await db.SaveChangesAsync();

        logger.LogInformation("Deleted as-built document category {CategoryId} ({CategoryCode})", entity.Id, entity.Code);
        return true;
    }

    /// <summary>
    /// Resolve category by ID or create from code if needed.
    /// Used by AsBuiltDocumentService to handle category references.
    /// </summary>
    public async Task<int> ResolveCategoryIdAsync(
        int? categoryId,
        string? categoryCode,
        int? allowedInactiveCategoryId = null)
    {
        if (categoryId.HasValue)
        {
            var exists = await db.AsBuiltDocumentCategories.AnyAsync(c =>
                c.Id == categoryId.Value && (c.IsActive || c.Id == allowedInactiveCategoryId));
            if (!exists)
            {
                throw new InvalidOperationException($"Danh mục hồ sơ không tồn tại hoặc đã bị vô hiệu hóa (ID: {categoryId.Value}).");
            }
            return categoryId.Value;
        }

        if (string.IsNullOrWhiteSpace(categoryCode))
        {
            throw new InvalidOperationException("Vui lòng chọn danh mục hồ sơ.");
        }

        var byCode = await db.AsBuiltDocumentCategories
            .FirstOrDefaultAsync(c =>
                c.Code.ToLower() == categoryCode.ToLower()
                && (c.IsActive || c.Id == allowedInactiveCategoryId));

        if (byCode != null)
        {
            return byCode.Id;
        }

        throw new InvalidOperationException($"Danh mục '{categoryCode}' không hợp lệ hoặc đã bị vô hiệu hóa.");
    }

    /// <summary>
    /// Get all required category IDs for handover completeness checks.
    /// </summary>
    public async Task<int[]> GetRequiredCategoryIdsAsync()
    {
        return await db.AsBuiltDocumentCategories
            .AsNoTracking()
            .Where(c => c.IsRequired && c.IsActive)
            .Select(c => c.Id)
            .ToArrayAsync();
    }

    /// <summary>
    /// Seed default categories if none exist.
    /// Called on first access to ensure data consistency.
    /// </summary>
    private async Task SeedDefaultCategoriesIfEmptyAsync()
    {
        if (await db.AsBuiltDocumentCategories.AsNoTracking().AnyAsync())
        {
            return;
        }

        var defaults = new List<AsBuiltDocumentCategory>
        {
            new()
            {
                Code = AsBuiltCategoryCodes.Drawing,
                Name = "Bản vẽ hoàn công",
                NameVi = "Bản vẽ hoàn công",
                NameEn = "As-built drawings",
                NameZh = "竣工图纸",
                NameJa = "竣工図面",
                IsRequired = true,
                IsActive = true,
                SortOrder = 1,
            },
            new()
            {
                Code = AsBuiltCategoryCodes.AcceptanceMinute,
                Name = "Biên bản nghiệm thu",
                NameVi = "Biên bản nghiệm thu",
                NameEn = "Acceptance minutes",
                NameZh = "验收记录",
                NameJa = "検収議事録",
                IsRequired = true,
                IsActive = true,
                SortOrder = 2,
            },
            new()
            {
                Code = AsBuiltCategoryCodes.TestReport,
                Name = "Báo cáo thí nghiệm",
                NameVi = "Báo cáo thí nghiệm",
                NameEn = "Test reports",
                NameZh = "测试报告",
                NameJa = "試験報告書",
                IsRequired = true,
                IsActive = true,
                SortOrder = 3,
            },
            new()
            {
                Code = AsBuiltCategoryCodes.WarrantyCertificate,
                Name = "Chứng chỉ bảo hành",
                NameVi = "Chứng chỉ bảo hành",
                NameEn = "Warranty certificates",
                NameZh = "保修证书",
                NameJa = "保証書",
                IsRequired = true,
                IsActive = true,
                SortOrder = 4,
            },
            new()
            {
                Code = AsBuiltCategoryCodes.Other,
                Name = "Tài liệu khác",
                NameVi = "Tài liệu khác",
                NameEn = "Other supporting documents",
                NameZh = "其他支持文件",
                NameJa = "その他の書類",
                IsRequired = false,
                IsActive = true,
                SortOrder = 5,
            },
        };

        db.AsBuiltDocumentCategories.AddRange(defaults);
        await db.SaveChangesAsync();

        logger.LogInformation("Seeded {Count} default as-built document categories", defaults.Count);
    }

    private async Task EnsureCodeUniqueAsync(string code, int? excludingId = null)
    {
        var normalized = code.ToLower();

        var exists = await db.AsBuiltDocumentCategories
            .AsNoTracking()
            .AnyAsync(c => c.Code.ToLower() == normalized && (!excludingId.HasValue || c.Id != excludingId.Value));

        if (exists)
        {
            throw new InvalidOperationException($"Mã danh mục '{code}' đã tồn tại.");
        }
    }

    private async Task EnsureNameUniqueAsync(string name, int? excludingId = null)
    {
        var normalized = name.ToLower();

        var exists = await db.AsBuiltDocumentCategories
            .AsNoTracking()
            .AnyAsync(c => c.Name.ToLower() == normalized && (!excludingId.HasValue || c.Id != excludingId.Value));

        if (exists)
        {
            throw new InvalidOperationException("Danh mục đã tồn tại.");
        }
    }

    private static string NormalizeCode(string code)
    {
        var normalized = (code ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Mã danh mục không được để trống. Ví dụ: Drawing.");
        }

        // Validate code format: alphanumeric with optional underscores
        if (!System.Text.RegularExpressions.Regex.IsMatch(normalized, @"^[A-Za-z][A-Za-z0-9_]*$"))
        {
            throw new InvalidOperationException("Mã danh mục phải bắt đầu bằng chữ cái và chỉ chứa chữ cái, số hoặc dấu gạch dưới. Ví dụ: ConstructionPhoto.");
        }

        return normalized;
    }

    private static string NormalizeName(string? name, string? legacyNameVi)
    {
        var normalized = (!string.IsNullOrWhiteSpace(name) ? name : legacyNameVi ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Tên danh mục không được để trống. Ví dụ: Bản vẽ hoàn công.");
        }

        return normalized;
    }

    private static string SynchronizeFallbackTranslation(
        string translation,
        string previousSourceName,
        string newSourceName) =>
        string.IsNullOrWhiteSpace(translation)
        || string.Equals(translation, previousSourceName, StringComparison.Ordinal)
            ? newSourceName
            : translation;

    private static string ResolveLegacyTranslation(string? translation, string sourceName) =>
        translation?.Trim() ?? sourceName;

    private static AsBuiltDocumentCategoryResponse MapToResponse(AsBuiltDocumentCategory item, int documentCount) => new()
    {
        Id = item.Id,
        Code = item.Code,
        Name = item.Name,
        NameVi = string.IsNullOrWhiteSpace(item.NameVi) ? item.Name : item.NameVi,
        NameEn = item.NameEn ?? "",
        NameZh = item.NameZh ?? "",
        NameJa = item.NameJa ?? "",
        IsRequired = item.IsRequired,
        IsActive = item.IsActive,
        SortOrder = item.SortOrder,
        DocumentCount = documentCount,
    };
}
