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
            .ToDictionary(
                translation => translation.Key + "|" + translation.LanguageCode,
                StringComparer.OrdinalIgnoreCase);

        var assembly = Assembly.GetExecutingAssembly();
        var legacyDefaults = LoadLegacyDefaults(assembly);
        var seedResources = assembly.GetManifestResourceNames()
            .Where(n => n.Contains(".Data.Seeds.i18n.") && n.EndsWith(".json"))
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

            foreach (var entry in doc.RootElement.EnumerateArray())
            {
                if (!entry.TryGetProperty("key", out var keyProp)) continue;

                var key = keyProp.GetString();
                if (string.IsNullOrWhiteSpace(key)) continue;

                var category = entry.TryGetProperty("category", out var catProp) ? catProp.GetString() : null;

                foreach (var lang in LanguageCodes)
                {
                    if (!entry.TryGetProperty(lang, out var valProp)) continue;
                    var value = valProp.GetString();
                    if (string.IsNullOrEmpty(value)) continue;

                    var compositeKey = key + "|" + lang;
                    if (existingRows.TryGetValue(compositeKey, out var existing))
                    {
                        // Upgrade only untouched default copy. Editors keep their
                        // explicitly customized translation for the same key.
                        if (legacyDefaults.TryGetValue(compositeKey, out var oldValue) &&
                            existing.Value == oldValue && existing.Value != value)
                        {
                            existing.Value = value;
                            existing.UpdatedAt = now;
                            hasUpdates = true;
                        }
                        if (string.IsNullOrWhiteSpace(existing.Category)
                            && !string.IsNullOrWhiteSpace(category))
                        {
                            existing.Category = category;
                            existing.UpdatedAt = now;
                            hasUpdates = true;
                        }

                        continue;
                    }

                    var newRow = new Translation
                    {
                        Key = key,
                        LanguageCode = lang,
                        Value = value,
                        Category = category,
                        CreatedAt = now,
                        UpdatedAt = now,
                    };
                    newTranslations.Add(newRow);
                    // Two seed files can legitimately define the same key (e.g.
                    // profile.json and user-profile.json both have
                    // profilePage.about.eyebrow) — register it immediately so a
                    // later duplicate in this same pass is treated as an update,
                    // not a second insert, which SaveChanges() would otherwise
                    // reject on the unique (Key, LanguageCode) index.
                    existingRows[compositeKey] = newRow;
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

    private static Dictionary<string, string> LoadLegacyDefaults(Assembly assembly)
    {
        var defaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var resources = assembly.GetManifestResourceNames()
            .Where(name => name.Contains(".Data.Seeds.i18n-upgrades.") && name.EndsWith(".json"));
        foreach (var resource in resources)
        {
            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var document = JsonDocument.Parse(stream);
            foreach (var entry in document.RootElement.EnumerateArray())
            {
                var key = entry.GetProperty("key").GetString();
                if (string.IsNullOrWhiteSpace(key)) continue;
                foreach (var language in entry.GetProperty("old").EnumerateObject())
                {
                    var oldValue = language.Value.GetString();
                    if (oldValue is not null)
                        defaults[$"{key}|{language.Name}"] = oldValue;
                }
            }
        }
        return defaults;
    }
}
