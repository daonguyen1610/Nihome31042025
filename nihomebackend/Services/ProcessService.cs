using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class ProcessService(AppDbContext db, IWebHostEnvironment? env = null)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly string _webRoot = ResolveWebRoot(env);

    private static string ResolveWebRoot(IWebHostEnvironment? env)
    {
        if (!string.IsNullOrEmpty(env?.ContentRootPath))
        {
            return Path.Combine(env.ContentRootPath, "wwwroot");
        }
        if (!string.IsNullOrEmpty(env?.WebRootPath))
        {
            return env.WebRootPath;
        }
        return Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
    }

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
            ImagesJson = SerializeAssets(req.Images),
            FilesJson = SerializeAssets(req.Files),
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
        entity.ImagesJson = SerializeAssets(req.Images);
        entity.FilesJson = SerializeAssets(req.Files);

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

    private ProcessResponse MapToResponse(ProcessDocument p) => new()
    {
        Id = p.Id,
        GroupKey = p.GroupKey,
        Code = p.Code,
        Title = p.Title,
        SortOrder = p.SortOrder,
        Images = Hydrate(p.ImagesJson),
        Files = Hydrate(p.FilesJson),
    };

    private List<ProcessAssetInfo> Hydrate(string? json)
    {
        if (string.IsNullOrEmpty(json)) return [];
        var list = JsonSerializer.Deserialize<List<ProcessAssetInfo>>(json, JsonOpts) ?? [];
        foreach (var a in list)
        {
            a.FileSizeBytes = ResolveFileSize(a.Url);
        }
        return list;
    }

    private static string? SerializeAssets(List<ProcessAssetInput>? assets)
    {
        if (assets == null || assets.Count == 0) return null;
        var ordered = assets
            .Where(a => !string.IsNullOrWhiteSpace(a.Url))
            .Select((a, i) => new
            {
                a.DisplayName,
                a.Url,
                a.OriginalFileName,
                a.ContentType,
                SortOrder = a.SortOrder == 0 ? i : a.SortOrder,
            })
            .OrderBy(a => a.SortOrder)
            .ToList();
        return ordered.Count == 0 ? null : JsonSerializer.Serialize(ordered);
    }

    private long ResolveFileSize(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return 0;
        var relative = url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var path = Path.Combine(_webRoot, relative);
        return File.Exists(path) ? new FileInfo(path).Length : 0;
    }
}
