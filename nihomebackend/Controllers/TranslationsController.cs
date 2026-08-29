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
    private sealed record EntityTranslationDefinition(
        string DisplayKey,
        IReadOnlyDictionary<string, string> Fields);

    private static readonly IReadOnlyDictionary<string, EntityTranslationDefinition> EntityDefinitions =
        new Dictionary<string, EntityTranslationDefinition>
        {
            [EntityTypes.Activity] = new("translations.entityType.activity", Fields(("Title", "text"), ("Excerpt", "text"), ("Content", "content"))),
            [EntityTypes.News] = new("translations.entityType.news", Fields(("Title", "text"), ("Excerpt", "text"), ("Content", "content"))),
            [EntityTypes.Project] = new("translations.entityType.project", Fields(("Name", "text"), ("Description", "text"), ("Content", "content"), ("Challenges", "stringArray"), ("Solutions", "stringArray"), ("Highlights", "json"))),
            [EntityTypes.Service] = new("translations.entityType.service", Fields(("Title", "text"), ("ShortTitle", "text"), ("Tagline", "text"), ("Intro", "text"), ("Highlights", "stringArray"), ("Sections", "sections"), ("IntroBlocks", "stringArray"))),
            [EntityTypes.Slideshow] = new("translations.entityType.slideshow", Fields(("Title", "text"), ("Subtitle", "text"), ("LinkText", "text"))),
            [EntityTypes.JobPosition] = new("translations.entityType.jobPosition", Fields(("Title", "text"), ("Department", "text"), ("Description", "text"), ("Requirements", "stringArray"))),
            [EntityTypes.About] = new("translations.entityType.about", Fields(("Eyebrow", "text"), ("TitleA", "text"), ("TitleB", "text"), ("Paragraph1", "text"), ("Paragraph2", "text"), ("ItemsJson", "json"))),
            [EntityTypes.ActivityCategory] = new("translations.entityType.activityCategory", Fields(("Name", "text"))),
            [EntityTypes.NewsCategory] = new("translations.entityType.newsCategory", Fields(("Name", "text"))),
            [EntityTypes.ProjectCategory] = new("translations.entityType.projectCategory", Fields(("Name", "text"))),
            [EntityTypes.AsBuiltDocumentCategory] = new("translations.entityType.asBuiltCategory", Fields(("Name", "text"))),
        };

    private static Dictionary<string, string> Fields(params (string Name, string Format)[] fields) =>
        fields.ToDictionary(field => field.Name, field => field.Format);

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
        var types = EntityDefinitions.Select(definition => new
        {
            type = definition.Key,
            displayKey = definition.Value.DisplayKey,
            fields = definition.Value.Fields.Keys,
            fieldFormats = definition.Value.Fields,
        });
        return Ok(types);
    }

    /// <summary>Get all entities of a type with their translation status.</summary>
    [HttpGet("entity/{entityType}")]
    [RequirePermission("content.translations", "view")]
    public async Task<IActionResult> GetEntitiesWithTranslationStatus(string entityType)
    {
        if (!EntityDefinitions.TryGetValue(entityType, out var definition))
            return BadRequest(new { message = $"Unknown entity type: {entityType}" });

        var supportedLanguages = new[] { "en", "zh", "ja" };
        var explicitlySavedCategoryLanguages = (await db.EntityTranslations
            .AsNoTracking()
            .Where(t => t.EntityType == entityType
                && supportedLanguages.Contains(t.LanguageCode)
                && t.FieldName == "Name")
            .Select(t => new { t.EntityId, t.LanguageCode })
            .ToListAsync())
            .Select(t => (t.EntityId, t.LanguageCode))
            .ToHashSet();
        var translationCounts = await db.EntityTranslations
            .AsNoTracking()
            .Where(t => t.EntityType == entityType
                && supportedLanguages.Contains(t.LanguageCode)
                && definition.Fields.Keys.Contains(t.FieldName)
                && t.Value != "")
            .GroupBy(t => t.EntityId)
            .Select(g => new { EntityId = g.Key, Count = g.Select(t => new { t.LanguageCode, t.FieldName }).Distinct().Count() })
            .ToDictionaryAsync(x => x.EntityId, x => x.Count);
        var expectedFields = definition.Fields.Count * supportedLanguages.Length;

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
                    expectedFields
                }),
            EntityTypes.News => (await db.NewsArticles.AsNoTracking().OrderByDescending(n => n.CreatedAt).ToListAsync())
                .Select(n => new
                {
                    id = n.Id,
                    title = n.Title,
                    description = n.Excerpt,
                    hasTranslation = translationCounts.ContainsKey(n.Id),
                    translationCount = translationCounts.GetValueOrDefault(n.Id, 0),
                    expectedFields
                }),
            EntityTypes.Project => (await db.Projects.AsNoTracking().OrderByDescending(p => p.CreatedAt).ToListAsync())
                .Select(p => new
                {
                    id = p.Id,
                    title = p.Name,
                    description = p.Description ?? "",
                    hasTranslation = translationCounts.ContainsKey(p.Id),
                    translationCount = translationCounts.GetValueOrDefault(p.Id, 0),
                    expectedFields
                }),
            EntityTypes.Service => (await db.ServiceItems.AsNoTracking().OrderBy(s => s.SortOrder).ToListAsync())
                .Select(s => new
                {
                    id = s.Id,
                    title = s.Title,
                    description = s.Tagline,
                    hasTranslation = translationCounts.ContainsKey(s.Id),
                    translationCount = translationCounts.GetValueOrDefault(s.Id, 0),
                    expectedFields
                }),
            EntityTypes.Slideshow => (await db.SlideshowItems.AsNoTracking().OrderBy(s => s.SortOrder).ToListAsync())
                .Select(s => new
                {
                    id = s.Id,
                    title = s.Title,
                    description = s.Subtitle ?? "",
                    hasTranslation = translationCounts.GetValueOrDefault(s.Id, 0) > 0,
                    translationCount = translationCounts.GetValueOrDefault(s.Id, 0),
                    expectedFields
                }),
            EntityTypes.JobPosition => (await db.JobPositions.AsNoTracking().OrderBy(j => j.SortOrder).ThenBy(j => j.Title).ToListAsync())
                .Select(j => new
                {
                    id = j.Id,
                    title = j.Title,
                    description = j.Department,
                    hasTranslation = translationCounts.ContainsKey(j.Id),
                    translationCount = translationCounts.GetValueOrDefault(j.Id, 0),
                    expectedFields
                }),
            EntityTypes.About => (await db.AboutSectionContents.AsNoTracking().OrderBy(a => a.SortOrder).ThenBy(a => a.Id).ToListAsync())
                .Select(a => new
                {
                    id = a.Id,
                    title = a.Slug,
                    description = a.Eyebrow,
                    hasTranslation = translationCounts.ContainsKey(a.Id),
                    translationCount = translationCounts.GetValueOrDefault(a.Id, 0),
                    expectedFields
                }),
            EntityTypes.ActivityCategory => (await db.ActivityCategories.AsNoTracking().OrderBy(c => c.SortOrder).ToListAsync())
                .Select(c => new
                {
                    id = c.Id,
                    title = GetCategorySource(c.NameVi, c.Name),
                    description = "",
                    hasTranslation = CountCategoryTranslations(GetCategorySource(c.NameVi, c.Name), c.Id, explicitlySavedCategoryLanguages, c.NameEn, c.NameZh, c.NameJa) > 0,
                    translationCount = CountCategoryTranslations(GetCategorySource(c.NameVi, c.Name), c.Id, explicitlySavedCategoryLanguages, c.NameEn, c.NameZh, c.NameJa),
                    expectedFields = 3
                }),
            EntityTypes.NewsCategory => (await db.NewsCategories.AsNoTracking().OrderBy(c => c.SortOrder).ToListAsync())
                .Select(c => new
                {
                    id = c.Id,
                    title = GetCategorySource(c.NameVi, c.Name),
                    description = "",
                    hasTranslation = CountCategoryTranslations(GetCategorySource(c.NameVi, c.Name), c.Id, explicitlySavedCategoryLanguages, c.NameEn, c.NameZh, c.NameJa) > 0,
                    translationCount = CountCategoryTranslations(GetCategorySource(c.NameVi, c.Name), c.Id, explicitlySavedCategoryLanguages, c.NameEn, c.NameZh, c.NameJa),
                    expectedFields = 3
                }),
            EntityTypes.ProjectCategory => (await db.ProjectCategories.AsNoTracking().OrderBy(c => c.SortOrder).ToListAsync())
                .Select(c => new
                {
                    id = c.Id,
                    title = GetCategorySource(c.NameVi, c.Name),
                    description = "",
                    hasTranslation = CountCategoryTranslations(GetCategorySource(c.NameVi, c.Name), c.Id, explicitlySavedCategoryLanguages, c.NameEn, c.NameZh, c.NameJa) > 0,
                    translationCount = CountCategoryTranslations(GetCategorySource(c.NameVi, c.Name), c.Id, explicitlySavedCategoryLanguages, c.NameEn, c.NameZh, c.NameJa),
                    expectedFields = 3
                }),
            EntityTypes.AsBuiltDocumentCategory => (await db.AsBuiltDocumentCategories.AsNoTracking().OrderBy(c => c.SortOrder).ToListAsync())
                .Select(c => new
                {
                    id = c.Id,
                    title = c.NameVi,
                    description = c.Code,
                    hasTranslation = CountCategoryTranslations(c.NameVi, c.Id, explicitlySavedCategoryLanguages, c.NameEn, c.NameZh, c.NameJa) > 0,
                    translationCount = CountCategoryTranslations(c.NameVi, c.Id, explicitlySavedCategoryLanguages, c.NameEn, c.NameZh, c.NameJa),
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
        var explicitlySavedCategoryLanguages = (await db.EntityTranslations
            .AsNoTracking()
            .Where(t => t.EntityType == entityType && t.EntityId == entityId && t.FieldName == "Name")
            .Select(t => t.LanguageCode)
            .ToListAsync())
            .ToHashSet();

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
                if (proj != null) original = new() { ["Name"] = proj.Name, ["Description"] = proj.Description ?? "", ["Content"] = proj.ContentJson ?? "", ["Challenges"] = proj.ChallengesJson ?? "", ["Solutions"] = proj.SolutionsJson ?? "", ["Highlights"] = proj.HighlightsJson ?? "[]" };
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
            case EntityTypes.Slideshow:
                var slideshow = await db.SlideshowItems.AsNoTracking().FirstOrDefaultAsync(s => s.Id == entityId);
                if (slideshow != null) original = new()
                {
                    ["Title"] = slideshow.Title,
                    ["Subtitle"] = slideshow.Subtitle ?? "",
                    ["LinkText"] = slideshow.LinkText ?? "",
                };
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
                    var activityCategorySource = GetCategorySource(actCat.NameVi, actCat.Name);
                    original = new() { ["Name"] = activityCategorySource };
                    categoryTranslations = new()
                    {
                        ["en"] = new() { ["Name"] = GetActualTranslation(activityCategorySource, actCat.NameEn, explicitlySavedCategoryLanguages.Contains("en")) },
                        ["zh"] = new() { ["Name"] = GetActualTranslation(activityCategorySource, actCat.NameZh, explicitlySavedCategoryLanguages.Contains("zh")) },
                        ["ja"] = new() { ["Name"] = GetActualTranslation(activityCategorySource, actCat.NameJa, explicitlySavedCategoryLanguages.Contains("ja")) },
                    };
                }
                break;
            case EntityTypes.NewsCategory:
                var newsCat = await db.NewsCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == entityId);
                if (newsCat != null)
                {
                    var newsCategorySource = GetCategorySource(newsCat.NameVi, newsCat.Name);
                    original = new() { ["Name"] = newsCategorySource };
                    categoryTranslations = new()
                    {
                        ["en"] = new() { ["Name"] = GetActualTranslation(newsCategorySource, newsCat.NameEn, explicitlySavedCategoryLanguages.Contains("en")) },
                        ["zh"] = new() { ["Name"] = GetActualTranslation(newsCategorySource, newsCat.NameZh, explicitlySavedCategoryLanguages.Contains("zh")) },
                        ["ja"] = new() { ["Name"] = GetActualTranslation(newsCategorySource, newsCat.NameJa, explicitlySavedCategoryLanguages.Contains("ja")) },
                    };
                }
                break;
            case EntityTypes.ProjectCategory:
                var projCat = await db.ProjectCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == entityId);
                if (projCat != null)
                {
                    var projectCategorySource = GetCategorySource(projCat.NameVi, projCat.Name);
                    original = new() { ["Name"] = projectCategorySource };
                    categoryTranslations = new()
                    {
                        ["en"] = new() { ["Name"] = GetActualTranslation(projectCategorySource, projCat.NameEn, explicitlySavedCategoryLanguages.Contains("en")) },
                        ["zh"] = new() { ["Name"] = GetActualTranslation(projectCategorySource, projCat.NameZh, explicitlySavedCategoryLanguages.Contains("zh")) },
                        ["ja"] = new() { ["Name"] = GetActualTranslation(projectCategorySource, projCat.NameJa, explicitlySavedCategoryLanguages.Contains("ja")) },
                    };
                }
                break;
            case EntityTypes.AsBuiltDocumentCategory:
                var asBuiltCat = await db.AsBuiltDocumentCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == entityId);
                if (asBuiltCat != null)
                {
                    original = new() { ["Name"] = asBuiltCat.NameVi };
                    categoryTranslations = new()
                    {
                        ["en"] = new() { ["Name"] = GetActualTranslation(asBuiltCat.NameVi, asBuiltCat.NameEn, explicitlySavedCategoryLanguages.Contains("en")) },
                        ["zh"] = new() { ["Name"] = GetActualTranslation(asBuiltCat.NameVi, asBuiltCat.NameZh, explicitlySavedCategoryLanguages.Contains("zh")) },
                        ["ja"] = new() { ["Name"] = GetActualTranslation(asBuiltCat.NameVi, asBuiltCat.NameJa, explicitlySavedCategoryLanguages.Contains("ja")) },
                    };
                }
                break;
        }

        if (!EntityDefinitions.ContainsKey(entityType))
            return BadRequest(new { message = $"Unknown entity type: {entityType}" });
        if (original == null)
            return NotFound();

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
        if (!EntityDefinitions.TryGetValue(entityType, out var definition))
            return BadRequest(new { message = $"Unknown entity type: {entityType}" });
        if (req.LanguageCode is not ("en" or "zh" or "ja"))
            return BadRequest(new { message = "Ngôn ngữ bản dịch phải là en, zh hoặc ja." });
        if (!await EntityExistsAsync(entityType, entityId))
            return NotFound();
        if (req.Translations.Count == 0)
            return BadRequest(new { message = "Vui lòng nhập ít nhất một trường bản dịch." });

        foreach (var (field, rawValue) in req.Translations)
        {
            if (!definition.Fields.TryGetValue(field, out var format))
                return BadRequest(new { message = $"Trường bản dịch không hợp lệ: {field}." });
            if (string.IsNullOrWhiteSpace(rawValue))
                return BadRequest(new { message = $"Bản dịch cho trường {field} không được để trống." });
            if (format != "text" && !await IsValidStructuredValueAsync(entityType, entityId, field, rawValue, format))
                return BadRequest(new { message = $"Bản dịch cho trường {field} không đúng cấu trúc {format}." });
        }

        if (entityType is EntityTypes.ActivityCategory or EntityTypes.NewsCategory
            or EntityTypes.ProjectCategory or EntityTypes.AsBuiltDocumentCategory)
        {
            var value = req.Translations["Name"].Trim();
            if (value.Length > 200)
                return BadRequest(new { message = "Tên bản dịch không được vượt quá 200 ký tự." });
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
                case EntityTypes.AsBuiltDocumentCategory:
                    var asBuiltCat = await db.AsBuiltDocumentCategories.FindAsync(entityId);
                    if (asBuiltCat == null) return NotFound();
                    SetCategoryLanguageField(asBuiltCat, req.LanguageCode, value);
                    asBuiltCat.UpdatedAt = DateTime.UtcNow;
                    break;
            }
            await db.SaveChangesAsync();
            await entitySvc.SetTranslationsAsync(
                entityType,
                entityId,
                req.LanguageCode,
                new Dictionary<string, string> { ["Name"] = value });
            return Ok();
        }

        var normalized = req.Translations.ToDictionary(pair => pair.Key, pair => pair.Value.Trim());
        await entitySvc.SetTranslationsAsync(entityType, entityId, req.LanguageCode, normalized);
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

    private static void SetCategoryLanguageField(AsBuiltDocumentCategory category, string lang, string value)
    {
        switch (lang)
        {
            case "en": category.NameEn = value; break;
            case "zh": category.NameZh = value; break;
            case "ja": category.NameJa = value; break;
        }
    }

    /// <summary>Delete all translations for an entity.</summary>
    [HttpDelete("entity/{entityType}/{entityId:int}")]
    [RequirePermission("content.translations", "manage")]
    public async Task<IActionResult> DeleteEntityTranslations(string entityType, int entityId)
    {
        if (!EntityDefinitions.ContainsKey(entityType))
            return BadRequest(new { message = $"Unknown entity type: {entityType}" });
        if (!await EntityExistsAsync(entityType, entityId))
            return NotFound();

        switch (entityType)
        {
            case EntityTypes.ActivityCategory:
                var actCat = await db.ActivityCategories.FindAsync(entityId);
                if (actCat == null) return NotFound();
                var activitySource = GetCategorySource(actCat.NameVi, actCat.Name);
                actCat.NameEn = activitySource; actCat.NameZh = activitySource; actCat.NameJa = activitySource;
                actCat.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
                await entitySvc.DeleteEntityTranslationsAsync(entityType, entityId);
                return NoContent();
            case EntityTypes.NewsCategory:
                var newsCat = await db.NewsCategories.FindAsync(entityId);
                if (newsCat == null) return NotFound();
                var newsSource = GetCategorySource(newsCat.NameVi, newsCat.Name);
                newsCat.NameEn = newsSource; newsCat.NameZh = newsSource; newsCat.NameJa = newsSource;
                newsCat.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
                await entitySvc.DeleteEntityTranslationsAsync(entityType, entityId);
                return NoContent();
            case EntityTypes.ProjectCategory:
                var projCat = await db.ProjectCategories.FindAsync(entityId);
                if (projCat == null) return NotFound();
                var projectSource = GetCategorySource(projCat.NameVi, projCat.Name);
                projCat.NameEn = projectSource; projCat.NameZh = projectSource; projCat.NameJa = projectSource;
                projCat.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
                await entitySvc.DeleteEntityTranslationsAsync(entityType, entityId);
                return NoContent();
            case EntityTypes.AsBuiltDocumentCategory:
                var asBuiltCat = await db.AsBuiltDocumentCategories.FindAsync(entityId);
                if (asBuiltCat == null) return NotFound();
                asBuiltCat.NameEn = asBuiltCat.NameVi;
                asBuiltCat.NameZh = asBuiltCat.NameVi;
                asBuiltCat.NameJa = asBuiltCat.NameVi;
                asBuiltCat.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
                await entitySvc.DeleteEntityTranslationsAsync(entityType, entityId);
                return NoContent();
        }

        await entitySvc.DeleteEntityTranslationsAsync(entityType, entityId);
        return NoContent();
    }

    private async Task<bool> EntityExistsAsync(string entityType, int entityId) => entityType switch
    {
        EntityTypes.Activity => await db.Activities.AsNoTracking().AnyAsync(item => item.Id == entityId),
        EntityTypes.News => await db.NewsArticles.AsNoTracking().AnyAsync(item => item.Id == entityId),
        EntityTypes.Project => await db.Projects.AsNoTracking().AnyAsync(item => item.Id == entityId),
        EntityTypes.Service => await db.ServiceItems.AsNoTracking().AnyAsync(item => item.Id == entityId),
        EntityTypes.Slideshow => await db.SlideshowItems.AsNoTracking().AnyAsync(item => item.Id == entityId),
        EntityTypes.JobPosition => await db.JobPositions.AsNoTracking().AnyAsync(item => item.Id == entityId),
        EntityTypes.About => await db.AboutSectionContents.AsNoTracking().AnyAsync(item => item.Id == entityId),
        EntityTypes.ActivityCategory => await db.ActivityCategories.AsNoTracking().AnyAsync(item => item.Id == entityId),
        EntityTypes.NewsCategory => await db.NewsCategories.AsNoTracking().AnyAsync(item => item.Id == entityId),
        EntityTypes.ProjectCategory => await db.ProjectCategories.AsNoTracking().AnyAsync(item => item.Id == entityId),
        EntityTypes.AsBuiltDocumentCategory => await db.AsBuiltDocumentCategories.AsNoTracking().AnyAsync(item => item.Id == entityId),
        _ => false,
    };

    private async Task<bool> IsValidStructuredValueAsync(
        string entityType,
        int entityId,
        string field,
        string value,
        string format)
    {
        try
        {
            using var translatedDocument = JsonDocument.Parse(value);
            var translated = translatedDocument.RootElement;
            var matchesFormat = format switch
            {
                "stringArray" => translated.ValueKind == JsonValueKind.Array
                    && translated.EnumerateArray().All(item => item.ValueKind == JsonValueKind.String),
                "content" or "sections" => translated.ValueKind == JsonValueKind.Array,
                "json" => translated.ValueKind is JsonValueKind.Array or JsonValueKind.Object,
                _ => false,
            };
            if (!matchesFormat) return false;

            var sourceValue = await GetStructuredSourceAsync(entityType, entityId, field);
            if (string.IsNullOrWhiteSpace(sourceValue)) return false;

            using var sourceDocument = JsonDocument.Parse(sourceValue);
            return HasSameJsonStructure(sourceDocument.RootElement, translated);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task<string?> GetStructuredSourceAsync(string entityType, int entityId, string field) =>
        (entityType, field) switch
        {
            (EntityTypes.Activity, "Content") => await db.Activities.Where(item => item.Id == entityId).Select(item => item.ContentJson).FirstOrDefaultAsync(),
            (EntityTypes.News, "Content") => await db.NewsArticles.Where(item => item.Id == entityId).Select(item => item.ContentJson).FirstOrDefaultAsync(),
            (EntityTypes.Project, "Content") => await db.Projects.Where(item => item.Id == entityId).Select(item => item.ContentJson).FirstOrDefaultAsync(),
            (EntityTypes.Project, "Challenges") => await db.Projects.Where(item => item.Id == entityId).Select(item => item.ChallengesJson).FirstOrDefaultAsync(),
            (EntityTypes.Project, "Solutions") => await db.Projects.Where(item => item.Id == entityId).Select(item => item.SolutionsJson).FirstOrDefaultAsync(),
            (EntityTypes.Project, "Highlights") => await db.Projects.Where(item => item.Id == entityId).Select(item => item.HighlightsJson).FirstOrDefaultAsync(),
            (EntityTypes.Service, "Highlights") => await db.ServiceItems.Where(item => item.Id == entityId).Select(item => item.HighlightsJson).FirstOrDefaultAsync(),
            (EntityTypes.Service, "Sections") => await db.ServiceItems.Where(item => item.Id == entityId).Select(item => item.SectionsJson).FirstOrDefaultAsync(),
            (EntityTypes.Service, "IntroBlocks") => ExtractIntroBlockTexts(await db.ServiceItems.Where(item => item.Id == entityId).Select(item => item.IntroBlocksJson).FirstOrDefaultAsync()),
            (EntityTypes.JobPosition, "Requirements") => await db.JobPositions.Where(item => item.Id == entityId).Select(item => item.RequirementsJson).FirstOrDefaultAsync(),
            (EntityTypes.About, "ItemsJson") => await db.AboutSectionContents.Where(item => item.Id == entityId).Select(item => item.ItemsJson).FirstOrDefaultAsync(),
            _ => null,
        };

    private static bool HasSameJsonStructure(JsonElement source, JsonElement translated)
    {
        if (source.ValueKind != translated.ValueKind) return false;

        if (source.ValueKind == JsonValueKind.Array)
        {
            var sourceItems = source.EnumerateArray().ToArray();
            var translatedItems = translated.EnumerateArray().ToArray();
            return sourceItems.Length == translatedItems.Length
                && sourceItems.Zip(translatedItems).All(pair => HasSameJsonStructure(pair.First, pair.Second));
        }

        if (source.ValueKind == JsonValueKind.Object)
        {
            var sourceProperties = source.EnumerateObject().ToDictionary(property => property.Name, property => property.Value);
            var translatedProperties = translated.EnumerateObject().ToDictionary(property => property.Name, property => property.Value);
            return sourceProperties.Count == translatedProperties.Count
                && sourceProperties.All(property => translatedProperties.TryGetValue(property.Key, out var translatedValue)
                    && HasSameJsonStructure(property.Value, translatedValue));
        }

        return true;
    }

    private static int CountActualTranslations(string source, params string[] translations) =>
        translations.Count(value => !string.IsNullOrWhiteSpace(value)
            && !string.Equals(value, source, StringComparison.Ordinal));

    private static int CountCategoryTranslations(
        string source,
        int entityId,
        HashSet<(int EntityId, string LanguageCode)> explicitlySaved,
        string english,
        string chinese,
        string japanese) =>
        new[] { (LanguageCode: "en", Value: english), (LanguageCode: "zh", Value: chinese), (LanguageCode: "ja", Value: japanese) }
            .Count(item => explicitlySaved.Contains((entityId, item.LanguageCode))
                || (!string.IsNullOrWhiteSpace(item.Value)
                    && !string.Equals(item.Value, source, StringComparison.Ordinal)));

    private static string GetCategorySource(string? nameVi, string name) =>
        string.IsNullOrWhiteSpace(nameVi) ? name : nameVi;

    private static string GetActualTranslation(string source, string translation, bool explicitlySaved = false) =>
        !explicitlySaved && string.Equals(source, translation, StringComparison.Ordinal) ? "" : translation;

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
