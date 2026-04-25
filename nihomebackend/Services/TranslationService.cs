using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NihomeBackend.Data;
using NihomeBackend.Models;

namespace NihomeBackend.Services;

public class TranslationService(AppDbContext db, IMemoryCache cache)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);

    /// <summary>Get all static translations for a language (frontend consumption).</summary>
    public async Task<Dictionary<string, string>> GetTranslationMapAsync(string lang)
    {
        var key = $"translations:{lang}";
        if (cache.TryGetValue(key, out Dictionary<string, string>? cached) && cached != null)
            return cached;

        var result = await db.Translations
            .AsNoTracking()
            .Where(t => t.LanguageCode == lang)
            .ToDictionaryAsync(t => t.Key, t => t.Value);

        cache.Set(key, result, CacheTtl);
        return result;
    }

    /// <summary>Get paginated translation pairs for admin (vi + target side-by-side).</summary>
    public async Task<List<TranslationPairDto>> GetPairsAsync(string? category = null, string? search = null)
    {
        var viQuery = db.Translations.AsNoTracking().Where(t => t.LanguageCode == "vi");
        if (!string.IsNullOrEmpty(category))
            viQuery = viQuery.Where(t => t.Category == category);
        if (!string.IsNullOrEmpty(search))
            viQuery = viQuery.Where(t => t.Key.Contains(search) || t.Value.Contains(search));

        var viItems = await viQuery.OrderBy(t => t.Category).ThenBy(t => t.Key).ToListAsync();
        var keys = viItems.Select(t => t.Key).ToList();

        // Load all other languages for these keys
        var others = await db.Translations.AsNoTracking()
            .Where(t => keys.Contains(t.Key) && t.LanguageCode != "vi")
            .ToListAsync();
        var otherLookup = others.ToLookup(t => t.Key);

        return viItems.Select(vi => new TranslationPairDto
        {
            Key = vi.Key,
            Category = vi.Category,
            VietnameseValue = vi.Value,
            Translations = otherLookup[vi.Key]
                .ToDictionary(t => t.LanguageCode, t => t.Value),
            CreatedAt = vi.CreatedAt,
        }).ToList();
    }

    /// <summary>Get list of categories.</summary>
    public async Task<List<string>> GetCategoriesAsync()
    {
        return await db.Translations
            .AsNoTracking()
            .Where(t => t.Category != null)
            .Select(t => t.Category!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();
    }

    /// <summary>Create or update a translation pair (vi + other languages).</summary>
    public async Task UpsertPairAsync(string key, string viValue, Dictionary<string, string>? translations, string? category)
    {
        var now = DateTime.UtcNow;

        // Upsert Vietnamese
        var viRow = await db.Translations.FirstOrDefaultAsync(t => t.Key == key && t.LanguageCode == "vi");
        if (viRow != null) { viRow.Value = viValue; viRow.Category = category; viRow.UpdatedAt = now; }
        else db.Translations.Add(new Translation { Key = key, LanguageCode = "vi", Value = viValue, Category = category, CreatedAt = now, UpdatedAt = now });

        // Upsert other languages
        if (translations != null)
        {
            foreach (var (lang, value) in translations)
            {
                var row = await db.Translations.FirstOrDefaultAsync(t => t.Key == key && t.LanguageCode == lang);
                if (row != null) { row.Value = value; row.UpdatedAt = now; }
                else db.Translations.Add(new Translation { Key = key, LanguageCode = lang, Value = value, Category = category, CreatedAt = now, UpdatedAt = now });
            }
        }

        await db.SaveChangesAsync();
        InvalidateAllCaches();
    }

    /// <summary>Bulk upsert translations.</summary>
    public async Task BulkUpsertAsync(List<BulkTranslationItem> items)
    {
        var now = DateTime.UtcNow;
        var existingKeys = (await db.Translations
            .Select(t => t.Key + "_" + t.LanguageCode)
            .ToListAsync())
            .ToHashSet();

        foreach (var item in items)
        {
            var compositeKey = item.Key + "_" + item.LanguageCode;
            if (existingKeys.Contains(compositeKey))
            {
                var row = await db.Translations.FirstOrDefaultAsync(
                    t => t.Key == item.Key && t.LanguageCode == item.LanguageCode);
                if (row != null) { row.Value = item.Value; row.Category = item.Category; row.UpdatedAt = now; }
            }
            else
            {
                db.Translations.Add(new Translation
                {
                    Key = item.Key, LanguageCode = item.LanguageCode, Value = item.Value,
                    Category = item.Category, CreatedAt = now, UpdatedAt = now,
                });
                existingKeys.Add(compositeKey);
            }
        }

        await db.SaveChangesAsync();
        InvalidateAllCaches();
    }

    /// <summary>Delete a key across all languages.</summary>
    public async Task DeleteKeyAsync(string key)
    {
        var rows = await db.Translations.Where(t => t.Key == key).ToListAsync();
        db.Translations.RemoveRange(rows);
        await db.SaveChangesAsync();
        InvalidateAllCaches();
    }

    private void InvalidateAllCaches()
    {
        foreach (var lang in new[] { "vi", "en", "zh", "ja" })
            cache.Remove($"translations:{lang}");
    }
}

public class TranslationPairDto
{
    public string Key { get; set; } = "";
    public string? Category { get; set; }
    public string VietnameseValue { get; set; } = "";
    public Dictionary<string, string> Translations { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

public class BulkTranslationItem
{
    public string Key { get; set; } = "";
    public string LanguageCode { get; set; } = "";
    public string Value { get; set; } = "";
    public string? Category { get; set; }
}
