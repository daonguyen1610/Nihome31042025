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
    IReadOnlyList<string> Blockers,
    DriveTreeMigrationSummary? LatestMigration);

/// <summary>What the admin page needs after a reload to offer only safe actions.</summary>
public sealed record DriveTreeMigrationSummary(
    int Id,
    DriveTreeMigrationStatus Status,
    bool UndoStarted,
    string? Error);

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
    Task<DriveTreeMigrationResult> UndoAsync(int migrationId, int userId, CancellationToken ct);
}

/// <summary>Moves existing project Drive folders onto the PRD v2 tree; the preview is read-only.</summary>
public sealed class DriveTreeMigrationService(
    AppDbContext db,
    IGoogleDriveAdapter drive,
    IGoogleDriveSettingsStore settingsStore,
    IProjectDriveFolderService folderService) : IDriveTreeMigrationService
{
    private const string DriveFileMissing = "DRIVE_FILE_MISSING";
    private const string DriveFolderMissing = "DRIVE_FOLDER_MISSING";
    private const string DriveParentUnknown = "DRIVE_PARENT_UNKNOWN";

    // shortcut: serialises run/undo in this app instance only; use a database lock before running two backends.
    private static readonly SemaphoreSlim ExecutionGate = new(1, 1);

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
        if (await IsUndoInProgressAsync(ct)) blockers.Add("DRIVE_TREE_UNDO_IN_PROGRESS");

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
        var latest = await db.DriveTreeMigrations.AsNoTracking().OrderByDescending(m => m.Id)
            .Select(m => new DriveTreeMigrationSummary(m.Id, m.Status, m.UndoStartedAt != null, m.Error))
            .FirstOrDefaultAsync(ct);
        return new DriveTreeMigrationPreview(changes, projectCount, documentCount, untracked, blockers, latest);
    }

    /// <summary>HttpClient reports a Drive timeout as a cancellation too; that one is a failure to record.</summary>
    private static bool IsCallerCancellation(Exception ex, CancellationToken ct) =>
        ex is OperationCanceledException && ct.IsCancellationRequested;

    private async Task<bool> IsConnectedAsync(CancellationToken ct)
    {
        try
        {
            var connection = await drive.CheckConnectionAsync(ct);
            return connection.IsFolder && !connection.IsTrashed && connection.CanAddChildren;
        }
        catch (Exception ex) when (!IsCallerCancellation(ex, ct))
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
                .Count(item => !item.IsTrashed && !known.Contains(item.Id));
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

    private async Task<bool> IsUndoInProgressAsync(CancellationToken ct) =>
        await db.DriveTreeMigrations.OrderByDescending(m => m.Id).FirstOrDefaultAsync(ct) is
        { UndoStartedAt: not null, Status: not DriveTreeMigrationStatus.RolledBack };

    public Task<DriveTreeMigrationResult> RunAsync(int userId, CancellationToken ct) =>
        ExecuteExclusivelyAsync(() => ResumeOrStartAsync(userId, ct));

    public Task<DriveTreeMigrationResult> UndoAsync(int migrationId, int userId, CancellationToken ct) =>
        ExecuteExclusivelyAsync(() => RevertAsync(migrationId, userId, ct));

    private static async Task<DriveTreeMigrationResult> ExecuteExclusivelyAsync(
        Func<Task<DriveTreeMigrationResult>> move)
    {
        if (!await ExecutionGate.WaitAsync(0))
            throw new DriveTreeMigrationBlockedException("DRIVE_TREE_MIGRATION_RUNNING");
        try
        {
            return await move();
        }
        finally
        {
            ExecutionGate.Release();
        }
    }

    private async Task<DriveTreeMigrationResult> ResumeOrStartAsync(int userId, CancellationToken ct)
    {
        // A run that stopped (Failed) or was interrupted (Running) is resumed; its old bindings exist only in its items.
        var resume = await db.DriveTreeMigrations.OrderByDescending(m => m.Id).FirstOrDefaultAsync(ct);
        if (resume is not { Status: DriveTreeMigrationStatus.Running or DriveTreeMigrationStatus.Failed }) resume = null;
        // A started undo may already have returned files; only UndoAsync may finish it.
        if (await IsUndoInProgressAsync(ct)) throw new DriveTreeMigrationBlockedException("DRIVE_TREE_UNDO_IN_PROGRESS");
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
        catch (Exception ex) when (!IsCallerCancellation(ex, ct))
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
        // The preview may have taken long; a save since then could have turned sync back on.
        if (settings.Enabled) throw new DriveTreeMigrationBlockedException("DRIVE_SYNC_ENABLED");
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
            var metadata = await drive.GetMetadataAsync(document.DriveFileId!, ct);
            // Undo needs a real folder to return to; without a recorded one, use the file's current parent.
            var from = string.IsNullOrEmpty(document.DriveFolderId)
                ? metadata?.Parents?.FirstOrDefault()
                : document.DriveFolderId;
            item ??= Track(items, new DriveTreeMigrationItem
            {
                MigrationId = migration.Id,
                OperationalProjectId = document.OperationalProjectId,
                Category = document.Category,
                Kind = DriveTreeMigrationItemKind.Document,
                ProjectDocumentId = document.Id,
                DriveFileId = document.DriveFileId,
            });
            if (string.IsNullOrEmpty(item.FromFolderId)) item.FromFolderId = from ?? string.Empty;
            item.ToFolderId = to;
            item.SkipReason = metadata is null ? DriveFileMissing
                : string.IsNullOrEmpty(item.FromFolderId) ? DriveParentUnknown : null;
            await db.SaveChangesAsync(ct);
            if (item.SkipReason is not null) continue;
            await drive.MoveAsync(document.DriveFileId!, to, ct);
            item.MovedAt = DateTime.UtcNow;
            document.DriveFolderId = to;
            await db.SaveChangesAsync(ct);
        }

        // A move that reached Drive but whose MovedAt was not saved; MoveAsync is a no-op once the file is in place.
        foreach (var item in items.Where(i => i.Kind == DriveTreeMigrationItemKind.UntrackedFile
                     && i.MovedAt is null && i.SkipReason is null).ToList())
        {
            if (await drive.GetMetadataAsync(item.DriveFileId!, ct) is null)
                item.SkipReason = DriveFileMissing;
            else
            {
                await drive.MoveAsync(item.DriveFileId!, item.ToFolderId!, ct);
                item.MovedAt = DateTime.UtcNow;
            }
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
            // A subfolder is one item: moving it carries its whole subtree.
            foreach (var child in children.Where(c => !c.IsTrashed && !known.Contains(c.Id)))
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

    private async Task<DriveTreeMigrationResult> RevertAsync(int migrationId, int userId, CancellationToken ct)
    {
        var migration = await db.DriveTreeMigrations.OrderByDescending(m => m.Id).FirstOrDefaultAsync(ct);
        if (migration is not { Status: DriveTreeMigrationStatus.Completed or DriveTreeMigrationStatus.Failed }
            || migration.Id != migrationId)
            throw new DriveTreeMigrationBlockedException("DRIVE_TREE_MIGRATION_NOT_COMPLETED");
        if ((await settingsStore.GetRuntimeAsync(ct)).Enabled)
            throw new DriveTreeMigrationBlockedException("DRIVE_SYNC_ENABLED");
        if (await db.ProjectDocuments.AnyAsync(d => d.SyncStatus == ProjectDocumentSyncStatus.Processing, ct))
            throw new DriveTreeMigrationBlockedException("DRIVE_DOCUMENTS_PROCESSING");
        if (!await IsConnectedAsync(ct)) throw new DriveTreeMigrationBlockedException("DRIVE_NOT_CONNECTED");
        // Saved before the first move so RunAsync cannot resume or overtake a half-undone migration.
        migration.UndoStartedAt ??= DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var items = await db.DriveTreeMigrationItems.Where(i => i.MigrationId == migration.Id)
            .OrderBy(i => i.Id).ToListAsync(ct);
        var folderExists = new Dictionary<string, bool>(StringComparer.Ordinal);
        // A trashed folder counts as gone: a file moved into it would land in the trash.
        async Task<bool> FolderExistsAsync(string folderId)
        {
            if (!folderExists.TryGetValue(folderId, out var exists))
                folderExists[folderId] = exists =
                    await drive.GetMetadataAsync(folderId, ct) is { IsTrashed: false };
            return exists;
        }

        try
        {
            // Each return is saved on its own, so a rerun after a Drive failure skips it.
            // An item without MovedAt may still have reached Drive; it is returned only if it sits in its target.
            foreach (var item in items.Where(i => i.Kind != DriveTreeMigrationItemKind.Binding
                         && i.SkipReason == null && i.RevertedAt == null))
            {
                var metadata = await drive.GetMetadataAsync(item.DriveFileId!, ct);
                if (item.MovedAt is null && metadata?.Parents?.Contains(item.ToFolderId) != true) continue;
                if (metadata is null)
                    item.SkipReason = DriveFileMissing;
                else if (!await FolderExistsAsync(item.FromFolderId))
                    item.SkipReason = DriveFolderMissing;
                else
                    await drive.MoveAsync(item.DriveFileId!, item.FromFolderId, ct);
                item.RevertedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }

            // Files are back; restore paths, bindings and document folders in one transaction.
            var old = JsonSerializer.Deserialize<GoogleDriveFolderOptions>(migration.OldFoldersJson)!;
            var settings = await db.GoogleDriveCredentials.SingleAsync(item => item.Id == 1, ct);
            settings.SurveyMediaFolder = old.SurveyMedia;
            settings.CrmPreDesignFolder = old.CrmPreDesign;
            settings.DesignConceptFolder = old.DesignConcept;
            settings.DesignBasicFolder = old.DesignBasic;
            settings.DesignShopDrawingFolder = old.DesignShopDrawing;
            settings.LegalPermitsFolder = old.LegalPermits;
            settings.ConstructionAcceptanceFolder = old.ConstructionAcceptance;
            settings.ProcurementFolder = old.Procurement;
            settings.FinanceContractsFolder = old.FinanceContracts;
            settings.HandoverFolder = old.Handover;
            settings.UpdatedByUserId = userId;
            settings.UpdatedAt = DateTime.UtcNow;
            db.ProjectDriveFolders.RemoveRange(await db.ProjectDriveFolders.ToListAsync(ct));
            // A project hard-deleted since the run has no folder to bind any more.
            var projectIds = (await db.OperationalProjects.Select(p => p.Id).ToListAsync(ct)).ToHashSet();
            foreach (var binding in items.Where(i => i.Kind == DriveTreeMigrationItemKind.Binding
                         && projectIds.Contains(i.OperationalProjectId)))
            {
                // Binding a folder that is gone would send later uploads nowhere; the next sync creates a new one.
                if (await FolderExistsAsync(binding.FromFolderId))
                    db.ProjectDriveFolders.Add(new ProjectDriveFolder
                    {
                        OperationalProjectId = binding.OperationalProjectId,
                        Category = binding.Category,
                        DriveFolderId = binding.FromFolderId,
                    });
            }
            var returned = items.Where(i => i.Kind == DriveTreeMigrationItemKind.Document
                    && i.RevertedAt != null && i.SkipReason == null)
                .ToDictionary(i => i.ProjectDocumentId!.Value, i => i.FromFolderId);
            var documentIds = returned.Keys.ToList();
            foreach (var document in await db.ProjectDocuments.Where(d => documentIds.Contains(d.Id)).ToListAsync(ct))
                document.DriveFolderId = returned[document.Id];
            migration.Status = DriveTreeMigrationStatus.RolledBack;
            migration.Error = null;
            migration.FinishedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (!IsCallerCancellation(ex, ct))
        {
            db.ChangeTracker.Clear();
            migration = await db.DriveTreeMigrations.SingleAsync(m => m.Id == migrationId, ct);
            migration.Status = DriveTreeMigrationStatus.Failed;
            migration.Error = ex.Message;
            migration.FinishedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        var all = db.DriveTreeMigrationItems.Where(i => i.MigrationId == migration.Id);
        return new DriveTreeMigrationResult(migration.Id, migration.Status,
            await all.CountAsync(i => i.RevertedAt != null && i.SkipReason == null, ct),
            await all.CountAsync(i => i.SkipReason != null, ct),
            migration.Error);
    }

    private DriveTreeMigrationItem Track(List<DriveTreeMigrationItem> items, DriveTreeMigrationItem item)
    {
        db.DriveTreeMigrationItems.Add(item);
        items.Add(item);
        return item;
    }
}
