using System.Reflection;
using System.Text.Json;
using NihomeBackend.Models;

namespace NihomeBackend.Data;

public static class TranslationSeeder
{
    private static readonly string[] LanguageCodes = ["vi", "en", "zh", "ja"];

    public static void Seed(AppDbContext db)
    {
        var existingRows = db.Translations.ToList()
            .ToDictionary(t => t.Key + "|" + t.LanguageCode);

        var assembly = Assembly.GetExecutingAssembly();
        var seedResources = assembly.GetManifestResourceNames()
            .Where(n => n.Contains(".Data.Seeds.") && n.EndsWith(".json"))
            .OrderBy(n => n)
            .ToList();

        if (seedResources.Count == 0) return;

        var now = DateTime.UtcNow;
        var newTranslations = new List<Translation>();
        var hasUpdates = false;

        foreach (var resourceName in seedResources)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName)!;
            using var doc = JsonDocument.Parse(stream);
            if (doc.RootElement.ValueKind != JsonValueKind.Array ||
                !doc.RootElement.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.Object &&
                    e.TryGetProperty("key", out _)))
            {
                continue;
            }

            foreach (var entry in doc.RootElement.EnumerateArray())
            {
                if (!entry.TryGetProperty("key", out var keyProp))
                {
                    continue;
                }

                var key = keyProp.GetString()!;
                var category = entry.TryGetProperty("category", out var catProp) ? catProp.GetString() : null;

                foreach (var lang in LanguageCodes)
                {
                    if (!entry.TryGetProperty(lang, out var valProp)) continue;
                    var value = valProp.GetString();
                    if (string.IsNullOrEmpty(value)) continue;

                    var compositeKey = key + "|" + lang;
                    if (existingRows.TryGetValue(compositeKey, out var existing))
                    {
                        if (existing.Value != value || existing.Category != category)
                        {
                            existing.Value = value;
                            existing.Category = category;
                            existing.UpdatedAt = now;
                            hasUpdates = true;
                        }

                        continue;
                    }

                    newTranslations.Add(new Translation
                    {
                        Key = key,
                        LanguageCode = lang,
                        Value = value,
                        Category = category,
                        CreatedAt = now,
                        UpdatedAt = now,
                    });
                }
            }
        }

        if (newTranslations.Count > 0)
        {
            db.Translations.AddRange(newTranslations);
            hasUpdates = true;
        }

        if (hasUpdates)
        {
            db.SaveChanges();
        }
    }
}
