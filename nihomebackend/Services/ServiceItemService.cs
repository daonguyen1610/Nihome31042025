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
        Highlights = JsonSerializer.Deserialize<string[]>(s.HighlightsJson) ?? [],
        IntroBlocks = JsonSerializer.Deserialize<JsonElement>(s.IntroBlocksJson),
        SortOrder = s.SortOrder,
    };

    private static string Coalesce(Dictionary<string, string> t, string field, string original) =>
        t.TryGetValue(field, out var v) && !string.IsNullOrWhiteSpace(v) ? v : original;

    // Sections is a JSON blob: apply translated string only when it parses as valid JSON.
    private static JsonElement ResolveJsonElement(
        Dictionary<string, string> t, string field, string originalJson)
    {
        if (t.TryGetValue(field, out var translated) && !string.IsNullOrWhiteSpace(translated))
        {
            try
            {
                using var doc = JsonDocument.Parse(translated);
                return doc.RootElement.Clone();
            }
            catch (JsonException) { }
        }
        return JsonSerializer.Deserialize<JsonElement>(originalJson);
    }
}
