using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class AboutSectionService(
    AppDbContext db,
    HostedImageService hostedImageService,
    EntityTranslationService translationSvc)
{
    public async Task<List<AboutSectionResponse>> GetAllAsync(string lang = "vi", bool activeOnly = true)
    {
        var query = db.AboutSectionContents.AsNoTracking();
        if (activeOnly)
            query = query.Where(x => x.IsActive);

        var items = await query
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToListAsync();

        // Single DB round-trip for all sections in the requested language.
        var translations = await translationSvc.GetBatchTranslationsAsync(
            EntityTypes.About, items.Select(x => x.Id), lang);

        return items.Select(item =>
        {
            var t = translations.GetValueOrDefault(item.Id, new Dictionary<string, string>());
            return MapWithTranslations(item, t);
        }).ToList();
    }

    public async Task<AboutSectionResponse?> GetBySlugAsync(string slug, string lang = "vi")
    {
        // Always look up by the original (Vietnamese) slug — translated slugs are not supported.
        var item = await db.AboutSectionContents.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Slug == slug);
        if (item == null) return null;

        var t = await translationSvc.GetEntityTranslationsAsync(EntityTypes.About, item.Id, lang);
        return MapWithTranslations(item, t);
    }

    public async Task<AboutSectionResponse> CreateAsync(UpsertAboutSectionRequest req)
    {
        var normalizedImageUrl = hostedImageService.NormalizeImageUrl(req.ImageUrl);

        var entity = new AboutSectionContent
        {
            Slug = req.Slug,
            ItemsJson = req.ItemsJson?.Trim(),
            Eyebrow = req.Eyebrow,
            TitleA = req.TitleA,
            TitleB = req.TitleB,
            Paragraph1 = req.Paragraph1,
            Paragraph2 = req.Paragraph2,
            ImageUrl = normalizedImageUrl ?? string.Empty,
            IsActive = req.IsActive,
            SortOrder = req.SortOrder,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        db.AboutSectionContents.Add(entity);
        await db.SaveChangesAsync();
        return MapWithTranslations(entity, new Dictionary<string, string>());
    }

    public async Task<AboutSectionResponse?> UpdateAsync(int id, UpsertAboutSectionRequest req)
    {
        var entity = await db.AboutSectionContents.FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null) return null;

        var previousImageUrl = hostedImageService.NormalizeImageUrl(entity.ImageUrl);
        var nextImageUrl = hostedImageService.NormalizeImageUrl(req.ImageUrl);

        entity.Slug = req.Slug;
        entity.ItemsJson = req.ItemsJson?.Trim();
        entity.Eyebrow = req.Eyebrow;
        entity.TitleA = req.TitleA;
        entity.TitleB = req.TitleB;
        entity.Paragraph1 = req.Paragraph1;
        entity.Paragraph2 = req.Paragraph2;
        entity.ImageUrl = nextImageUrl ?? string.Empty;
        entity.IsActive = req.IsActive;
        entity.SortOrder = req.SortOrder;
        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        if (!string.Equals(previousImageUrl, entity.ImageUrl, StringComparison.OrdinalIgnoreCase))
            hostedImageService.DeleteIfManagedUpload(previousImageUrl);

        return MapWithTranslations(entity, new Dictionary<string, string>());
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await db.AboutSectionContents.FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null) return false;

        var imageUrl = entity.ImageUrl;
        db.AboutSectionContents.Remove(entity);
        await db.SaveChangesAsync();
        hostedImageService.DeleteIfManagedUpload(imageUrl);
        return true;
    }

    private static AboutSectionResponse MapWithTranslations(
        AboutSectionContent item, Dictionary<string, string> t)
    {
        // For each field: use translation when non-empty, fallback to original.
        var eyebrow = Coalesce(t, "Eyebrow", item.Eyebrow);
        var titleA = Coalesce(t, "TitleA", item.TitleA);
        var titleB = Coalesce(t, "TitleB", item.TitleB);
        var paragraph1 = Coalesce(t, "Paragraph1", item.Paragraph1);
        var paragraph2 = Coalesce(t, "Paragraph2", item.Paragraph2);

        // ItemsJson: only apply translation when it's present AND parses as valid JSON.
        var itemsJson = item.ItemsJson;
        if (t.TryGetValue("ItemsJson", out var translatedItems) &&
            !string.IsNullOrWhiteSpace(translatedItems) &&
            IsValidJson(translatedItems))
        {
            itemsJson = translatedItems;
        }

        return new AboutSectionResponse
        {
            Id = item.Id,
            Slug = item.Slug,
            ItemsJson = itemsJson,
            Eyebrow = eyebrow,
            TitleA = titleA,
            TitleB = titleB,
            Paragraph1 = paragraph1,
            Paragraph2 = paragraph2,
            ImageUrl = item.ImageUrl,
            IsActive = item.IsActive,
            SortOrder = item.SortOrder,
        };
    }

    private static string Coalesce(Dictionary<string, string> t, string field, string original)
    {
        return t.TryGetValue(field, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : original;
    }

    private static bool IsValidJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
