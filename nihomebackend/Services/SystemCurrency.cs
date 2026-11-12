using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;

namespace NihomeBackend.Services;

/// <summary>
/// The single currency configured in admin settings. Records keep a currency
/// column for historical data, but every new or edited amount uses this value;
/// callers no longer choose a currency.
/// </summary>
public static class SystemCurrency
{
    public static async Task<string> ResolveAsync(AppDbContext db, CancellationToken ct = default)
    {
        var code = await db.SiteSettings.AsNoTracking()
            .Select(settings => settings.CurrencyCode).FirstOrDefaultAsync(ct);
        return DisplaySettings.NormalizeCurrencyOrDefault(code);
    }
}
