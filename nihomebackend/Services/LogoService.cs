using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class LogoService(
    AppDbContext db,
    HostedImageService hostedImageService,
    EntityTranslationService entityTranslations,
    ILogger<LogoService> logger)
{

    public async Task<LogosGroupedResponse> GetAllGroupedAsync(string lang = "vi")
    {
        var all = await db.ClientLogos.AsNoTracking().OrderBy(l => l.SortOrder).ToListAsync();
        logger.LogDebug("Fetched {Count} logos", all.Count);
        var translations = await entityTranslations.GetBatchTranslationsAsync(
            EntityTypes.ClientLogo, all.Select(l => l.Id), lang);
        LogoResponse[] Map(LogoKind kind) => all
            .Where(l => l.Kind == kind)
            .Select(l => MapToResponse(l, translations.GetValueOrDefault(l.Id)))
            .ToArray();
        return new LogosGroupedResponse
        {
            Clients = Map(LogoKind.Client),
            Partners = Map(LogoKind.Partner),
            Suppliers = Map(LogoKind.Supplier),
            Awards = Map(LogoKind.Award),
        };
    }

    public async Task<LogoResponse> CreateAsync(UpsertLogoRequest req)
    {
        var normalizedImageUrl = hostedImageService.NormalizeImageUrl(req.ImageUrl);
        var entity = new ClientLogo
        {
            Name = req.Name,
            ImageUrl = normalizedImageUrl ?? string.Empty,
            Href = req.Href,
            Kind = Enum.Parse<LogoKind>(req.Kind, ignoreCase: true),
            SortOrder = req.SortOrder,
        };
        db.ClientLogos.Add(entity);
        await db.SaveChangesAsync();
        logger.LogInformation("Created logo {LogoId} ({Name})", entity.Id, entity.Name);
        return MapToResponse(entity);
    }

    public async Task<LogoResponse?> UpdateAsync(int id, UpsertLogoRequest req)
    {
        var entity = await db.ClientLogos.FindAsync(id);
        if (entity == null)
        {
            logger.LogWarning("Cannot update logo. Id {LogoId} not found", id);
            return null;
        }

        var previousImageUrl = hostedImageService.NormalizeImageUrl(entity.ImageUrl);
        var nextImageUrl = hostedImageService.NormalizeImageUrl(req.ImageUrl);

        entity.Name = req.Name;
        entity.ImageUrl = nextImageUrl ?? string.Empty;
        entity.Href = req.Href;
        entity.Kind = Enum.Parse<LogoKind>(req.Kind, ignoreCase: true);
        entity.SortOrder = req.SortOrder;

        await db.SaveChangesAsync();
        if (!string.Equals(previousImageUrl, entity.ImageUrl, StringComparison.OrdinalIgnoreCase))
        {
            hostedImageService.DeleteIfManagedUpload(previousImageUrl);
            logger.LogInformation("Updated logo {LogoId} image from {OldImageUrl} to {NewImageUrl}", id, previousImageUrl, entity.ImageUrl);
        }
        logger.LogInformation("Updated logo {LogoId} ({Name})", id, entity.Name);
        return MapToResponse(entity);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await db.ClientLogos.FindAsync(id);
        if (entity == null)
        {
            logger.LogWarning("Cannot delete logo. Id {LogoId} not found", id);
            return false;
        }
        var imageUrl = entity.ImageUrl;
        db.ClientLogos.Remove(entity);
        await db.SaveChangesAsync();
        await entityTranslations.DeleteEntityTranslationsAsync(EntityTypes.ClientLogo, entity.Id);
        hostedImageService.DeleteIfManagedUpload(imageUrl);
        logger.LogInformation("Deleted logo {LogoId}", id);
        return true;
    }

    private static LogoResponse MapToResponse(
        ClientLogo l, Dictionary<string, string>? translations = null) => new()
        {
            Id = l.Id,
            Name = translations != null && translations.TryGetValue("Name", out var name) && !string.IsNullOrWhiteSpace(name)
                ? name
                : l.Name,
            ImageUrl = l.ImageUrl,
            Href = l.Href,
            Kind = l.Kind.ToString(),
            SortOrder = l.SortOrder,
        };
}
