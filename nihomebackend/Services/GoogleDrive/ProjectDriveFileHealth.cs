using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Models;
using NihomeBackend.Services.Audit;

namespace NihomeBackend.Services.GoogleDrive;

/// <summary>
/// Keeps "Synced" truthful: a Drive file that is gone, trashed or broken gets its own status so people can
/// see it. This check never removes a record and never changes anything on Drive; the operator decides.
/// </summary>
public sealed partial class ProjectDriveSyncProcessor
{
    internal const string DriveProblemKey = "operationalProjects.documents.driveProblem.";
    private const string DriveFileProblemTitle = "Google Drive: có tệp bị mất hoặc hỏng";
    private const string DriveFolderProblemTitle = "Google Drive: thư mục dự án đã bị xoá";
    private const int FileCheckBatchSize = 10;

    // Status changes waiting for their save; side effects only run once they are stored.
    private readonly List<(ProjectDocument Document, string Action)> driveStateChanges = [];

    private enum FileCheckResult { Checked, Skipped, DriveUnavailable }

    private static Expression<Func<ProjectDocument, bool>> IsCheckable(DateTime now) => document =>
        document.DeletedAt == null && document.DesiredOperation == ProjectDocumentDesiredOperation.None &&
        (document.ClaimToken == null || document.ClaimExpiresAt <= now) &&
        (document.SyncStatus == ProjectDocumentSyncStatus.Synced ||
         document.SyncStatus == ProjectDocumentSyncStatus.Missing ||
         document.SyncStatus == ProjectDocumentSyncStatus.Damaged);

    public async Task<int> VerifyDriveFilesAsync(CancellationToken ct = default)
    {
        var options = await settingsStore.GetRuntimeAsync(ct);
        if (!options.Enabled) return 0;
        currentInstanceId = options.InstanceId;
        var now = DateTime.UtcNow;
        var dueBefore = now.AddHours(-options.FileCheckIntervalHours);
        var due = await db.ProjectDocuments.AsNoTracking().Where(IsCheckable(now))
            .Where(document => document.DriveCheckedAt == null || document.DriveCheckedAt < dueBefore)
            .OrderBy(document => document.DriveCheckedAt).ThenBy(document => document.Id)
            .Select(document => document.Id).Take(FileCheckBatchSize).ToListAsync(ct);
        // Without a working connection every file would look lost.
        if (due.Count == 0 || !await HasWritableConnectionAsync(ct)) return 0;

        var checkedCount = 0;
        foreach (var documentId in due)
        {
            var result = await CheckDriveFileAsync(documentId, ct);
            if (result == FileCheckResult.DriveUnavailable) break;
            if (result == FileCheckResult.Checked) checkedCount++;
        }
        return checkedCount;
    }

    public async Task<bool> VerifyDocumentAsync(long documentId, CancellationToken ct = default)
    {
        var options = await settingsStore.GetRuntimeAsync(ct);
        if (!options.Enabled || !await HasWritableConnectionAsync(ct)) return false;
        currentInstanceId = options.InstanceId;
        return await CheckDriveFileAsync(documentId, ct) == FileCheckResult.Checked;
    }

    private async Task<FileCheckResult> CheckDriveFileAsync(long documentId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var document = await db.ProjectDocuments.Where(IsCheckable(now))
            .SingleOrDefaultAsync(item => item.Id == documentId, ct);
        if (document is null) return FileCheckResult.Skipped;
        var fileId = document.DriveFileId;
        DriveItem? remote = null;
        if (!string.IsNullOrWhiteSpace(fileId))
        {
            try
            {
                remote = await drive.GetMetadataAsync(fileId, ct);
            }
            catch (Exception exception) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning(exception, "Drive file check for document {DocumentId} could not be completed", documentId);
                // Either way the status stays: a failed question proves nothing about the file. A temporary
                // failure stops the pass and brings this file back shortly, behind the others, so that one
                // file that always fails cannot starve the rest.
                var temporary = GoogleDriveFailure.Classify(exception) != GoogleDriveFailureKind.Permanent;
                document.DriveCheckedAt = temporary ? await RetrySoonAsync(now, ct) : now;
                await TrySaveCheckAsync(document, ct);
                return temporary ? FileCheckResult.DriveUnavailable : FileCheckResult.Skipped;
            }
        }
        // The user or the sync worker may have changed the document while Drive was answering.
        await db.Entry(document).ReloadAsync(ct);
        if (!IsCheckable(now).Compile()(document) || document.DriveFileId != fileId)
        {
            logger.LogWarning("Drive file check for document {DocumentId} was set aside because the document changed " +
                "meanwhile (status {SyncStatus}); it stays due", documentId, document.SyncStatus);
            return FileCheckResult.Skipped;
        }

