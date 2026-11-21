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
    /// The sector's master-data name and translation key, also for one switched off; nulls when
    /// unknown. The one lookup used wherever a sector is named.
    /// </summary>
    public static async Task<(string? Name, string? LabelKey)> LabelAsync(
        AppDbContext db, string? code, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(code)) return (null, null);
        var option = await db.MasterDataOptions.AsNoTracking()
            .Where(o => o.Category == Category && o.Code == code)
            .Select(o => new { o.Name, o.LabelKey })
            .FirstOrDefaultAsync(ct);
        return (option?.Name, option?.LabelKey);
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
