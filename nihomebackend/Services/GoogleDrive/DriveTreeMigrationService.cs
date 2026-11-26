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

public interface IDriveTreeMigrationService
{
    Task<DriveTreeMigrationPreview> PreviewAsync(CancellationToken ct);
}

/// <summary>Moves existing project Drive folders onto the PRD v2 tree; the preview is read-only.</summary>
public sealed class DriveTreeMigrationService(
    AppDbContext db,
    IGoogleDriveAdapter drive,
    IGoogleDriveSettingsStore settingsStore) : IDriveTreeMigrationService
{
    private const string FolderMimeType = "application/vnd.google-apps.folder";

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
        var misplaced = await liveDocuments.AnyAsync(d => !db.ProjectDriveFolders.Any(f =>
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
            count += (await drive.ListChildrenAsync(folderId, ct))
                .Count(item => item.MimeType != FolderMimeType && !item.IsTrashed && !known.Contains(item.Id));
        return count;
    }
}
