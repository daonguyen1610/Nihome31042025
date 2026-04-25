using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class LogoService(AppDbContext db)
{
    public async Task<LogosGroupedResponse> GetAllGroupedAsync()
    {
        var all = await db.ClientLogos.AsNoTracking().OrderBy(l => l.SortOrder).ToListAsync();
        return new LogosGroupedResponse
        {
            Clients = all.Where(l => l.Kind == LogoKind.Client).Select(MapToResponse).ToArray(),
            Partners = all.Where(l => l.Kind == LogoKind.Partner).Select(MapToResponse).ToArray(),
            Suppliers = all.Where(l => l.Kind == LogoKind.Supplier).Select(MapToResponse).ToArray(),
        };
    }

    public async Task<LogoResponse> CreateAsync(UpsertLogoRequest req)
    {
        var entity = new ClientLogo
        {
            Name = req.Name,
            ImageUrl = req.ImageUrl,
            Href = req.Href,
            Kind = Enum.Parse<LogoKind>(req.Kind, ignoreCase: true),
            SortOrder = req.SortOrder,
        };
        db.ClientLogos.Add(entity);
        await db.SaveChangesAsync();
        return MapToResponse(entity);
    }

    public async Task<LogoResponse?> UpdateAsync(int id, UpsertLogoRequest req)
    {
        var entity = await db.ClientLogos.FindAsync(id);
        if (entity == null) return null;

        entity.Name = req.Name;
        entity.ImageUrl = req.ImageUrl;
        entity.Href = req.Href;
        entity.Kind = Enum.Parse<LogoKind>(req.Kind, ignoreCase: true);
        entity.SortOrder = req.SortOrder;

        await db.SaveChangesAsync();
        return MapToResponse(entity);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await db.ClientLogos.FindAsync(id);
        if (entity == null) return false;
        db.ClientLogos.Remove(entity);
        await db.SaveChangesAsync();
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
