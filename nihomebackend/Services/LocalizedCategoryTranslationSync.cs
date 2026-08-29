using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;

namespace NihomeBackend.Services;

internal static class LocalizedCategoryTranslationSync
{
    public static async Task<HashSet<string>> GetExplicitLanguagesAsync(
        AppDbContext db,
        string entityType,
        int entityId) =>
        (await db.EntityTranslations
            .AsNoTracking()
            .Where(translation => translation.EntityType == entityType
                && translation.EntityId == entityId
                && translation.FieldName == "Name")
            .Select(translation => translation.LanguageCode)
            .ToListAsync())
            .ToHashSet();

    public static string ResolveProvidedOrFallback(string? translation, string sourceName) =>
        string.IsNullOrWhiteSpace(translation) ? sourceName : translation.Trim();

    public static string ResolveUpdate(
        string? requestedTranslation,
        string currentTranslation,
        string previousSourceName,
        string newSourceName,
        bool wasExplicitlySaved = false)
    {
        if (requestedTranslation != null)
        {
            return ResolveProvidedOrFallback(requestedTranslation, newSourceName);
        }

        if (wasExplicitlySaved)
        {
            return currentTranslation;
        }

        return string.IsNullOrWhiteSpace(currentTranslation)
            || string.Equals(currentTranslation, previousSourceName, StringComparison.Ordinal)
                ? newSourceName
                : currentTranslation;
    }
}
