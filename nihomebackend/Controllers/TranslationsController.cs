using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

[ApiController]
[Route("api/translations")]
public class TranslationsController(
    TranslationService translationSvc,
    EntityTranslationService entitySvc,
    AppDbContext db) : ControllerBase
{
    // ─── Static UI translations (key-value) ─────────────────────────

    /// <summary>Get all static translations for a language (frontend).</summary>
    [HttpGet("{lang}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetTranslationMap(string lang)
    {
        var map = await translationSvc.GetTranslationMapAsync(lang);
        return Ok(new { languageCode = lang, translations = map });
    }

    /// <summary>Get translation pairs for admin (vi + others side-by-side).</summary>
    [HttpGet("admin")]
    [Authorize(Roles = "ADMIN,SUPER_ADMIN")]
    public async Task<IActionResult> GetPairs([FromQuery] string? category, [FromQuery] string? search)
    {
        var pairs = await translationSvc.GetPairsAsync(category, search);
        return Ok(pairs);
    }

    /// <summary>Get list of translation categories.</summary>
    [HttpGet("categories")]
    [Authorize(Roles = "ADMIN,SUPER_ADMIN")]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await translationSvc.GetCategoriesAsync();
        return Ok(categories);
    }

    /// <summary>Create or update a translation pair.</summary>
    [HttpPost("pair")]
    [Authorize(Roles = "ADMIN,SUPER_ADMIN")]
    public async Task<IActionResult> UpsertPair([FromBody] UpsertTranslationPairRequest req)
    {
        await translationSvc.UpsertPairAsync(req.Key, req.VietnameseValue, req.Translations, req.Category);
        return Ok();
    }

    /// <summary>Bulk create/update translations.</summary>
    [HttpPost("bulk")]
    [Authorize(Roles = "ADMIN,SUPER_ADMIN")]
    public async Task<IActionResult> BulkUpsert([FromBody] List<BulkTranslationItem> items)
    {
        await translationSvc.BulkUpsertAsync(items);
        return Ok();
    }

    /// <summary>Delete a translation key across all languages.</summary>
    [HttpDelete("key/{key}")]
    [Authorize(Roles = "ADMIN,SUPER_ADMIN")]
    public async Task<IActionResult> DeleteKey(string key)
    {
        await translationSvc.DeleteKeyAsync(key);
        return NoContent();
    }

    // ─── Entity translations (dynamic content) ──────────────────────

    /// <summary>List entity types with their translatable fields.</summary>
    [HttpGet("entity/types")]
    [Authorize(Roles = "ADMIN,SUPER_ADMIN")]
    public IActionResult GetEntityTypes()
    {
        var types = new[]
        {
            new { type = EntityTypes.Activity, display = "Activities", fields = new[] { "Title", "Excerpt", "Content" } },
            new { type = EntityTypes.News, display = "News", fields = new[] { "Title", "Excerpt", "Content" } },
            new { type = EntityTypes.Project, display = "Projects", fields = new[] { "Name", "Description", "Challenges", "Solutions" } },
            new { type = EntityTypes.Service, display = "Services", fields = new[] { "Title", "ShortTitle", "Tagline", "Intro", "Sections" } },
        };
        return Ok(types);
    }

    /// <summary>Get all entities of a type with their translation status.</summary>
    [HttpGet("entity/{entityType}")]
    [Authorize(Roles = "ADMIN,SUPER_ADMIN")]
    public async Task<IActionResult> GetEntitiesWithTranslationStatus(string entityType)
    {
        var translationCounts = await db.EntityTranslations
            .AsNoTracking()
            .Where(t => t.EntityType == entityType && t.LanguageCode == "en")
            .GroupBy(t => t.EntityId)
            .Select(g => new { EntityId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.EntityId, x => x.Count);

        object? items = entityType switch
        {
            EntityTypes.Activity => (await db.Activities.AsNoTracking().OrderByDescending(a => a.CreatedAt).ToListAsync())
                .Select(a => new
                {
                    id = a.Id,
                    title = a.Title,
                    description = a.Excerpt,
                    hasTranslation = translationCounts.ContainsKey(a.Id),
                    translationCount = translationCounts.GetValueOrDefault(a.Id, 0),
                    expectedFields = 3
                }),
            EntityTypes.News => (await db.NewsArticles.AsNoTracking().OrderByDescending(n => n.CreatedAt).ToListAsync())
                .Select(n => new
                {
                    id = n.Id,
                    title = n.Title,
                    description = n.Excerpt,
                    hasTranslation = translationCounts.ContainsKey(n.Id),
                    translationCount = translationCounts.GetValueOrDefault(n.Id, 0),
                    expectedFields = 3
                }),
            EntityTypes.Project => (await db.Projects.AsNoTracking().OrderByDescending(p => p.CreatedAt).ToListAsync())
                .Select(p => new
                {
                    id = p.Id,
                    title = p.Name,
                    description = p.Description ?? "",
                    hasTranslation = translationCounts.ContainsKey(p.Id),
                    translationCount = translationCounts.GetValueOrDefault(p.Id, 0),
                    expectedFields = 4
                }),
            EntityTypes.Service => (await db.ServiceItems.AsNoTracking().OrderBy(s => s.SortOrder).ToListAsync())
                .Select(s => new
                {
                    id = s.Id,
                    title = s.Title,
                    description = s.Tagline,
                    hasTranslation = translationCounts.ContainsKey(s.Id),
                    translationCount = translationCounts.GetValueOrDefault(s.Id, 0),
                    expectedFields = 5
                }),
            _ => null
        };

        if (items is null)
            return BadRequest(new { message = $"Unknown entity type: {entityType}" });

        return Ok(new { entityType, items });
    }

    /// <summary>Get all translations for a specific entity (admin edit form).</summary>
    [HttpGet("entity/{entityType}/{entityId:int}")]
    [Authorize(Roles = "ADMIN,SUPER_ADMIN")]
    public async Task<IActionResult> GetEntityTranslations(string entityType, int entityId)
    {
        // Get original entity data
        object? original = entityType switch
        {
            EntityTypes.Activity => await db.Activities.AsNoTracking().Where(a => a.Id == entityId)
                .Select(a => new { a.Title, a.Excerpt, Content = a.ContentJson ?? "" }).FirstOrDefaultAsync(),
            EntityTypes.News => await db.NewsArticles.AsNoTracking().Where(n => n.Id == entityId)
                .Select(n => new { n.Title, n.Excerpt, Content = n.ContentJson ?? "" }).FirstOrDefaultAsync(),
            EntityTypes.Project => await db.Projects.AsNoTracking().Where(p => p.Id == entityId)
                .Select(p => new { Name = p.Name, Description = p.Description ?? "", Challenges = p.ChallengesJson ?? "", Solutions = p.SolutionsJson ?? "" }).FirstOrDefaultAsync(),
            EntityTypes.Service => await db.ServiceItems.AsNoTracking().Where(s => s.Id == entityId)
                .Select(s => new { s.Title, s.ShortTitle, s.Tagline, s.Intro, Sections = s.SectionsJson ?? "" }).FirstOrDefaultAsync(),
            _ => null
        };

        var translations = await entitySvc.GetAllTranslationsForEntityAsync(entityType, entityId);
        return Ok(new { entityType, entityId, original, translations });
    }

    /// <summary>Save translations for an entity in a specific language.</summary>
    [HttpPost("entity/{entityType}/{entityId:int}")]
    [Authorize(Roles = "ADMIN,SUPER_ADMIN")]
    public async Task<IActionResult> SaveEntityTranslations(
        string entityType, int entityId, [FromBody] SaveEntityTranslationsRequest req)
    {
        await entitySvc.SetTranslationsAsync(entityType, entityId, req.LanguageCode, req.Translations);
        return Ok();
    }

    /// <summary>Delete all translations for an entity.</summary>
    [HttpDelete("entity/{entityType}/{entityId:int}")]
    [Authorize(Roles = "ADMIN,SUPER_ADMIN")]
    public async Task<IActionResult> DeleteEntityTranslations(string entityType, int entityId)
    {
        await entitySvc.DeleteEntityTranslationsAsync(entityType, entityId);
        return NoContent();
    }
}

// ─── Request DTOs ────────────────────────────────────────────────

public class UpsertTranslationPairRequest
{
    public string Key { get; set; } = "";
    public string VietnameseValue { get; set; } = "";
    public Dictionary<string, string>? Translations { get; set; }
    public string? Category { get; set; }
}

public class SaveEntityTranslationsRequest
{
    public string LanguageCode { get; set; } = "en";
    public Dictionary<string, string> Translations { get; set; } = new();
}
