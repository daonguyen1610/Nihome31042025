using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;

namespace NihomeBackend.Services;

/// <summary>
/// The one rule for naming a master-data option in a language, used by the master-data list and
/// by every record that shows an option: the translation an admin entered for that language,
/// else the translation of its label key (seeded options), else its name. In Vietnamese, the
/// source language, the name an admin gave it.
/// </summary>
public static class MasterDataNames
{
    public static async Task<Dictionary<int, string>> LocalizeAsync(
        AppDbContext db, IReadOnlyCollection<MasterDataOption> options, string lang, CancellationToken ct)
    {
        var names = options.ToDictionary(option => option.Id, option => option.Name);
        if (lang == "vi" || options.Count == 0) return names;

        var ids = options.Select(option => option.Id).ToList();
        var entered = await db.EntityTranslations.AsNoTracking()
            .Where(row => row.EntityType == EntityTypes.MasterDataOption && ids.Contains(row.EntityId) &&
                row.LanguageCode == lang && row.FieldName == "Name")
            .ToDictionaryAsync(row => row.EntityId, row => row.Value, ct);
        var keys = options.Where(option => !string.IsNullOrEmpty(option.LabelKey))
            .Select(option => option.LabelKey!).Distinct().ToList();
        var seeded = await db.Translations.AsNoTracking()
            .Where(row => keys.Contains(row.Key) && row.LanguageCode == lang)
            .ToDictionaryAsync(row => row.Key, row => row.Value, ct);

        foreach (var option in options)
        {
            if (entered.TryGetValue(option.Id, out var value) && !string.IsNullOrWhiteSpace(value))
                names[option.Id] = value;
            else if (option.LabelKey is not null && seeded.TryGetValue(option.LabelKey, out var label) &&
                !string.IsNullOrWhiteSpace(label))
                names[option.Id] = label;
        }
        return names;
    }
}
