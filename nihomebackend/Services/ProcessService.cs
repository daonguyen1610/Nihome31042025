using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class ProcessService
{
    private readonly AppDbContext db;
    private readonly ProcessAssetStorageService? assetStorage;

    public ProcessService(AppDbContext db, ProcessAssetStorageService? assetStorage = null)
    {
        this.db = db;
        this.assetStorage = assetStorage;
    }

    private ILogger<ProcessService> Logger => db.GetService<ILoggerFactory>().CreateLogger<ProcessService>();

    public async Task<Dictionary<string, List<ProcessResponse>>> GetAllGroupedAsync()
    {
        var all = await db.ProcessDocuments
            .AsNoTracking()
            .Include(p => p.Assets)
            .OrderBy(p => p.GroupKey)
            .ThenBy(p => p.SortOrder)
            .ToListAsync();

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

        ApplyAssetInputs(entity, req.Images, ProcessAssetType.Image);
        ApplyAssetInputs(entity, req.Files, ProcessAssetType.File);

        db.ProcessDocuments.Add(entity);
        await db.SaveChangesAsync();
        Logger.LogInformation(
            "Created process document {ProcessId} (group={GroupKey}, code={Code})",
            entity.Id,
            entity.GroupKey,
            entity.Code);

        return MapToResponse(entity);
    }

    public async Task<ProcessResponse?> UpdateAsync(int id, UpsertProcessRequest req)
    {
        var entity = await db.ProcessDocuments
            .Include(p => p.Assets)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (entity == null)
        {
            Logger.LogWarning("Cannot update process document. Id {ProcessId} not found", id);
            return null;
        }

        entity.GroupKey = req.GroupKey;
        entity.Code = req.Code;
        entity.Title = req.Title;
        entity.SortOrder = req.SortOrder;

        if (req.Images != null)
        {
            ReplaceAssetInputs(entity, req.Images, ProcessAssetType.Image);
        }

        if (req.Files != null)
        {
            ReplaceAssetInputs(entity, req.Files, ProcessAssetType.File);
        }

        await db.SaveChangesAsync();
        Logger.LogInformation("Updated process document {ProcessId}", id);
        return MapToResponse(entity);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await db.ProcessDocuments
            .Include(p => p.Assets)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (entity == null)
        {
            Logger.LogWarning("Cannot delete process document. Id {ProcessId} not found", id);
            return false;
        }

        var assetUrls = entity.Assets.Select(a => a.Url).ToList();
        db.ProcessDocuments.Remove(entity);
        await db.SaveChangesAsync();

        foreach (var assetUrl in assetUrls)
        {
            assetStorage?.DeleteIfManagedAsset(assetUrl);
        }

        Logger.LogInformation("Deleted process document {ProcessId}", id);
        return true;
    }

    public async Task<ProcessAssetResponse?> AddAssetAsync(
        int processId,
        ProcessAssetType type,
        IFormFile file,
        string? displayName,
        int? sortOrder,
        CancellationToken cancellationToken)
    {
        if (assetStorage == null)
        {
            throw new InvalidOperationException("Process asset storage is not configured.");
        }

        var process = await db.ProcessDocuments
            .Include(p => p.Assets)
            .FirstOrDefaultAsync(p => p.Id == processId, cancellationToken);

        if (process == null)
        {
            return null;
        }

        var stored = await assetStorage.SaveUploadAsync(file, type, cancellationToken);
        var nextSortOrder = sortOrder ?? process.Assets
            .Where(a => a.Type == type)
            .Select(a => a.SortOrder)
            .DefaultIfEmpty(-1)
            .Max() + 1;

        var asset = new ProcessAsset
        {
            ProcessDocumentId = processId,
            Type = type,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? stored.OriginalFileName : displayName.Trim(),
            Url = stored.Url,
            OriginalFileName = stored.OriginalFileName,
            ContentType = stored.ContentType,
            FileSizeBytes = stored.FileSizeBytes,
            SortOrder = nextSortOrder,
        };

        db.ProcessAssets.Add(asset);
        await db.SaveChangesAsync(cancellationToken);
        Logger.LogInformation("Added process asset {AssetId} to process {ProcessId}", asset.Id, processId);
        return MapAsset(asset);
    }

    public async Task<bool> DeleteAssetAsync(int processId, int assetId)
    {
        var asset = await db.ProcessAssets
            .FirstOrDefaultAsync(a => a.Id == assetId && a.ProcessDocumentId == processId);

        if (asset == null)
        {
            return false;
        }

        var assetUrl = asset.Url;
        db.ProcessAssets.Remove(asset);
        await db.SaveChangesAsync();
        assetStorage?.DeleteIfManagedAsset(assetUrl);
        Logger.LogInformation("Deleted process asset {AssetId} from process {ProcessId}", assetId, processId);
        return true;
    }

    public async Task ReplaceGroupDataAsync(
        IReadOnlyCollection<LegacyProcessGroupImport> groups,
        CancellationToken cancellationToken)
    {
        var groupKeys = groups.Select(g => g.GroupKey).ToList();
        var existing = await db.ProcessDocuments
            .Include(p => p.Assets)
            .Where(p => groupKeys.Contains(p.GroupKey))
            .ToListAsync(cancellationToken);

        var oldAssetUrls = existing.SelectMany(p => p.Assets).Select(a => a.Url).ToList();
        db.ProcessDocuments.RemoveRange(existing);

        var newProcesses = groups
            .SelectMany(group => group.Processes.Select((process, index) => new ProcessDocument
            {
                GroupKey = group.GroupKey,
                Code = process.Code,
                Title = process.Title,
                SortOrder = index,
                Assets = process.Assets.Select(asset => new ProcessAsset
                {
                    Type = asset.Type,
                    DisplayName = asset.DisplayName,
                    Url = asset.Url,
                    OriginalFileName = asset.OriginalFileName,
                    ContentType = asset.ContentType,
                    FileSizeBytes = asset.FileSizeBytes,
                    SortOrder = asset.SortOrder,
                }).ToList(),
            }))
            .ToList();

        db.ProcessDocuments.AddRange(newProcesses);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var oldAssetUrl in oldAssetUrls)
        {
            assetStorage?.DeleteIfManagedAsset(oldAssetUrl);
        }

        Logger.LogInformation("Replaced legacy process data for {GroupCount} groups", groups.Count);
    }

    private void ReplaceAssetInputs(
        ProcessDocument entity,
        IReadOnlyCollection<UpsertProcessAssetRequest> assetInputs,
        ProcessAssetType type)
    {
        var oldAssets = entity.Assets.Where(a => a.Type == type).ToList();
        foreach (var oldAsset in oldAssets)
        {
            entity.Assets.Remove(oldAsset);
            assetStorage?.DeleteIfManagedAsset(oldAsset.Url);
        }

        ApplyAssetInputs(entity, assetInputs, type);
    }

    private void ApplyAssetInputs(
        ProcessDocument entity,
        IReadOnlyCollection<UpsertProcessAssetRequest>? assetInputs,
        ProcessAssetType type)
    {
        if (assetInputs == null)
        {
            return;
        }

        foreach (var input in assetInputs.OrderBy(a => a.SortOrder))
        {
            entity.Assets.Add(new ProcessAsset
            {
                Type = type,
                DisplayName = input.DisplayName.Trim(),
                Url = assetStorage?.NormalizeAssetUrl(input.Url) ?? input.Url,
                OriginalFileName = string.IsNullOrWhiteSpace(input.OriginalFileName)
                    ? input.DisplayName.Trim()
                    : input.OriginalFileName,
                ContentType = input.ContentType,
                FileSizeBytes = input.FileSizeBytes,
                SortOrder = input.SortOrder,
            });
        }
    }

    private static ProcessResponse MapToResponse(ProcessDocument p) => new()
    {
        Id = p.Id,
        GroupKey = p.GroupKey,
        Code = p.Code,
        Title = p.Title,
        SortOrder = p.SortOrder,
        Images = p.Assets
            .Where(a => a.Type == ProcessAssetType.Image)
            .OrderBy(a => a.SortOrder)
            .Select(MapAsset)
            .ToList(),
        Files = p.Assets
            .Where(a => a.Type == ProcessAssetType.File)
            .OrderBy(a => a.SortOrder)
            .Select(MapAsset)
            .ToList(),
    };

    private static ProcessAssetResponse MapAsset(ProcessAsset asset) => new()
    {
        Id = asset.Id,
        Type = asset.Type.ToString().ToLowerInvariant(),
        DisplayName = asset.DisplayName,
        Url = asset.Url,
        OriginalFileName = asset.OriginalFileName,
        ContentType = asset.ContentType,
        FileSizeBytes = asset.FileSizeBytes,
        SortOrder = asset.SortOrder,
    };
}

public record LegacyProcessGroupImport(
    string GroupKey,
    IReadOnlyCollection<LegacyProcessDocumentImport> Processes);

public record LegacyProcessDocumentImport(
    string Title,
    string? Code,
    IReadOnlyCollection<LegacyProcessAssetImport> Assets);

public record LegacyProcessAssetImport(
    ProcessAssetType Type,
    string DisplayName,
    string Url,
    string OriginalFileName,
    string? ContentType,
    long FileSizeBytes,
    int SortOrder);
