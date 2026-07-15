using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Authorization;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

[ApiController]
[Authorize]
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
    [RequirePermission("content.translations", "view")]
    public async Task<IActionResult> GetPairs([FromQuery] string? category, [FromQuery] string? search)
    {
        var pairs = await translationSvc.GetPairsAsync(category, search);
        return Ok(pairs);
    }

    /// <summary>Get list of translation categories.</summary>
    [HttpGet("categories")]
    [RequirePermission("content.translations", "view")]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await translationSvc.GetCategoriesAsync();
        return Ok(categories);
    }

    /// <summary>Create or update a translation pair.</summary>
    [HttpPost("pair")]
    [RequirePermission("content.translations", "manage")]
    public async Task<IActionResult> UpsertPair([FromBody] UpsertTranslationPairRequest req)
    {
        await translationSvc.UpsertPairAsync(req.Key, req.VietnameseValue, req.Translations, req.Category);
        return Ok();
    }

    /// <summary>Bulk create/update translations.</summary>
    [HttpPost("bulk")]
    [RequirePermission("content.translations", "manage")]
    public async Task<IActionResult> BulkUpsert([FromBody] List<BulkTranslationItem> items)
    {
        await translationSvc.BulkUpsertAsync(items);
        return Ok();
    }

    /// <summary>Delete a translation key across all languages.</summary>
    [HttpDelete("key/{key}")]
    [RequirePermission("content.translations", "manage")]
    public async Task<IActionResult> DeleteKey(string key)
    {
        await translationSvc.DeleteKeyAsync(key);
        return NoContent();
    }

    // ─── Entity translations (dynamic content) ──────────────────────

    /// <summary>List entity types with their translatable fields.</summary>
    [HttpGet("entity/types")]
    [RequirePermission("content.translations", "view")]
    public IActionResult GetEntityTypes()
    {
        var types = new[]
        {
            new { type = EntityTypes.Activity, display = "Activities", fields = new[] { "Title", "Excerpt", "Content" } },
            new { type = EntityTypes.News, display = "News", fields = new[] { "Title", "Excerpt", "Content" } },
            new { type = EntityTypes.Project, display = "Projects", fields = new[] { "Name", "Description", "Content", "Challenges", "Solutions" } },
            new { type = EntityTypes.Service, display = "Services", fields = new[] { "Title", "ShortTitle", "Tagline", "Intro", "Highlights", "Sections", "IntroBlocks" } },
            new { type = EntityTypes.JobPosition, display = "Job Positions", fields = new[] { "Title", "Department", "Description", "Requirements" } },
            new { type = EntityTypes.About, display = "About Sections", fields = new[] { "Eyebrow", "TitleA", "TitleB", "Paragraph1", "Paragraph2", "ItemsJson" } },
            new { type = EntityTypes.ActivityCategory, display = "Activity Categories", fields = new[] { "Name" } },
            new { type = EntityTypes.NewsCategory, display = "News Categories", fields = new[] { "Name" } },
            new { type = EntityTypes.ProjectCategory, display = "Project Categories", fields = new[] { "Name" } },
        };
        return Ok(types);
    }

    /// <summary>Get all entities of a type with their translation status.</summary>
    [HttpGet("entity/{entityType}")]
    [RequirePermission("content.translations", "view")]
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
                    expectedFields = 5
                }),
            EntityTypes.Service => (await db.ServiceItems.AsNoTracking().OrderBy(s => s.SortOrder).ToListAsync())
                .Select(s => new
                {
                    id = s.Id,
                    title = s.Title,
                    description = s.Tagline,
                    hasTranslation = translationCounts.ContainsKey(s.Id),
                    translationCount = translationCounts.GetValueOrDefault(s.Id, 0),
                    expectedFields = 7
                }),
            EntityTypes.JobPosition => (await db.JobPositions.AsNoTracking().OrderBy(j => j.SortOrder).ThenBy(j => j.Title).ToListAsync())
                .Select(j => new
                {
                    id = j.Id,
                    title = j.Title,
                    description = j.Department,
                    hasTranslation = translationCounts.ContainsKey(j.Id),
                    translationCount = translationCounts.GetValueOrDefault(j.Id, 0),
                    expectedFields = 4
                }),
            EntityTypes.About => (await db.AboutSectionContents.AsNoTracking().OrderBy(a => a.SortOrder).ThenBy(a => a.Id).ToListAsync())
                .Select(a => new
                {
                    id = a.Id,
                    title = a.Slug,
                    description = a.Eyebrow,
                    hasTranslation = translationCounts.ContainsKey(a.Id),
                    translationCount = translationCounts.GetValueOrDefault(a.Id, 0),
                    expectedFields = 6
                }),
            EntityTypes.ActivityCategory => (await db.ActivityCategories.AsNoTracking().OrderBy(c => c.SortOrder).ToListAsync())
                .Select(c => new
                {
                    id = c.Id,
                    title = string.IsNullOrWhiteSpace(c.NameVi) ? c.Name : c.NameVi,
                    description = "",
                    hasTranslation = !string.IsNullOrWhiteSpace(c.NameEn) || !string.IsNullOrWhiteSpace(c.NameZh) || !string.IsNullOrWhiteSpace(c.NameJa),
                    translationCount = new[] { c.NameEn, c.NameZh, c.NameJa }.Count(v => !string.IsNullOrWhiteSpace(v)),
                    expectedFields = 3
                }),
            EntityTypes.NewsCategory => (await db.NewsCategories.AsNoTracking().OrderBy(c => c.SortOrder).ToListAsync())
                .Select(c => new
                {
                    id = c.Id,
                    title = string.IsNullOrWhiteSpace(c.NameVi) ? c.Name : c.NameVi,
                    description = "",
                    hasTranslation = !string.IsNullOrWhiteSpace(c.NameEn) || !string.IsNullOrWhiteSpace(c.NameZh) || !string.IsNullOrWhiteSpace(c.NameJa),
                    translationCount = new[] { c.NameEn, c.NameZh, c.NameJa }.Count(v => !string.IsNullOrWhiteSpace(v)),
                    expectedFields = 3
                }),
            EntityTypes.ProjectCategory => (await db.ProjectCategories.AsNoTracking().OrderBy(c => c.SortOrder).ToListAsync())
                .Select(c => new
                {
                    id = c.Id,
                    title = string.IsNullOrWhiteSpace(c.NameVi) ? c.Name : c.NameVi,
                    description = "",
                    hasTranslation = !string.IsNullOrWhiteSpace(c.NameEn) || !string.IsNullOrWhiteSpace(c.NameZh) || !string.IsNullOrWhiteSpace(c.NameJa),
                    translationCount = new[] { c.NameEn, c.NameZh, c.NameJa }.Count(v => !string.IsNullOrWhiteSpace(v)),
                    expectedFields = 3
                }),
            _ => null
        };

        if (items is null)
            return BadRequest(new { message = $"Unknown entity type: {entityType}" });

        return Ok(new { entityType, items });
    }

    /// <summary>Get all translations for a specific entity (admin edit form).</summary>
    [HttpGet("entity/{entityType}/{entityId:int}")]
    [RequirePermission("content.translations", "view")]
    public async Task<IActionResult> GetEntityTranslations(string entityType, int entityId)
    {
        // Build original as Dictionary so keys stay PascalCase (matching fields array)
        Dictionary<string, string>? original = null;
        // Category types store EN/ZH/JA directly on fixed columns, not in EntityTranslations —
        // populated here instead of via the entitySvc call below.
        Dictionary<string, Dictionary<string, string>>? categoryTranslations = null;

        switch (entityType)
        {
            case EntityTypes.Activity:
                var act = await db.Activities.AsNoTracking().FirstOrDefaultAsync(a => a.Id == entityId);
                if (act != null) original = new() { ["Title"] = act.Title, ["Excerpt"] = act.Excerpt ?? "", ["Content"] = act.ContentJson ?? "" };
                break;
            case EntityTypes.News:
                var news = await db.NewsArticles.AsNoTracking().FirstOrDefaultAsync(n => n.Id == entityId);
                if (news != null) original = new() { ["Title"] = news.Title, ["Excerpt"] = news.Excerpt ?? "", ["Content"] = news.ContentJson ?? "" };
                break;
            case EntityTypes.Project:
                var proj = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == entityId);
                if (proj != null) original = new() { ["Name"] = proj.Name, ["Description"] = proj.Description ?? "", ["Content"] = proj.ContentJson ?? "", ["Challenges"] = proj.ChallengesJson ?? "", ["Solutions"] = proj.SolutionsJson ?? "" };
                break;
            case EntityTypes.Service:
                var svc = await db.ServiceItems.AsNoTracking().FirstOrDefaultAsync(s => s.Id == entityId);
                if (svc != null)
                {
                    var ibTexts = ExtractIntroBlockTexts(svc.IntroBlocksJson);
                    original = new()
                    {
                        ["Title"] = svc.Title,
                        ["ShortTitle"] = svc.ShortTitle ?? "",
                        ["Tagline"] = svc.Tagline ?? "",
                        ["Intro"] = svc.Intro ?? "",
                        ["Highlights"] = svc.HighlightsJson ?? "[]",
                        ["Sections"] = svc.SectionsJson ?? "[]",
                        ["IntroBlocks"] = ibTexts,
                    };
                }
                break;
            case EntityTypes.JobPosition:
                var job = await db.JobPositions.AsNoTracking().FirstOrDefaultAsync(j => j.Id == entityId);
                if (job != null) original = new() { ["Title"] = job.Title, ["Department"] = job.Department, ["Description"] = job.Description ?? "", ["Requirements"] = job.RequirementsJson };
                break;
            case EntityTypes.About:
                var about = await db.AboutSectionContents.AsNoTracking().FirstOrDefaultAsync(a => a.Id == entityId);
                if (about != null) original = new()
                {
                    ["Eyebrow"] = about.Eyebrow,
                    ["TitleA"] = about.TitleA,
                    ["TitleB"] = about.TitleB,
                    ["Paragraph1"] = about.Paragraph1,
                    ["Paragraph2"] = about.Paragraph2,
                    ["ItemsJson"] = about.ItemsJson ?? "",
                };
                break;
            case EntityTypes.ActivityCategory:
                var actCat = await db.ActivityCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == entityId);
                if (actCat != null)
                {
                    original = new() { ["Name"] = string.IsNullOrWhiteSpace(actCat.NameVi) ? actCat.Name : actCat.NameVi };
                    categoryTranslations = new()
                    {
                        ["en"] = new() { ["Name"] = actCat.NameEn ?? "" },
                        ["zh"] = new() { ["Name"] = actCat.NameZh ?? "" },
                        ["ja"] = new() { ["Name"] = actCat.NameJa ?? "" },
                    };
                }
                break;
            case EntityTypes.NewsCategory:
                var newsCat = await db.NewsCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == entityId);
                if (newsCat != null)
                {
                    original = new() { ["Name"] = string.IsNullOrWhiteSpace(newsCat.NameVi) ? newsCat.Name : newsCat.NameVi };
                    categoryTranslations = new()
                    {
                        ["en"] = new() { ["Name"] = newsCat.NameEn ?? "" },
                        ["zh"] = new() { ["Name"] = newsCat.NameZh ?? "" },
                        ["ja"] = new() { ["Name"] = newsCat.NameJa ?? "" },
                    };
                }
                break;
            case EntityTypes.ProjectCategory:
                var projCat = await db.ProjectCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == entityId);
                if (projCat != null)
                {
                    original = new() { ["Name"] = string.IsNullOrWhiteSpace(projCat.NameVi) ? projCat.Name : projCat.NameVi };
                    categoryTranslations = new()
                    {
                        ["en"] = new() { ["Name"] = projCat.NameEn ?? "" },
                        ["zh"] = new() { ["Name"] = projCat.NameZh ?? "" },
                        ["ja"] = new() { ["Name"] = projCat.NameJa ?? "" },
                    };
                }
                break;
        }

        if (categoryTranslations != null)
        {
            return Ok(new { entityType, entityId, original, translations = categoryTranslations });
        }

        var raw = await entitySvc.GetAllTranslationsForEntityAsync(entityType, entityId);
        // Transform flat list into { lang: { field: value } } shape expected by frontend
        var translations = raw
            .GroupBy(t => t.LanguageCode)
            .ToDictionary(
                g => g.Key,
                g => g.ToDictionary(t => t.FieldName, t => t.Value));
        return Ok(new { entityType, entityId, original, translations });
    }

    /// <summary>Save translations for an entity in a specific language.</summary>
    [HttpPost("entity/{entityType}/{entityId:int}")]
    [RequirePermission("content.translations", "manage")]
    public async Task<IActionResult> SaveEntityTranslations(
        string entityType, int entityId, [FromBody] SaveEntityTranslationsRequest req)
    {
        if (entityType is EntityTypes.ActivityCategory or EntityTypes.NewsCategory or EntityTypes.ProjectCategory)
        {
            var value = req.Translations.GetValueOrDefault("Name", "");
            switch (entityType)
            {
                case EntityTypes.ActivityCategory:
                    var actCat = await db.ActivityCategories.FindAsync(entityId);
                    if (actCat == null) return NotFound();
                    SetCategoryLanguageField(actCat, req.LanguageCode, value);
                    actCat.UpdatedAt = DateTime.UtcNow;
                    break;
                case EntityTypes.NewsCategory:
                    var newsCat = await db.NewsCategories.FindAsync(entityId);
                    if (newsCat == null) return NotFound();
                    SetCategoryLanguageField(newsCat, req.LanguageCode, value);
                    newsCat.UpdatedAt = DateTime.UtcNow;
                    break;
                case EntityTypes.ProjectCategory:
                    var projCat = await db.ProjectCategories.FindAsync(entityId);
                    if (projCat == null) return NotFound();
                    SetCategoryLanguageField(projCat, req.LanguageCode, value);
                    projCat.UpdatedAt = DateTime.UtcNow;
                    break;
            }
            await db.SaveChangesAsync();
            return Ok();
        }

        await entitySvc.SetTranslationsAsync(entityType, entityId, req.LanguageCode, req.Translations);
        return Ok();
    }

    private static void SetCategoryLanguageField(ActivityCategory c, string lang, string value)
    {
        switch (lang)
        {
            case "en": c.NameEn = value; break;
            case "zh": c.NameZh = value; break;
            case "ja": c.NameJa = value; break;
        }
    }

    private static void SetCategoryLanguageField(NewsCategory c, string lang, string value)
    {
        switch (lang)
        {
            case "en": c.NameEn = value; break;
            case "zh": c.NameZh = value; break;
            case "ja": c.NameJa = value; break;
        }
    }

    private static void SetCategoryLanguageField(ProjectCategory c, string lang, string value)
    {
        switch (lang)
        {
            case "en": c.NameEn = value; break;
            case "zh": c.NameZh = value; break;
            case "ja": c.NameJa = value; break;
        }
    }

    /// <summary>Delete all translations for an entity.</summary>
    [HttpDelete("entity/{entityType}/{entityId:int}")]
    [RequirePermission("content.translations", "manage")]
    public async Task<IActionResult> DeleteEntityTranslations(string entityType, int entityId)
    {
        switch (entityType)
        {
            case EntityTypes.ActivityCategory:
                var actCat = await db.ActivityCategories.FindAsync(entityId);
                if (actCat == null) return NotFound();
                actCat.NameEn = ""; actCat.NameZh = ""; actCat.NameJa = "";
                actCat.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
                return NoContent();
            case EntityTypes.NewsCategory:
                var newsCat = await db.NewsCategories.FindAsync(entityId);
                if (newsCat == null) return NotFound();
                newsCat.NameEn = ""; newsCat.NameZh = ""; newsCat.NameJa = "";
                newsCat.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
                return NoContent();
            case EntityTypes.ProjectCategory:
                var projCat = await db.ProjectCategories.FindAsync(entityId);
                if (projCat == null) return NotFound();
                projCat.NameEn = ""; projCat.NameZh = ""; projCat.NameJa = "";
                projCat.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
                return NoContent();
        }

        await entitySvc.DeleteEntityTranslationsAsync(entityType, entityId);
        return NoContent();
    }

    // Extract only the text values from IntroBlocksJson as a JSON string array.
    private static string ExtractIntroBlockTexts(string? introBlocksJson)
    {
        if (string.IsNullOrWhiteSpace(introBlocksJson)) return "[]";
        try
        {
            using var doc = JsonDocument.Parse(introBlocksJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return "[]";
            var texts = doc.RootElement.EnumerateArray()
                .Select(b => b.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "")
                .ToArray();
            return JsonSerializer.Serialize(texts);
        }
        catch (JsonException)
        {
            return "[]";
        }
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
