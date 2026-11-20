using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;

namespace NihomeBackend.Services;

/// <summary>
/// Labels in the source language (Vietnamese) for server-side messages and logs. The
/// client localizes from the label key and the error code, not from this text.
/// </summary>
public static class SourceLabels
{
    public static async Task<string> VietnameseAsync(AppDbContext db, string? labelKey, string fallback,
        CancellationToken ct) =>
        labelKey is null
            ? fallback
            : await db.Translations.AsNoTracking()
                .Where(row => row.Key == labelKey && row.LanguageCode == "vi")
                .Select(row => row.Value).FirstOrDefaultAsync(ct) ?? fallback;
}
