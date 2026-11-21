using Microsoft.EntityFrameworkCore;
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
        var option = await db.MasterDataOptions.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Category == Category && o.Code == code, ct);
        return option is null ? null : (await MasterDataNames.LocalizeAsync(db, [option], lang, ct))[option.Id];
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
