using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class RecruitmentMetadataService(
    AppDbContext db,
    TranslationService translationService)
{
    public async Task<RecruitmentMetadataResponse> GetAsync(string lang = "vi", bool includeInactive = false)
    {
        var items = await QueryItems(includeInactive).ToListAsync();
        var translations = await GetTranslationsAsync(lang, items);

        return new RecruitmentMetadataResponse
        {
            EmploymentTypes = MapOptions(items, RecruitmentMetadataGroups.EmploymentType, translations),
            ExperienceLevels = MapOptions(items, RecruitmentMetadataGroups.ExperienceLevel, translations),
            ApplicationStatuses = MapOptions(items, RecruitmentMetadataGroups.ApplicationStatus, translations),
        };
    }

    public async Task<List<RecruitmentMetadataItemResponse>> GetAllItemsAsync(bool includeInactive = false)
    {
        var items = await QueryItems(includeInactive).ToListAsync();
        return items.Select(MapItem).ToList();
    }

    public async Task<RecruitmentMetadataItemResponse> CreateAsync(UpsertRecruitmentMetadataItemRequest req)
    {
        var groupKey = NormalizeGroupKey(req.GroupKey);
        var value = NormalizeValue(req.Value);
        var label = NormalizeLabel(req.Label);
        var translationKey = NormalizeTranslationKey(req.TranslationKey);

        await EnsureUniqueAsync(groupKey, value);

        var entity = new RecruitmentMetadataItem
        {
            GroupKey = groupKey,
            Value = value,
            Label = label,
            TranslationKey = translationKey,
            IsActive = req.IsActive,
            SortOrder = req.SortOrder,
        };

        db.RecruitmentMetadataItems.Add(entity);
        await db.SaveChangesAsync();

        return MapItem(entity);
    }

    public async Task<RecruitmentMetadataItemResponse?> UpdateAsync(int id, UpsertRecruitmentMetadataItemRequest req)
    {
        var entity = await db.RecruitmentMetadataItems.FindAsync(id);
        if (entity == null) return null;

        var groupKey = NormalizeGroupKey(req.GroupKey);
        var value = NormalizeValue(req.Value);
        var label = NormalizeLabel(req.Label);
        var translationKey = NormalizeTranslationKey(req.TranslationKey);

        if ((!string.Equals(entity.GroupKey, groupKey, StringComparison.Ordinal) ||
             !string.Equals(entity.Value, value, StringComparison.Ordinal)) &&
            await IsInUseAsync(entity.GroupKey, entity.Value))
        {
            throw new InvalidOperationException("Metadata đang được sử dụng, không thể thay đổi mã hoặc nhóm.");
        }

        await EnsureUniqueAsync(groupKey, value, id);

        entity.GroupKey = groupKey;
        entity.Value = value;
        entity.Label = label;
        entity.TranslationKey = translationKey;
        entity.IsActive = req.IsActive;
        entity.SortOrder = req.SortOrder;
        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return MapItem(entity);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await db.RecruitmentMetadataItems.FindAsync(id);
        if (entity == null) return false;

        var isInUse = await IsInUseAsync(entity.GroupKey, entity.Value);

        if (isInUse)
        {
            throw new InvalidOperationException("Metadata đang được sử dụng, không thể xóa.");
        }

        db.RecruitmentMetadataItems.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }

    private async Task<bool> IsInUseAsync(string groupKey, string value)
    {
        return groupKey switch
        {
            RecruitmentMetadataGroups.EmploymentType => await db.JobPositions
                .AsNoTracking()
                .AnyAsync(item => item.EmploymentType == value),
            RecruitmentMetadataGroups.ExperienceLevel => await db.JobPositions
                .AsNoTracking()
                .AnyAsync(item => item.ExperienceLevel == value),
            RecruitmentMetadataGroups.ApplicationStatus => await db.JobApplications
                .AsNoTracking()
                .AnyAsync(item => item.Status == value),
            _ => false,
        };
    }

    public async Task EnsureOptionExistsAsync(string groupKey, string value)
    {
        var normalizedGroupKey = NormalizeGroupKey(groupKey);
        var normalizedValue = NormalizeValue(value);

        var exists = await db.RecruitmentMetadataItems
            .AsNoTracking()
            .AnyAsync(item =>
                item.GroupKey == normalizedGroupKey &&
                item.Value == normalizedValue &&
                item.IsActive);

        if (!exists)
        {
            throw new InvalidOperationException($"Metadata không hợp lệ: {normalizedValue}");
        }
    }

    public async Task<string> GetDefaultOptionValueAsync(string groupKey)
    {
        var normalizedGroupKey = NormalizeGroupKey(groupKey);

        var value = await db.RecruitmentMetadataItems
            .AsNoTracking()
            .Where(item => item.GroupKey == normalizedGroupKey && item.IsActive)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Label)
            .Select(item => item.Value)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Không tìm thấy metadata mặc định cho nhóm: {normalizedGroupKey}");
        }

        return value;
    }

    private IQueryable<RecruitmentMetadataItem> QueryItems(bool includeInactive)
    {
        var query = db.RecruitmentMetadataItems.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(item => item.IsActive);
        }

        return query
            .OrderBy(item => item.GroupKey)
            .ThenBy(item => item.SortOrder)
            .ThenBy(item => item.Label);
    }

    private async Task<Dictionary<string, string>> GetTranslationsAsync(
        string lang,
        IReadOnlyCollection<RecruitmentMetadataItem> items)
    {
        if (string.Equals(lang, "vi", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        if (!items.Any(item => !string.IsNullOrWhiteSpace(item.TranslationKey)))
        {
            return [];
        }

        return await translationService.GetTranslationMapAsync(lang);
    }

    private static List<RecruitmentOptionResponse> MapOptions(
        IEnumerable<RecruitmentMetadataItem> items,
        string groupKey,
        IReadOnlyDictionary<string, string> translations)
    {
        return items
            .Where(item => item.GroupKey == groupKey)
            .Select(item => new RecruitmentOptionResponse
            {
                Id = item.Id,
                Value = item.Value,
                Label = ResolveLabel(item, translations),
                IsActive = item.IsActive,
                SortOrder = item.SortOrder,
            })
            .ToList();
    }

    private static RecruitmentMetadataItemResponse MapItem(RecruitmentMetadataItem item) => new()
    {
        Id = item.Id,
        GroupKey = item.GroupKey,
        Value = item.Value,
        Label = item.Label,
        TranslationKey = item.TranslationKey,
        IsActive = item.IsActive,
        SortOrder = item.SortOrder,
    };

    private static string ResolveLabel(
        RecruitmentMetadataItem item,
        IReadOnlyDictionary<string, string> translations)
    {
        if (!string.IsNullOrWhiteSpace(item.TranslationKey) &&
            translations.TryGetValue(item.TranslationKey, out var translatedLabel) &&
            !string.IsNullOrWhiteSpace(translatedLabel))
        {
            return translatedLabel;
        }

        return item.Label;
    }

    private async Task EnsureUniqueAsync(string groupKey, string value, int? excludingId = null)
    {
        var exists = await db.RecruitmentMetadataItems
            .AsNoTracking()
            .AnyAsync(item =>
                item.GroupKey == groupKey &&
                item.Value == value &&
                (!excludingId.HasValue || item.Id != excludingId.Value));

        if (exists)
        {
            throw new InvalidOperationException("Metadata đã tồn tại.");
        }
    }

    private static string NormalizeGroupKey(string value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (!RecruitmentMetadataGroups.All.Contains(normalized, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"Nhóm metadata không hợp lệ: {value}");
        }

        return normalized;
    }

    private static string NormalizeValue(string value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Giá trị metadata không được để trống.");
        }

        return normalized;
    }

    private static string NormalizeLabel(string value)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Nhãn metadata không được để trống.");
        }

        return normalized;
    }

    private static string? NormalizeTranslationKey(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
