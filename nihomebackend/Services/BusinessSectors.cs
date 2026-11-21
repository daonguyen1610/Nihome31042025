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

    /// <summary>The sector's name in master data, also for one switched off; null when unknown.</summary>
    public static Task<string?> NameAsync(AppDbContext db, string? code, CancellationToken ct) =>
        string.IsNullOrEmpty(code)
            ? Task.FromResult<string?>(null)
            : db.MasterDataOptions.AsNoTracking().Where(o => o.Category == Category && o.Code == code)
                .Select(o => o.Name).FirstOrDefaultAsync(ct);

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
