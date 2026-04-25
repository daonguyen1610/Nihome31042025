using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NihomeBackend.Data;
using NihomeBackend.Models;

namespace NihomeBackend.Services;

public class EntityTranslationService(AppDbContext db, IMemoryCache cache)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);

    /// <summary>Get all field translations for a single entity in a given language.</summary>
    public async Task<Dictionary<string, string>> GetEntityTranslationsAsync(
        string entityType, int entityId, string lang)
    {
        if (lang == "vi") return new Dictionary<string, string>();

        var key = $"et:{entityType}:{entityId}:{lang}";
        if (cache.TryGetValue(key, out Dictionary<string, string>? cached) && cached != null)
            return cached;

        var result = await db.EntityTranslations
            .AsNoTracking()
            .Where(t => t.EntityType == entityType && t.EntityId == entityId && t.LanguageCode == lang)
            .ToDictionaryAsync(t => t.FieldName, t => t.Value);

        cache.Set(key, result, CacheTtl);
        return result;
    }

    /// <summary>Batch-load translations for multiple entities (single DB query).</summary>
    public async Task<Dictionary<int, Dictionary<string, string>>> GetBatchTranslationsAsync(
        string entityType, IEnumerable<int> entityIds, string lang)
    {
        if (lang == "vi")
            return entityIds.ToDictionary(id => id, _ => new Dictionary<string, string>());

        var idList = entityIds.ToList();
        var rows = await db.EntityTranslations
            .AsNoTracking()
            .Where(t => t.EntityType == entityType && idList.Contains(t.EntityId) && t.LanguageCode == lang)
            .ToListAsync();

        var result = new Dictionary<int, Dictionary<string, string>>();
        foreach (var id in idList)
            result[id] = new Dictionary<string, string>();

        foreach (var row in rows)
        {
            if (!result.ContainsKey(row.EntityId))
                result[row.EntityId] = new Dictionary<string, string>();
            result[row.EntityId][row.FieldName] = row.Value;
        }

        return result;
    }

    /// <summary>Save translations for a single entity (upsert).</summary>
    public async Task SetTranslationsAsync(
        string entityType, int entityId, string lang, Dictionary<string, string> translations)
    {
        var existing = await db.EntityTranslations
            .Where(t => t.EntityType == entityType && t.EntityId == entityId && t.LanguageCode == lang)
            .ToListAsync();

        var now = DateTime.UtcNow;
        foreach (var (field, value) in translations)
        {
            var row = existing.FirstOrDefault(r => r.FieldName == field);
            if (row != null)
            {
                row.Value = value;
                row.UpdatedAt = now;
            }
            else
            {
                db.EntityTranslations.Add(new EntityTranslation
                {
                    EntityType = entityType,
                    EntityId = entityId,
                    FieldName = field,
                    LanguageCode = lang,
                    Value = value,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
            }
        }

        await db.SaveChangesAsync();
        InvalidateCache(entityType, entityId, lang);
    }

    /// <summary>Delete all translations for an entity (call on entity delete).</summary>
    public async Task DeleteEntityTranslationsAsync(string entityType, int entityId)
    {
        var rows = await db.EntityTranslations
            .Where(t => t.EntityType == entityType && t.EntityId == entityId)
            .ToListAsync();
        db.EntityTranslations.RemoveRange(rows);
        await db.SaveChangesAsync();
    }

    /// <summary>Get all translations for an entity across all languages (for admin).</summary>
    public async Task<List<EntityTranslation>> GetAllTranslationsForEntityAsync(
        string entityType, int entityId)
    {
        return await db.EntityTranslations
            .AsNoTracking()
            .Where(t => t.EntityType == entityType && t.EntityId == entityId)
            .OrderBy(t => t.LanguageCode).ThenBy(t => t.FieldName)
            .ToListAsync();
    }

    private void InvalidateCache(string entityType, int entityId, string lang)
    {
        cache.Remove($"et:{entityType}:{entityId}:{lang}");
    }
}
