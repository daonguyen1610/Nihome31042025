using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class ServiceItemService(
    AppDbContext db,
    ILogger<ServiceItemService> logger,
    EntityTranslationService translationSvc)
{
    public async Task<List<ServiceResponse>> GetAllAsync(string lang = "vi")
    {
        var items = await db.ServiceItems.AsNoTracking().OrderBy(s => s.SortOrder).ToListAsync();
        logger.LogDebug("Fetched {Count} service items", items.Count);

        var translations = await translationSvc.GetBatchTranslationsAsync(
            EntityTypes.Service, items.Select(x => x.Id), lang);

        return items.Select(item =>
        {
            var t = translations.GetValueOrDefault(item.Id, new Dictionary<string, string>());
            return MapWithTranslations(item, t);
        }).ToList();
    }

    public async Task<ServiceResponse?> GetBySlugAsync(string slug, string lang = "vi")
    {
        var item = await db.ServiceItems.AsNoTracking().FirstOrDefaultAsync(s => s.Slug == slug);
        if (item == null)
        {
            logger.LogWarning("Service item not found by slug {Slug}", slug);
            return null;
        }
        var t = await translationSvc.GetEntityTranslationsAsync(EntityTypes.Service, item.Id, lang);
        return MapWithTranslations(item, t);
    }

    public async Task<ServiceResponse> CreateAsync(UpsertServiceRequest req)
    {
        var entity = new ServiceItem
        {
            Slug = req.Slug,
            Title = req.Title,
            ShortTitle = req.ShortTitle,
            Tagline = req.Tagline,
            Intro = req.Intro,
            SectionsJson = req.Sections.GetRawText(),
            HighlightsJson = JsonSerializer.Serialize(req.Highlights),
            IntroBlocksJson = req.IntroBlocks.ValueKind == JsonValueKind.Array ? req.IntroBlocks.GetRawText() : "[]",
            SortOrder = req.SortOrder,
        };
        db.ServiceItems.Add(entity);
        await db.SaveChangesAsync();
        logger.LogInformation("Created service item {ServiceItemId} (slug={Slug})", entity.Id, entity.Slug);
        return MapWithTranslations(entity, new Dictionary<string, string>());
    }

    public async Task<ServiceResponse?> UpdateAsync(int id, UpsertServiceRequest req)
    {
        var entity = await db.ServiceItems.FindAsync(id);
        if (entity == null)
        {
            logger.LogWarning("Cannot update service item. Id {ServiceItemId} not found", id);
            return null;
        }

        entity.Slug = req.Slug;
        entity.Title = req.Title;
        entity.ShortTitle = req.ShortTitle;
        entity.Tagline = req.Tagline;
        entity.Intro = req.Intro;
        entity.SectionsJson = req.Sections.GetRawText();
        entity.HighlightsJson = JsonSerializer.Serialize(req.Highlights);
        entity.IntroBlocksJson = req.IntroBlocks.ValueKind == JsonValueKind.Array ? req.IntroBlocks.GetRawText() : "[]";
        entity.SortOrder = req.SortOrder;
        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        logger.LogInformation("Updated service item {ServiceItemId} (slug={Slug})", id, entity.Slug);
        return MapWithTranslations(entity, new Dictionary<string, string>());
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await db.ServiceItems.FindAsync(id);
        if (entity == null)
        {
            logger.LogWarning("Cannot delete service item. Id {ServiceItemId} not found", id);
            return false;
        }
        db.ServiceItems.Remove(entity);
        await db.SaveChangesAsync();
        logger.LogInformation("Deleted service item {ServiceItemId}", id);
        return true;
    }

    private static ServiceResponse MapWithTranslations(ServiceItem s, Dictionary<string, string> t) => new()
    {
        Id = s.Id,
        Slug = s.Slug,
        Title = Coalesce(t, "Title", s.Title),
        ShortTitle = Coalesce(t, "ShortTitle", s.ShortTitle),
        Tagline = Coalesce(t, "Tagline", s.Tagline),
        Intro = Coalesce(t, "Intro", s.Intro),
        Sections = ResolveJsonElement(t, "Sections", s.SectionsJson),
        Highlights = ResolveStringArray(t, "Highlights", s.HighlightsJson),
        IntroBlocks = ResolveIntroBlocks(t, s.IntroBlocksJson),
        SortOrder = s.SortOrder,
    };

    private static string Coalesce(Dictionary<string, string> t, string field, string original) =>
        t.TryGetValue(field, out var v) && !string.IsNullOrWhiteSpace(v) ? v : original;

    // Highlights is string[]: apply translation when it parses as a valid JSON string array.
    private static string[] ResolveStringArray(Dictionary<string, string> t, string field, string originalJson)
    {
        if (t.TryGetValue(field, out var translated) && !string.IsNullOrWhiteSpace(translated))
        {
            try
            {
                var arr = JsonSerializer.Deserialize<string[]>(translated);
                if (arr != null) return arr;
            }
            catch (JsonException) { }
        }
        return JsonSerializer.Deserialize<string[]>(
            string.IsNullOrWhiteSpace(originalJson) ? "[]" : originalJson) ?? [];
    }

    // IntroBlocks: translate only the text field of each block, keep original imageUrl.
    private static JsonElement ResolveIntroBlocks(Dictionary<string, string> t, string? introBlocksJson)
    {
        var origJson = string.IsNullOrWhiteSpace(introBlocksJson) ? "[]" : introBlocksJson;
        if (t.TryGetValue("IntroBlocks", out var textsJson) && !string.IsNullOrWhiteSpace(textsJson))
        {
            try
            {
                var texts = JsonSerializer.Deserialize<string[]>(textsJson);
                using var origDoc = JsonDocument.Parse(origJson);
                if (texts != null && origDoc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    var origArr = origDoc.RootElement.EnumerateArray().ToArray();
                    var merged = origArr.Select((orig, idx) =>
                    {
                        var text = idx < texts.Length && !string.IsNullOrWhiteSpace(texts[idx])
                            ? texts[idx]
                            : (orig.TryGetProperty("text", out var tv) ? tv.GetString() ?? "" : "");
                        var imageUrl = orig.TryGetProperty("imageUrl", out var iv) ? iv.GetString() : null;
                        return new Dictionary<string, object?> { ["text"] = text, ["imageUrl"] = imageUrl };
                    }).ToList();
                    return JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(merged));
                }
            }
            catch (JsonException) { }
        }
        return JsonSerializer.Deserialize<JsonElement>(origJson);
    }

    // Sections is a JSON array blob: only apply translation when it parses as a valid JSON array.
    private static JsonElement ResolveJsonElement(
        Dictionary<string, string> t, string field, string originalJson)
    {
        if (t.TryGetValue(field, out var translated) && !string.IsNullOrWhiteSpace(translated))
        {
            try
            {
                using var doc = JsonDocument.Parse(translated);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    return doc.RootElement.Clone();
            }
            catch (JsonException) { }
        }
        var fallback = string.IsNullOrWhiteSpace(originalJson) ? "[]" : originalJson;
        return JsonSerializer.Deserialize<JsonElement>(fallback);
    }
}
