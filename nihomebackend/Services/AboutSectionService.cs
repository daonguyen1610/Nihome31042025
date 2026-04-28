using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class AboutSectionService(AppDbContext db, HostedImageService hostedImageService)
{
    public async Task<List<AboutSectionResponse>> GetAllAsync(bool activeOnly = true)
    {
        var query = db.AboutSectionContents.AsNoTracking();
        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        var items = await query
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToListAsync();

        return items.Select(Map).ToList();
    }

    public async Task<AboutSectionResponse?> GetBySlugAsync(string slug)
    {
        var item = await db.AboutSectionContents.AsNoTracking().FirstOrDefaultAsync(x => x.Slug == slug);
        return item == null ? null : Map(item);
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
        return Map(entity);
    }

    public async Task<AboutSectionResponse?> UpdateAsync(int id, UpsertAboutSectionRequest req)
    {
        var entity = await db.AboutSectionContents.FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null)
        {
            return null;
        }

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
        {
            hostedImageService.DeleteIfManagedUpload(previousImageUrl);
        }

        return Map(entity);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await db.AboutSectionContents.FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null)
        {
            return false;
        }

        var imageUrl = entity.ImageUrl;
        db.AboutSectionContents.Remove(entity);
        await db.SaveChangesAsync();
        hostedImageService.DeleteIfManagedUpload(imageUrl);
        return true;
    }

    private static AboutSectionResponse Map(AboutSectionContent item) => new()
    {
        Id = item.Id,
        Slug = item.Slug,
        ItemsJson = item.ItemsJson,
        Eyebrow = item.Eyebrow,
        TitleA = item.TitleA,
        TitleB = item.TitleB,
        Paragraph1 = item.Paragraph1,
        Paragraph2 = item.Paragraph2,
        ImageUrl = item.ImageUrl,
        IsActive = item.IsActive,
        SortOrder = item.SortOrder,
    };
}
