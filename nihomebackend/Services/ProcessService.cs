using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class ProcessService(AppDbContext db)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private ILogger<ProcessService> Logger => db.GetService<ILoggerFactory>().CreateLogger<ProcessService>();

    public async Task<Dictionary<string, List<ProcessResponse>>> GetAllGroupedAsync()
    {
        var all = await db.ProcessDocuments.AsNoTracking().OrderBy(p => p.SortOrder).ToListAsync();
        Logger.LogDebug("Fetched {Count} process documents", all.Count);
        return all.GroupBy(p => p.GroupKey)
            .ToDictionary(g => g.Key, g => g.Select(MapToResponse).ToList());
    }

    public async Task<ProcessResponse> CreateAsync(UpsertProcessRequest req)
    {
        var entity = new ProcessDocument
        {
            GroupKey = req.GroupKey,
            Code = req.Code,
            Title = req.Title,
            SortOrder = req.SortOrder,
        };
        db.ProcessDocuments.Add(entity);
        await db.SaveChangesAsync();
        Logger.LogInformation("Created process document {ProcessId} (group={GroupKey}, code={Code})", entity.Id, entity.GroupKey, entity.Code);
        return MapToResponse(entity);
    }

    public async Task<ProcessResponse?> UpdateAsync(int id, UpsertProcessRequest req)
    {
        var entity = await db.ProcessDocuments.FindAsync(id);
        if (entity == null)
        {
            Logger.LogWarning("Cannot update process document. Id {ProcessId} not found", id);
            return null;
        }

        entity.GroupKey = req.GroupKey;
        entity.Code = req.Code;
        entity.Title = req.Title;
        entity.SortOrder = req.SortOrder;

        await db.SaveChangesAsync();
        Logger.LogInformation("Updated process document {ProcessId}", id);
        return MapToResponse(entity);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await db.ProcessDocuments.FindAsync(id);
        if (entity == null)
        {
            Logger.LogWarning("Cannot delete process document. Id {ProcessId} not found", id);
            return false;
        }
        db.ProcessDocuments.Remove(entity);
        await db.SaveChangesAsync();
        Logger.LogInformation("Deleted process document {ProcessId}", id);
        return true;
    }

    private static ProcessResponse MapToResponse(ProcessDocument p) => new()
    {
        Id = p.Id,
        GroupKey = p.GroupKey,
        Code = p.Code,
        Title = p.Title,
        SortOrder = p.SortOrder,
        Images = p.ImagesJson != null
            ? JsonSerializer.Deserialize<List<ProcessAssetInfo>>(p.ImagesJson, JsonOpts) ?? []
            : [],
        Files = p.FilesJson != null
            ? JsonSerializer.Deserialize<List<ProcessAssetInfo>>(p.FilesJson, JsonOpts) ?? []
            : [],
    };
}
