using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Data;

namespace NihomeBackend.Services;

/// <summary>
/// NICON's business sectors (master data <c>business_sector</c>), used by customers and projects.
/// </summary>
public static class BusinessSectors
{
    public const string Category = "business_sector";
    public const string NotInListMessage = "Lĩnh vực kinh doanh đã chọn không có trong danh mục. Hãy chọn lại.";

    /// <summary>
    /// The sector's name in a language by the master-data naming rule, also for one switched off;
    /// null when unknown. The one lookup used wherever a record names its sector.
    /// </summary>
    public static async Task<string?> NameAsync(AppDbContext db, string? code, string lang, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(code)) return null;
        var isSource = lang == "vi";
        // One query: the option with its translations in that language.
        var option = await db.MasterDataOptions.AsNoTracking()
            .Where(o => o.Category == Category && o.Code == code)
            .Select(o => new
            {
                o.Name,
                Entered = isSource ? null : db.EntityTranslations
                    .Where(row => row.EntityType == EntityTypes.MasterDataOption && row.EntityId == o.Id &&
                        row.LanguageCode == lang && row.FieldName == "Name")
                    .Select(row => row.Value).FirstOrDefault(),
                Seeded = isSource || o.LabelKey == null ? null : db.Translations
                    .Where(row => row.Key == o.LabelKey && row.LanguageCode == lang)
                    .Select(row => row.Value).FirstOrDefault(),
            })
            .FirstOrDefaultAsync(ct);
        return option is null ? null : MasterDataNames.Pick(option.Name, option.Entered, option.Seeded);
    }

    /// <summary>
    /// The trimmed code, or null when none was chosen; false when it is not in the active list.
    /// A sector an admin later switched off stays allowed on records that already have it.
    /// </summary>
    public static async Task<(bool Allowed, string? Code)> CheckAsync(
        AppDbContext db, string? code, string? current, CancellationToken ct)
    {
        var normalized = code?.Trim();
        if (string.IsNullOrEmpty(normalized)) return (true, null);
        if (normalized == current) return (true, normalized);
        var active = await db.MasterDataOptions.AsNoTracking().AnyAsync(
            o => o.Category == Category && o.Code == normalized && o.IsActive, ct);
        return (active, normalized);
    }
}
