using System.Reflection;
using System.Text.Json;
using NihomeBackend.Models;

namespace NihomeBackend.Data;

/// <summary>
/// Idempotent bootstrap of the master-data catalogue for GD2 modules.
/// Loads <c>Data/Seeds/master-data/defaults.json</c> at every startup and:
///  1. Inserts (category, code) pairs that are missing.
///  2. Leaves everything else alone — admin edits made through the UI
///     are preserved on reboot. Only <c>LabelKey</c> is filled in when
///     absent because it is derived by convention.
///
/// The label convention is <c>masterData.&lt;category&gt;.&lt;code&gt;.label</c>.
/// The matching translations live in <c>Data/Seeds/i18n/master-data.json</c>
/// and are seeded by <see cref="TranslationSeeder"/>.
/// </summary>
public static class MasterDataSeeder
{
    public static void Seed(AppDbContext db) => Seed(db, typeof(MasterDataSeeder).Assembly);

    /// <summary>Test hook: seed from a specific assembly (isolates from shipped defaults).</summary>
    public static void Seed(AppDbContext db, Assembly assembly)
    {
        // .NET normalises the folder name `master-data` to `master_data`
        // in the embedded-resource identifier, so match on the underscore.
        var resource = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(".master_data.defaults.json", StringComparison.OrdinalIgnoreCase)
                              || n.EndsWith(".master-data.defaults.json", StringComparison.OrdinalIgnoreCase));
        if (resource == null)
        {
            return;
        }

        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var doc = JsonDocument.Parse(stream);

        if (!doc.RootElement.TryGetProperty("categories", out var categoriesEl) ||
            categoriesEl.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var existingByPair = db.MasterDataOptions
            .ToDictionary(o => o.Category + "|" + o.Code, StringComparer.OrdinalIgnoreCase);

        var now = DateTime.UtcNow;
        var toInsert = new List<MasterDataOption>();

        foreach (var categoryEl in categoriesEl.EnumerateArray())
        {
            if (!categoryEl.TryGetProperty("category", out var catProp)) continue;
            var category = (catProp.GetString() ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(category)) continue;

            if (!categoryEl.TryGetProperty("options", out var optionsEl) ||
                optionsEl.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var optEl in optionsEl.EnumerateArray())
            {
                var code = optEl.TryGetProperty("code", out var codeProp)
                    ? (codeProp.GetString() ?? string.Empty).Trim().ToLowerInvariant()
                    : string.Empty;
                var name = optEl.TryGetProperty("name", out var nameProp)
                    ? (nameProp.GetString() ?? string.Empty).Trim()
                    : string.Empty;
                if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(name)) continue;

                var pairKey = category + "|" + code;
                if (existingByPair.ContainsKey(pairKey))
                {
                    continue;
                }

                var sortOrder = optEl.TryGetProperty("sortOrder", out var sortProp) && sortProp.TryGetInt32(out var so)
                    ? so
                    : 0;
                var description = optEl.TryGetProperty("description", out var descProp)
                    ? descProp.GetString()
                    : null;

                toInsert.Add(new MasterDataOption
                {
                    Category = category,
                    Code = code,
                    Name = name,
                    LabelKey = $"masterData.{category}.{code}.label",
                    Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                    IsActive = true,
                    SortOrder = sortOrder,
                    CreatedAt = now,
                });
            }
        }

        if (toInsert.Count == 0)
        {
            return;
        }

        db.MasterDataOptions.AddRange(toInsert);
        db.SaveChanges();
    }
}
