using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class LogoService(AppDbContext db, HostedImageService hostedImageService)
{
    private ILogger<LogoService> Logger => db.GetService<ILoggerFactory>().CreateLogger<LogoService>();

    public async Task<LogosGroupedResponse> GetAllGroupedAsync()
    {
        var all = await db.ClientLogos.AsNoTracking().OrderBy(l => l.SortOrder).ToListAsync();
        Logger.LogDebug("Fetched {Count} logos", all.Count);
        return new LogosGroupedResponse
        {
            Clients = all.Where(l => l.Kind == LogoKind.Client).Select(MapToResponse).ToArray(),
            Partners = all.Where(l => l.Kind == LogoKind.Partner).Select(MapToResponse).ToArray(),
            Suppliers = all.Where(l => l.Kind == LogoKind.Supplier).Select(MapToResponse).ToArray(),
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
        Logger.LogInformation("Created logo {LogoId} ({Name})", entity.Id, entity.Name);
        return MapToResponse(entity);
    }

    public async Task<LogoResponse?> UpdateAsync(int id, UpsertLogoRequest req)
    {
        var entity = await db.ClientLogos.FindAsync(id);
        if (entity == null)
        {
            Logger.LogWarning("Cannot update logo. Id {LogoId} not found", id);
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
            Logger.LogInformation("Updated logo {LogoId} image from {OldImageUrl} to {NewImageUrl}", id, previousImageUrl, entity.ImageUrl);
        }
        Logger.LogInformation("Updated logo {LogoId} ({Name})", id, entity.Name);
        return MapToResponse(entity);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await db.ClientLogos.FindAsync(id);
        if (entity == null)
        {
            Logger.LogWarning("Cannot delete logo. Id {LogoId} not found", id);
            return false;
        }
        var imageUrl = entity.ImageUrl;
        db.ClientLogos.Remove(entity);
        await db.SaveChangesAsync();
        hostedImageService.DeleteIfManagedUpload(imageUrl);
        Logger.LogInformation("Deleted logo {LogoId}", id);
        return true;
    }

    private static LogoResponse MapToResponse(ClientLogo l) => new()
    {
        Id = l.Id,
        Name = l.Name,
        ImageUrl = l.ImageUrl,
        Href = l.Href,
        Kind = l.Kind.ToString(),
    };
}
