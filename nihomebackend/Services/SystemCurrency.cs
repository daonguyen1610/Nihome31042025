using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;

namespace NihomeBackend.Services;

/// <summary>
/// The single currency configured in admin settings. A record captures this
/// value when it is created and keeps it afterwards, so changing the setting
/// never rewrites stored history; callers no longer choose a currency.
/// </summary>
public static class SystemCurrency
{
    public static async Task<string> ResolveAsync(AppDbContext db, CancellationToken ct = default)
    {
        var code = await db.SiteSettings.AsNoTracking()
            .Select(settings => settings.CurrencyCode).FirstOrDefaultAsync(ct);
        return DisplaySettings.NormalizeCurrencyOrDefault(code);
    }

    /// <summary>Active currency plus the VND-per-USD rate configured in admin settings.</summary>
    public static async Task<(string Currency, decimal UsdVndRate)> ResolveDisplayAsync(
        AppDbContext db, CancellationToken ct = default)
    {
        var settings = await db.SiteSettings.AsNoTracking()
            .Select(row => new { row.CurrencyCode, row.UsdVndRate })
            .FirstOrDefaultAsync(ct);
        return (
            DisplaySettings.NormalizeCurrencyOrDefault(settings?.CurrencyCode),
            settings?.UsdVndRate > 0 ? settings.UsdVndRate : SiteSettings.DefaultUsdVndRate);
    }
}
