using System.Reflection;
using System.Text.Json;
using NihomeBackend.Models;

namespace NihomeBackend.Data;

public static class TranslationSeeder
{
    private static readonly string[] LanguageCodes = ["vi", "en", "zh", "ja"];

    public static void Seed(AppDbContext db)
    {
        var existingKeys = db.Translations
            .Select(t => t.Key + "|" + t.LanguageCode)
            .ToHashSet();

        var assembly = Assembly.GetExecutingAssembly();
        var seedResources = assembly.GetManifestResourceNames()
            .Where(n => n.Contains(".Data.Seeds.") && n.EndsWith(".json"))
            .OrderBy(n => n)
            .ToList();

        if (seedResources.Count == 0) return;

        var now = DateTime.UtcNow;
        var allTranslations = new List<Translation>();

        foreach (var resourceName in seedResources)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName)!;
            using var doc = JsonDocument.Parse(stream);

            foreach (var entry in doc.RootElement.EnumerateArray())
            {
                var key = entry.GetProperty("key").GetString()!;
                var category = entry.TryGetProperty("category", out var catProp) ? catProp.GetString() : null;

                foreach (var lang in LanguageCodes)
                {
                    if (!entry.TryGetProperty(lang, out var valProp)) continue;
                    var value = valProp.GetString();
                    if (string.IsNullOrEmpty(value)) continue;

                    var compositeKey = key + "|" + lang;
                    if (existingKeys.Contains(compositeKey)) continue;

                    allTranslations.Add(new Translation
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

        if (allTranslations.Count > 0)
        {
            db.Translations.AddRange(allTranslations);
            db.SaveChanges();
        }
    }
}