        if (string.IsNullOrWhiteSpace(fileId))
        {
            if (HasServerCopy(document)) QueueUpload(document, now);
            else MarkDriveProblem(document, ProjectDocumentSyncStatus.Missing, "neverUploaded");
        }
        else if (remote is null) MarkDriveProblem(document, ProjectDocumentSyncStatus.Missing, "notFound");
        else if (remote.IsTrashed) MarkDriveProblem(document, ProjectDocumentSyncStatus.Missing, "trashed");
        else
        {
            var damage = DamageReason(document, remote, await IsInLinkedFolderAsync(document, ct));
            if (damage is null)
            {
                MarkDriveHealthy(document);
                await ClearDeletedFolderFlagAsync(document, ct);
            }
            else MarkDriveProblem(document, ProjectDocumentSyncStatus.Damaged, damage);
        }
        document.DriveCheckedAt = now;
        if (!await TrySaveCheckAsync(document, ct)) return FileCheckResult.Skipped;
        await CompleteDriveStateChangesAsync(ct);
        if (document.SyncStatus == ProjectDocumentSyncStatus.Missing) await FlagDeletedFolderAsync(document, ct);
        return FileCheckResult.Checked;
    }

    // So the project page offers the repair without waiting for the next upload to hit the folder.
    private async Task FlagDeletedFolderAsync(ProjectDocument document, CancellationToken ct)
    {
        var linked = await db.ProjectDriveFolders.AsNoTracking().FirstOrDefaultAsync(folder =>
            folder.OperationalProjectId == document.OperationalProjectId &&
            folder.DriveFolderId == document.DriveFolderId && folder.MissingSince == null, ct);
        if (linked is null) return;
        try
        {
            if (await drive.GetMetadataAsync(linked.DriveFolderId, ct) is { IsTrashed: false }) return;
        }
        catch (Exception exception) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Drive folder {FolderId} could not be checked", linked.Id);
            return;
        }
        var missingSince = DateTime.UtcNow;
        await db.ProjectDriveFolders.Where(folder => folder.Id == linked.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(folder => folder.MissingSince, missingSince), ct);
    }

    private async Task NotifyAdminsOfMissingFolderAsync(ProjectDocument document, CancellationToken ct)
    {
        try
        {
            var since = DateTime.UtcNow.AddHours(-24);
            var link = $"/admin/operational-projects/{document.OperationalProjectId}";
            if (await db.Notifications.AnyAsync(item => item.Module == "GoogleDrive" &&
                    item.Title == DriveFolderProblemTitle && item.LinkUrl == link && item.CreatedAt > since, ct)) return;
            await notifications.CreateForAdminsAsync("GoogleDrive", DriveFolderProblemTitle,
                $"Tệp \"{document.OriginalFileName}\" đang chờ vì thư mục đích trên Drive đã bị xoá. " +
                "Mở dự án để khôi phục thư mục từ thùng rác Drive hoặc tạo lại thư mục.", link);
        }
        catch (Exception exception) when (!ct.IsCancellationRequested)
        {
            DiscardUnsavedNotifications();
            logger.LogWarning(exception, "Could not notify administrators about a deleted Drive folder");
        }
    }

    private async Task<DateTime> RetrySoonAsync(DateTime now, CancellationToken ct) =>
        now.AddHours(-(await settingsStore.GetRuntimeAsync(ct)).FileCheckIntervalHours).AddMinutes(5);

    // A live file proves its folder is back, e.g. after someone restored it from the trash.
    private Task ClearDeletedFolderFlagAsync(ProjectDocument document, CancellationToken ct) =>
        db.ProjectDriveFolders.Where(folder => folder.OperationalProjectId == document.OperationalProjectId &&
                folder.DriveFolderId == document.DriveFolderId && folder.MissingSince != null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(folder => folder.MissingSince, (DateTime?)null), ct);

    private async Task<bool> TrySaveCheckAsync(ProjectDocument document, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException exception)
        {
            logger.LogWarning(exception, "Drive file check for document {DocumentId} was not stored because " +
                "{Entities} changed at the same time; it stays due", document.Id,
                string.Join(", ", exception.Entries.Select(entry => entry.Metadata.ClrType.Name)));
            driveStateChanges.RemoveAll(change => change.Document == document);
            db.Entry(document).State = EntityState.Detached;
            return false;
        }
    }

    private Task<bool> IsInLinkedFolderAsync(ProjectDocument document, CancellationToken ct) =>
        db.ProjectDriveFolders.AnyAsync(folder => folder.OperationalProjectId == document.OperationalProjectId &&
            folder.DriveFolderId == document.DriveFolderId, ct);

    // In a linked folder an edit made on Drive is two-way sync that reconciliation owns, so only a file
    // outside those folders is compared with the recorded content. An emptied file is broken anywhere.
    private static string? DamageReason(ProjectDocument document, DriveItem remote, bool linked)
    {
        if (IsNativeGoogleFile(remote)) return null;
        if (IsEmptiedOnDrive(document, remote)) return "empty";
        if (linked || string.IsNullOrWhiteSpace(document.Sha256)) return null;
        if (!string.IsNullOrWhiteSpace(remote.Sha256))
            return string.Equals(remote.Sha256, document.Sha256, StringComparison.OrdinalIgnoreCase) ? null : "contentChanged";
        return remote.Size.HasValue && remote.Size != document.Size ? "contentChanged" : null;
    }

    private static bool IsEmptiedOnDrive(ProjectDocument document, DriveItem remote) =>
        !IsNativeGoogleFile(remote) && remote.Size == 0 && document.Size > 0;

    private bool HasServerCopy(ProjectDocument document)
    {
        if (string.IsNullOrWhiteSpace(document.LocalPath)) return false;
        try
        {
            using var content = storage.OpenRead(document.OperationalProjectId, document.LocalPath);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
            ProjectDocumentValidationException)
        {
            return false;
        }
    }

    // Marked "Synced" but never uploaded: put it back in the queue instead of claiming it is on Drive.
    private void QueueUpload(ProjectDocument document, DateTime now)
    {
        document.DesiredOperation = ProjectDocumentDesiredOperation.Upsert;
        document.SyncStatus = ProjectDocumentSyncStatus.Pending;
        document.SyncAttemptCount = 0;
        document.NextSyncAttemptAt = now;
        document.SyncError = null;
        document.UpdatedAt = now;
        driveStateChanges.Add((document, "project-document.drive_requeued"));
    }

    private void MarkDriveProblem(ProjectDocument document, ProjectDocumentSyncStatus status, string reason)
    {
        var error = DriveProblemKey + reason;
        if (document.SyncStatus == status && document.SyncError == error) return;
        document.SyncStatus = status;
        document.SyncError = error;
        document.UpdatedAt = DateTime.UtcNow;
        driveStateChanges.Add((document, status == ProjectDocumentSyncStatus.Missing
            ? "project-document.drive_missing"
            : "project-document.drive_damaged"));
    }

    private void MarkDriveHealthy(ProjectDocument document)
    {
        if (document.SyncStatus is not (ProjectDocumentSyncStatus.Missing or ProjectDocumentSyncStatus.Damaged)) return;
        document.SyncStatus = ProjectDocumentSyncStatus.Synced;
        document.SyncError = null;
        document.UpdatedAt = DateTime.UtcNow;
        driveStateChanges.Add((document, "project-document.drive_recovered"));
    }

    /// <summary>Call after the status changes are saved: audit them, update surveys and alert administrators.</summary>
    private async Task CompleteDriveStateChangesAsync(CancellationToken ct)
    {
        var changes = driveStateChanges.ToList();
        driveStateChanges.Clear();
        foreach (var (document, action) in changes)
        {
            audit.Log(new AuditEvent
            {
                Action = action,
                ResourceType = EntityTypes.ProjectDocument,
                ResourceId = document.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Message = $"Project document #{document.Id} is now {document.SyncStatus} after a Google Drive check.",
                TargetSystem = "GoogleDrive",
            });
            await PropagateSurveyHealthAsync(document, ct);
        }
        var problems = changes.Where(change => change.Document.SyncStatus is
            ProjectDocumentSyncStatus.Missing or ProjectDocumentSyncStatus.Damaged).Select(change => change.Document).ToList();
        if (problems.Count > 0) await NotifyAdminsOfDriveProblemsAsync(problems, ct);
    }

    private async Task PropagateSurveyHealthAsync(ProjectDocument document, CancellationToken ct)
    {
        var source = await GetSurveySourceAsync(document.Id, ct);
        if (source is null) return;
        var media = await db.SurveyMedia.FirstOrDefaultAsync(item => item.Id == source.MediaId, ct);
        if (media is null) return;
        media.SyncStatus = document.SyncStatus switch
        {
            ProjectDocumentSyncStatus.Synced => SurveyMediaSyncStatus.Synced,
            ProjectDocumentSyncStatus.Pending => SurveyMediaSyncStatus.Pending,
            _ => SurveyMediaSyncStatus.Failed,
        };
        media.SyncError = document.SyncError;
        media.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await RecalculateSurveyAggregateAsync(media, ct);
    }

    // One alert a day is enough to send someone to the document lists, which show every affected file.
    private async Task NotifyAdminsOfDriveProblemsAsync(IReadOnlyList<ProjectDocument> problems, CancellationToken ct)
    {
        try
        {
            var since = DateTime.UtcNow.AddHours(-24);
            if (await db.Notifications.AnyAsync(item => item.Module == "GoogleDrive" &&
                    item.Title == DriveFileProblemTitle && item.CreatedAt > since, ct)) return;
            var first = problems[0];
            var code = await db.OperationalProjects.Where(item => item.Id == first.OperationalProjectId)
                .Select(item => item.Code).SingleOrDefaultAsync(ct);
            var others = problems.Count > 1 ? $" và {problems.Count - 1} tệp khác" : string.Empty;
            await notifications.CreateForAdminsAsync("GoogleDrive", DriveFileProblemTitle,
                $"{code}: \"{first.OriginalFileName}\"{others} không còn dùng được trên Google Drive. " +
                "Mở Tài liệu dự án để kiểm tra, khôi phục, gửi lại hoặc xoá.",
                $"/admin/operational-projects/{first.OperationalProjectId}");
        }
        catch (Exception exception) when (!ct.IsCancellationRequested)
        {
            DiscardUnsavedNotifications();
            logger.LogWarning(exception, "Could not notify administrators about unusable Google Drive files");
        }
    }
}
