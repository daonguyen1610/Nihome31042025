using System.Net;
using System.Text.Json;
using Google;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;

namespace NihomeBackend.Services.GoogleDrive;

public sealed record CategoryChange(ProjectDocumentCategory Category, string From, string To);

public sealed record DriveTreeMigrationPreview(
    IReadOnlyList<CategoryChange> Changes,
    int ProjectCount,
    int DocumentCount,
    int UntrackedFileCount,
    IReadOnlyList<string> Blockers);

public sealed record DriveTreeMigrationResult(
    int MigrationId,
    DriveTreeMigrationStatus Status,
    int Moved,
    int Skipped,
    string? Error);

public sealed class DriveTreeMigrationBlockedException(string code)
    : InvalidOperationException($"Không thể chuyển cây thư mục Drive: {code}.")
{
    public string Code { get; } = code;
}

public interface IDriveTreeMigrationService
{
    Task<DriveTreeMigrationPreview> PreviewAsync(CancellationToken ct);
    Task<DriveTreeMigrationResult> RunAsync(int userId, CancellationToken ct);
}

/// <summary>Moves existing project Drive folders onto the PRD v2 tree; the preview is read-only.</summary>
public sealed class DriveTreeMigrationService(
    AppDbContext db,
    IGoogleDriveAdapter drive,
    IGoogleDriveSettingsStore settingsStore,
    IProjectDriveFolderService folderService) : IDriveTreeMigrationService
{
    private const string FolderMimeType = "application/vnd.google-apps.folder";
    private const string DriveFileMissing = "DRIVE_FILE_MISSING";
    private const string DriveFolderMissing = "DRIVE_FOLDER_MISSING";

    public async Task<DriveTreeMigrationPreview> PreviewAsync(CancellationToken ct)
    {
        var runtime = await settingsStore.GetRuntimeAsync(ct);
        var current = runtime.Folders;
        var target = new GoogleDriveFolderOptions();
        var changes = Enum.GetValues<ProjectDocumentCategory>()
            .Where(category => category != ProjectDocumentCategory.Unclassified
                && current.For(category) != target.For(category))
            .Select(category => new CategoryChange(category, current.For(category), target.For(category)))
            .ToList();

        var blockers = new List<string>();
        if (runtime.Enabled) blockers.Add("DRIVE_SYNC_ENABLED");
        if (await db.ProjectDocuments.AnyAsync(d => d.SyncStatus == ProjectDocumentSyncStatus.Processing, ct))
            blockers.Add("DRIVE_DOCUMENTS_PROCESSING");
        if (!await IsConnectedAsync(ct)) blockers.Add("DRIVE_NOT_CONNECTED");
        if (await db.DriveTreeMigrations.AnyAsync(m => m.Status == DriveTreeMigrationStatus.Running, ct))
            blockers.Add("DRIVE_TREE_MIGRATION_RUNNING");

        var liveDocuments = db.ProjectDocuments.Where(d => d.DeletedAt == null && d.DriveFileId != null);
        var projectCount = await db.ProjectDriveFolders.Select(f => f.OperationalProjectId).Distinct().CountAsync(ct);
        var documentCount = await liveDocuments.CountAsync(ct);

        // Re-categorised documents (handover) can sit in an old folder even when the saved paths are standard.
        // Unclassified documents have no category folder, so they are never misplaced.
        var misplaced = await liveDocuments.Where(d => d.Category != ProjectDocumentCategory.Unclassified)
            .AnyAsync(d => !db.ProjectDriveFolders.Any(f =>
            f.OperationalProjectId == d.OperationalProjectId && f.Category == d.Category
            && f.DriveFolderId == d.DriveFolderId), ct);
        if (changes.Count == 0 && !misplaced) blockers.Add("DRIVE_TREE_ALREADY_STANDARD");

        var untracked = blockers.Count == 0 ? await CountUntrackedAsync(changes, ct) : 0;
        return new DriveTreeMigrationPreview(changes, projectCount, documentCount, untracked, blockers);
    }

    private async Task<bool> IsConnectedAsync(CancellationToken ct)
    {
        try
        {
            var connection = await drive.CheckConnectionAsync(ct);
            return connection.IsFolder && !connection.IsTrashed && connection.CanAddChildren;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return false;
        }
    }

    private async Task<int> CountUntrackedAsync(IReadOnlyList<CategoryChange> changes, CancellationToken ct)
    {
        var categories = changes.Select(c => c.Category).ToList();
        var bindings = await db.ProjectDriveFolders.AsNoTracking()
            .Where(f => categories.Contains(f.Category)).Select(f => f.DriveFolderId).ToListAsync(ct);
        var known = (await db.ProjectDocuments.Where(d => d.DriveFileId != null)
            .Select(d => d.DriveFileId!).ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);
        var count = 0;
        foreach (var folderId in bindings)
            count += (await ListExistingFolderAsync(folderId, ct) ?? [])
                .Count(item => item.MimeType != FolderMimeType && !item.IsTrashed && !known.Contains(item.Id));
        return count;
    }

    /// <summary>Children of a folder, or null when the folder no longer exists on Drive.</summary>
    private async Task<IReadOnlyList<DriveItem>?> ListExistingFolderAsync(string folderId, CancellationToken ct)
    {
        try
        {
            return await drive.GetMetadataAsync(folderId, ct) is null ? null : await drive.ListChildrenAsync(folderId, ct);
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<DriveTreeMigrationResult> RunAsync(int userId, CancellationToken ct)
    {
        // A run that stopped (Failed) or was interrupted (Running) is resumed; its old bindings exist only in its items.
        var resume = await db.DriveTreeMigrations.OrderByDescending(m => m.Id).FirstOrDefaultAsync(ct);
        if (resume is not { Status: DriveTreeMigrationStatus.Running or DriveTreeMigrationStatus.Failed }) resume = null;
        var preview = await PreviewAsync(ct);
        var blocker = preview.Blockers.FirstOrDefault(code => resume is null
            || code is not ("DRIVE_TREE_MIGRATION_RUNNING" or "DRIVE_TREE_ALREADY_STANDARD"));
        if (blocker is not null) throw new DriveTreeMigrationBlockedException(blocker);

        var migration = resume ?? await StartAsync(userId, ct);
        migration.Status = DriveTreeMigrationStatus.Running;
        migration.Error = null;
        migration.FinishedAt = null;
        await db.SaveChangesAsync(ct);
        try
        {
            await MoveAllAsync(migration, ct);
            migration.Status = DriveTreeMigrationStatus.Completed;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Every finished move is already saved; drop unsaved state and record the failure for a later resume.
            db.ChangeTracker.Clear();
            migration = await db.DriveTreeMigrations.SingleAsync(m => m.Id == migration.Id, ct);
            migration.Status = DriveTreeMigrationStatus.Failed;
            migration.Error = ex.Message;
        }
        migration.FinishedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var items = db.DriveTreeMigrationItems.Where(i => i.MigrationId == migration.Id);
        return new DriveTreeMigrationResult(migration.Id, migration.Status,
            await items.CountAsync(i => i.MovedAt != null, ct),
            await items.CountAsync(i => i.SkipReason != null, ct),
            migration.Error);
    }

    /// <summary>Records the old bindings, saves the PRD v2 paths and drops the bindings in one transaction.</summary>
    private async Task<DriveTreeMigration> StartAsync(int userId, CancellationToken ct)
    {
        var runtime = await settingsStore.GetRuntimeAsync(ct);
        var target = new GoogleDriveFolderOptions();
        var bindings = await db.ProjectDriveFolders.ToListAsync(ct);
        var migration = new DriveTreeMigration
        {
            StartedByUserId = userId,
            OldFoldersJson = JsonSerializer.Serialize(runtime.Folders),
            NewFoldersJson = JsonSerializer.Serialize(target),
            Items = bindings.Select(binding => new DriveTreeMigrationItem
            {
                OperationalProjectId = binding.OperationalProjectId,
                Category = binding.Category,
                Kind = DriveTreeMigrationItemKind.Binding,
                FromFolderId = binding.DriveFolderId,
            }).ToList(),
        };
        db.DriveTreeMigrations.Add(migration);

        // Written directly: GoogleDriveSettingsStore.UpdateAsync would also delete the bindings without a record.
        var settings = await db.GoogleDriveCredentials.SingleAsync(item => item.Id == 1, ct);
        settings.SurveyMediaFolder = target.SurveyMedia;
        settings.CrmPreDesignFolder = target.CrmPreDesign;
        settings.DesignConceptFolder = target.DesignConcept;
        settings.DesignBasicFolder = target.DesignBasic;
        settings.DesignShopDrawingFolder = target.DesignShopDrawing;
        settings.LegalPermitsFolder = target.LegalPermits;
        settings.ConstructionAcceptanceFolder = target.ConstructionAcceptance;
        settings.ProcurementFolder = target.Procurement;
        settings.FinanceContractsFolder = target.FinanceContracts;
        settings.HandoverFolder = target.Handover;
        settings.UpdatedByUserId = userId;
        settings.UpdatedAt = DateTime.UtcNow;
        db.ProjectDriveFolders.RemoveRange(bindings);
        await db.SaveChangesAsync(ct);
        return migration;
    }

    private async Task MoveAllAsync(DriveTreeMigration migration, CancellationToken ct)
    {
        var items = await db.DriveTreeMigrationItems.Where(i => i.MigrationId == migration.Id).ToListAsync(ct);
        var bindingItems = items.Where(i => i.Kind == DriveTreeMigrationItemKind.Binding).ToList();
        var documents = await db.ProjectDocuments
            .Where(d => d.DeletedAt == null && d.DriveFileId != null && d.Category != ProjectDocumentCategory.Unclassified)
            .OrderBy(d => d.Id).ToListAsync(ct);

        var projectIds = bindingItems.Select(i => i.OperationalProjectId)
            .Concat(documents.Select(d => d.OperationalProjectId)).Distinct().Order().ToList();
        foreach (var projectId in projectIds)
        {
            var project = await db.OperationalProjects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, ct);
            if (project is not null)
                await folderService.EnsureTreeAsync(project, migration.StartedByUserId, ct, requireEnabled: false);
        }
        var targets = await db.ProjectDriveFolders.AsNoTracking()
            .ToDictionaryAsync(f => (f.OperationalProjectId, f.Category), f => f.DriveFolderId, ct);
        foreach (var binding in bindingItems)
            binding.ToFolderId = targets.GetValueOrDefault((binding.OperationalProjectId, binding.Category));
        await db.SaveChangesAsync(ct);

        foreach (var document in documents)
        {
            if (!targets.TryGetValue((document.OperationalProjectId, document.Category), out var to)
                || document.DriveFolderId == to) continue;
            var item = items.FirstOrDefault(i =>
                i.Kind == DriveTreeMigrationItemKind.Document && i.ProjectDocumentId == document.Id);
            if (item?.SkipReason is not null) continue;
            item ??= Track(items, new DriveTreeMigrationItem
            {
                MigrationId = migration.Id,
                OperationalProjectId = document.OperationalProjectId,
                Category = document.Category,
                Kind = DriveTreeMigrationItemKind.Document,
                ProjectDocumentId = document.Id,
                DriveFileId = document.DriveFileId,
                FromFolderId = document.DriveFolderId ?? string.Empty,
            });
            item.ToFolderId = to;
            await db.SaveChangesAsync(ct);
            if (await drive.GetMetadataAsync(document.DriveFileId!, ct) is null)
            {
                item.SkipReason = DriveFileMissing;
                await db.SaveChangesAsync(ct);
                continue;
            }
            await drive.MoveAsync(document.DriveFileId!, to, ct);
            item.MovedAt = DateTime.UtcNow;
            document.DriveFolderId = to;
            await db.SaveChangesAsync(ct);
        }

        var known = (await db.ProjectDocuments.Where(d => d.DriveFileId != null)
            .Select(d => d.DriveFileId!).ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);
        foreach (var binding in bindingItems.Where(b => b.SkipReason is null && b.ToFolderId is not null
                     && b.ToFolderId != b.FromFolderId))
        {
            var children = await ListExistingFolderAsync(binding.FromFolderId, ct);
            if (children is null)
            {
                binding.SkipReason = DriveFolderMissing;
                await db.SaveChangesAsync(ct);
                continue;
            }
            // shortcut: only direct files are moved; files in nested subfolders stay put, scan recursively if users nest folders.
            foreach (var child in children.Where(c => c.MimeType != FolderMimeType && !c.IsTrashed && !known.Contains(c.Id)))
            {
                var item = items.FirstOrDefault(i =>
                    i.Kind == DriveTreeMigrationItemKind.UntrackedFile && i.DriveFileId == child.Id)
                    ?? Track(items, new DriveTreeMigrationItem
                    {
                        MigrationId = migration.Id,
                        OperationalProjectId = binding.OperationalProjectId,
                        Category = binding.Category,
                        Kind = DriveTreeMigrationItemKind.UntrackedFile,
                        DriveFileId = child.Id,
                        FromFolderId = binding.FromFolderId,
                        ToFolderId = binding.ToFolderId,
                    });
                await db.SaveChangesAsync(ct);
                await drive.MoveAsync(child.Id, binding.ToFolderId!, ct);
                item.MovedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
        }
    }

    private DriveTreeMigrationItem Track(List<DriveTreeMigrationItem> items, DriveTreeMigrationItem item)
    {
        db.DriveTreeMigrationItems.Add(item);
        items.Add(item);
        return item;
    }
}
