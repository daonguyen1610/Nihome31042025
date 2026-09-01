using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Services.Audit;

namespace NihomeBackend.Services.GoogleDrive;

public interface IProjectDriveSyncProcessor
{
    Task<bool> ProcessNextOutboundAsync(CancellationToken ct = default);
    Task ReconcileProjectAsync(int projectId, CancellationToken ct = default);
    Task ReconcileBoundProjectsAsync(CancellationToken ct = default);
}

public sealed class ProjectDriveSyncProcessor(
    AppDbContext db,
    IProjectDocumentStorageService storage,
    IGoogleDriveAdapter drive,
    IProjectDriveFolderService folderService,
    IGoogleDriveSettingsStore settingsStore,
    IProjectDriveClaimLease claimLease,
    IAuditLogger audit,
    ILogger<ProjectDriveSyncProcessor> logger) : IProjectDriveSyncProcessor
{
    private static readonly TimeSpan ReconciliationLease = TimeSpan.FromMinutes(5);
    private const string NativeMimePrefix = "application/vnd.google-apps.";
    private const string UnsupportedNativeReason = "Tệp Google Workspace gốc chưa hỗ trợ tải xuống; có thể mở bằng liên kết Google Drive.";
    private string currentInstanceId = string.Empty;

    public async Task<bool> ProcessNextOutboundAsync(CancellationToken ct = default)
    {
        var options = await settingsStore.GetRuntimeAsync(ct);
        currentInstanceId = options.InstanceId;
        if (!options.Enabled) return false;
        var now = DateTime.UtcNow;
        var candidates = await db.ProjectDocuments.AsNoTracking().Where(IsDueForClaim(now))
            .OrderBy(document => document.NextSyncAttemptAt ?? document.CreatedAt)
            .ThenBy(document => document.Id)
            .Select(document => document.Id).Take(10).ToListAsync(ct);
        if (candidates.Count == 0 || !await HasWritableConnectionAsync(ct)) return false;
        foreach (var documentId in candidates)
        {
            var token = Guid.NewGuid();
            await using var claimTransaction = db.Database.IsRelational()
                ? await db.Database.BeginTransactionAsync(ct)
                : null;
            var generation = await TryClaimAsync(documentId, token, now, ct);
            if (!generation.HasValue) continue;
            await PropagateSurveyClaimAsync(documentId, now, ct);
            if (claimTransaction is not null) await claimTransaction.CommitAsync(ct);
            await ProcessClaimAsync(documentId, token, generation.Value, ct);
            return true;
        }
        return false;
    }

    private async Task<bool> HasWritableConnectionAsync(CancellationToken ct)
    {
        try
        {
            var connection = await drive.CheckConnectionAsync(ct);
            if (connection.IsFolder && !connection.IsTrashed && connection.CanAddChildren) return true;
            logger.LogWarning(
                "Project Drive synchronization paused because the configured root is not writable");
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception,
                "Project Drive synchronization paused because connection validation failed");
        }
        return false;
    }

    public async Task ReconcileBoundProjectsAsync(CancellationToken ct = default)
    {
        var options = await settingsStore.GetRuntimeAsync(ct);
        currentInstanceId = options.InstanceId;
        if (!options.Enabled) return;
        var projectIds = await db.ProjectDriveFolders.AsNoTracking()
            .Where(folder => !folder.LastReconciledAt.HasValue || folder.LastReconciledAt < DateTime.UtcNow.AddMinutes(-1))
            .Select(folder => folder.OperationalProjectId).Distinct().OrderBy(projectId => projectId).Take(10).ToListAsync(ct);
        foreach (var projectId in projectIds) await ReconcileProjectAsync(projectId, ct);
    }

    public async Task ReconcileProjectAsync(int projectId, CancellationToken ct = default)
    {
        var options = await settingsStore.GetRuntimeAsync(ct);
        currentInstanceId = options.InstanceId;
        if (!options.Enabled) return;
        var folders = await db.ProjectDriveFolders.Where(folder => folder.OperationalProjectId == projectId)
            .OrderBy(folder => folder.Category).ThenBy(folder => folder.Id).ToListAsync(ct);
        foreach (var folder in folders)
        {
            var leaseToken = Guid.NewGuid();
            if (!await TryAcquireReconciliationLeaseAsync(folder, leaseToken, ct)) continue;
            var completed = false;
            try
            {
                await ReconcileFolderAsync(projectId, folder, ct);
                completed = true;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Project Drive folder {FolderId} reconciliation failed; remaining folders will continue", folder.Id);
            }
            finally
            {
                await ReleaseReconciliationLeaseAsync(folder, leaseToken, completed, ct);
            }
        }
    }

    private async Task ReconcileFolderAsync(int projectId, ProjectDriveFolder folder, CancellationToken ct)
    {
        var remoteItems = await drive.ListChildrenAsync(folder.DriveFolderId, ct);
        var remoteFiles = remoteItems.Where(item => !item.IsTrashed &&
                !item.MimeType.Equals("application/vnd.google-apps.folder", StringComparison.Ordinal))
            .OrderBy(item => item.Name, StringComparer.Ordinal).ThenBy(item => item.Id, StringComparer.Ordinal).ToList();
        var known = await db.ProjectDocuments.Where(document => document.OperationalProjectId == projectId &&
                document.DriveFolderId == folder.DriveFolderId && document.DriveFileId != null &&
                document.SyncStatus != ProjectDocumentSyncStatus.Deleted)
            .OrderBy(document => document.Id).ToListAsync(ct);
        await RemoveDuplicateManagedReplicasAsync(remoteFiles, known, ct);
        var remoteById = remoteFiles.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var now = DateTime.UtcNow;

        foreach (var document in known)
        {
            if (IsProtectedFromReconciliation(document, now))
            {
                remoteById.Remove(document.DriveFileId!);
                continue;
            }
            if (!remoteById.TryGetValue(document.DriveFileId!, out var remote))
            {
                if (IsDrivePrimary(document))
                {
                    document.DesiredOperation = ProjectDocumentDesiredOperation.None;
                    document.SyncStatus = ProjectDocumentSyncStatus.Deleted;
                    document.DeletedAt = now;
                    document.UpdatedAt = now;
                    continue;
                }
                document.DriveFileId = null;
                document.DriveWebViewLink = null;
                document.DriveVersion = null;
                document.DriveModifiedAt = null;
                document.Generation++;
                document.DesiredOperation = ProjectDocumentDesiredOperation.Upsert;
                document.SyncStatus = ProjectDocumentSyncStatus.Pending;
                document.SyncAttemptCount = 0;
                document.NextSyncAttemptAt = now;
                document.SyncError = null;
                document.UpdatedAt = now;
                continue;
            }

            if (IsDrivePrimary(document))
            {
                document.OriginalFileName = remote.Name;
                document.ContentType = remote.MimeType;
                document.Size = remote.Size ?? 0;
                if (HasRemoteChanged(document, remote)) document.Sha256 = string.Empty;
                document.DriveWebViewLink = remote.Link;
                document.DriveVersion = remote.Version;
                document.DriveModifiedAt = remote.ModifiedAt;
                document.IsDownloadable = !IsNativeGoogleFile(remote);
                document.UnsupportedReason = document.IsDownloadable ? null : UnsupportedNativeReason;
                document.UpdatedAt = now;
                remoteById.Remove(remote.Id);
                continue;
            }

            if (HasRemoteChanged(document, remote) && document.ConflictState == ProjectDocumentConflictState.None)
                await ImportConflictAsync(document, remote, ct);
            remoteById.Remove(remote.Id);
        }

        foreach (var remote in remoteById.Values)
        {
            try
            {
                var existingBinding = await db.ProjectDocuments.FirstOrDefaultAsync(document =>
                    document.DriveFileId == remote.Id, ct);
                if (existingBinding is not null)
                {
                    if (existingBinding.OperationalProjectId == projectId && IsDrivePrimary(existingBinding) &&
                        existingBinding.DesiredOperation != ProjectDocumentDesiredOperation.Delete)
                    {
                        existingBinding.Category = folder.Category;
                        existingBinding.DriveFolderId = folder.DriveFolderId;
                        existingBinding.DriveWebViewLink = remote.Link;
                        existingBinding.DriveVersion = remote.Version;
                        existingBinding.DriveModifiedAt = remote.ModifiedAt;
                        existingBinding.OriginalFileName = remote.Name;
                        existingBinding.ContentType = remote.MimeType;
                        existingBinding.Size = remote.Size ?? 0;
                        existingBinding.Sha256 = string.Empty;
                        existingBinding.IsDownloadable = !IsNativeGoogleFile(remote);
                        existingBinding.UnsupportedReason = existingBinding.IsDownloadable ? null : UnsupportedNativeReason;
                        existingBinding.SyncStatus = ProjectDocumentSyncStatus.Synced;
                        existingBinding.DeletedAt = null;
                        existingBinding.DeletedByUserId = null;
                        existingBinding.UpdatedAt = DateTime.UtcNow;
                    }
                    continue;
                }
                if (TryReadManagedReplica(remote, out var managedDocumentId, out var generation))
                {
                    var managedDocument = await db.ProjectDocuments.FirstOrDefaultAsync(document =>
                        document.Id == managedDocumentId && document.OperationalProjectId == projectId &&
                        document.Generation == generation, ct);
                    if (managedDocument is not null && !IsProtectedFromReconciliation(managedDocument, DateTime.UtcNow))
                    {
                        managedDocument.DriveFileId = remote.Id;
                        managedDocument.DriveFolderId = folder.DriveFolderId;
                        managedDocument.DriveWebViewLink = remote.Link;
                        managedDocument.DriveVersion = remote.Version;
                        managedDocument.DriveModifiedAt = remote.ModifiedAt;
                        managedDocument.DesiredOperation = ProjectDocumentDesiredOperation.None;
                        managedDocument.SyncStatus = ProjectDocumentSyncStatus.Synced;
                        managedDocument.SyncError = null;
                        managedDocument.ClaimToken = null;
                        managedDocument.ClaimExpiresAt = null;
                        managedDocument.UpdatedAt = DateTime.UtcNow;
                        continue;
                    }
                    if (managedDocument is not null) continue;
                }
                await ImportUnknownAsync(projectId, folder, remote, ct);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Remote Drive item {DriveFileId} could not be reconciled; processing will continue", remote.Id);
            }
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task RemoveDuplicateManagedReplicasAsync(
        List<DriveItem> remoteFiles, IReadOnlyCollection<ProjectDocument> known, CancellationToken ct)
    {
        var groups = new Dictionary<(long DocumentId, long Generation), List<DriveItem>>();
        foreach (var remote in remoteFiles)
        {
            if (!TryReadManagedReplica(remote, out var documentId, out var generation)) continue;
            var key = (documentId, generation);
            if (!groups.TryGetValue(key, out var replicas)) groups[key] = replicas = [];
            replicas.Add(remote);
        }

        foreach (var (key, replicas) in groups.Where(group => group.Value.Count > 1))
        {
            var boundFileId = known.FirstOrDefault(document =>
                document.Id == key.DocumentId && document.Generation == key.Generation)?.DriveFileId;
            var canonical = replicas.FirstOrDefault(replica => replica.Id == boundFileId) ??
                replicas.OrderBy(replica => replica.Id, StringComparer.Ordinal).First();
            foreach (var duplicate in replicas.Where(replica => replica.Id != canonical.Id))
            {
                await drive.DeleteAsync(duplicate.Id, ct);
                remoteFiles.Remove(duplicate);
                logger.LogWarning(
                    "Moved duplicate Drive replica {DriveFileId} for project document {DocumentId} generation {Generation} to trash",
                    duplicate.Id, key.DocumentId, key.Generation);
            }
        }
    }

    internal static bool IsProtectedFromReconciliation(ProjectDocument document, DateTime now) =>
        document.DesiredOperation == ProjectDocumentDesiredOperation.Delete ||
        document.ClaimToken.HasValue && document.ClaimExpiresAt > now;

    internal static Expression<Func<ProjectDocument, bool>> IsDueForClaim(DateTime now) => document =>
        ((document.SyncStatus == ProjectDocumentSyncStatus.Pending && document.SyncAttemptCount < ProjectDocumentService.MaxSyncAttempts) ||
         (document.SyncStatus == ProjectDocumentSyncStatus.Processing && document.SyncAttemptCount <= ProjectDocumentService.MaxSyncAttempts &&
          document.ClaimExpiresAt <= now)) &&
        (!document.NextSyncAttemptAt.HasValue || document.NextSyncAttemptAt <= now) &&
        (document.DesiredOperation == ProjectDocumentDesiredOperation.Upsert ||
         document.DesiredOperation == ProjectDocumentDesiredOperation.Delete);

    internal async Task<long?> TryClaimAsync(long documentId, Guid token, DateTime now, CancellationToken ct = default)
    {
        var generation = await db.ProjectDocuments.AsNoTracking().Where(document => document.Id == documentId)
            .Select(document => (long?)document.Generation).SingleOrDefaultAsync(ct);
        if (!generation.HasValue) return null;
        var affected = await db.ProjectDocuments.Where(document => document.Id == documentId && document.Generation == generation.Value)
            .Where(IsDueForClaim(now)).ExecuteUpdateAsync(setters => setters
                .SetProperty(document => document.SyncStatus, ProjectDocumentSyncStatus.Processing)
                .SetProperty(document => document.SyncAttemptCount, document =>
                    document.SyncStatus == ProjectDocumentSyncStatus.Pending ? document.SyncAttemptCount + 1 : document.SyncAttemptCount)
                .SetProperty(document => document.LastSyncAttemptAt, now)
                .SetProperty(document => document.NextSyncAttemptAt, (DateTime?)null)
                .SetProperty(document => document.SyncError, (string?)null)
                .SetProperty(document => document.ClaimToken, token)
                .SetProperty(document => document.ClaimExpiresAt, now.Add(ProjectDriveClaimLease.ClaimDuration))
                .SetProperty(document => document.UpdatedAt, now), ct);
        return affected == 1 ? generation : null;
    }

    private async Task ProcessClaimAsync(long documentId, Guid token, long generation, CancellationToken ct)
    {
        var document = await db.ProjectDocuments.AsNoTracking().Include(item => item.OperationalProject)
            .SingleAsync(item => item.Id == documentId && item.ClaimToken == token && item.Generation == generation, ct);
        try
        {
            if (document.DesiredOperation == ProjectDocumentDesiredOperation.Delete)
            {
                if (!await CanDeleteRemoteAsync(document, token, generation, ct)) return;
                if (!string.IsNullOrWhiteSpace(document.DriveFileId)) await drive.DeleteAsync(document.DriveFileId, ct);
                await using var deleteTransaction = db.Database.IsRelational()
                    ? await db.Database.BeginTransactionAsync(ct)
                    : null;
                var deleted = await CompleteDeleteAsync(document, token, generation, ct);
                if (deleted) await PropagateSurveyDeleteAsync(document, ct);
                if (deleteTransaction is not null) await deleteTransaction.CommitAsync(ct);
                if (deleted && !string.IsNullOrWhiteSpace(document.LocalPath) &&
                    document.SourceType != ProjectDocumentSourceType.ExistingManagedFile)
                    storage.DeleteOwned(document.OperationalProjectId, document.LocalPath);
                return;
            }

            var folder = await folderService.EnsureAsync(document.OperationalProject, document.Category, document.UpdatedByUserId, ct);
            if (!await RenewClaimAsync(documentId, token, generation, ct)) return;
            await MoveToFolderIfNeededAsync(document, folder, ct);
            await using var content = storage.OpenRead(document.OperationalProjectId, document.LocalPath);
            var upload = await claimLease.RunAsync(documentId, token, generation,
                operationCt => drive.UploadAsync(folder.DriveFolderId, ReplicaKey(document.Id), generation,
                    document.OriginalFileName, document.ContentType, content, operationCt), ct);
            var completedAt = DateTime.UtcNow;
            await using var completionTransaction = db.Database.IsRelational()
                ? await db.Database.BeginTransactionAsync(ct)
                : null;
            var completed = await db.ProjectDocuments.Where(MatchesFence(documentId, token, generation))
                .Where(item => item.SyncStatus == ProjectDocumentSyncStatus.Processing)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.DriveFileId, upload.FileId)
                    .SetProperty(item => item.DriveFolderId, folder.DriveFolderId)
                    .SetProperty(item => item.DriveWebViewLink, upload.Link)
                    .SetProperty(item => item.DriveVersion, upload.Version)
                    .SetProperty(item => item.DriveModifiedAt, upload.ModifiedAt)
                    .SetProperty(item => item.DesiredOperation, ProjectDocumentDesiredOperation.None)
                    .SetProperty(item => item.SyncStatus, ProjectDocumentSyncStatus.Synced)
                    .SetProperty(item => item.SyncError, (string?)null)
                    .SetProperty(item => item.ClaimToken, (Guid?)null)
                    .SetProperty(item => item.ClaimExpiresAt, (DateTime?)null)
                    .SetProperty(item => item.UpdatedAt, completedAt), ct);
            if (completed == 0)
                logger.LogWarning("Project document {DocumentId} upload completed remotely but SQL fencing rejected token/generation", documentId);
            else
            {
                await PropagateSurveySuccessAsync(document, folder, upload, completedAt, ct);
                if (completionTransaction is not null) await completionTransaction.CommitAsync(ct);
                if (document.Origin == ProjectDocumentOrigin.GoogleDrive &&
                    !string.IsNullOrWhiteSpace(document.DriveFileId) &&
                    !string.Equals(document.DriveFileId, upload.FileId, StringComparison.Ordinal) &&
                    !await db.ProjectDocuments.AsNoTracking().AnyAsync(item =>
                        item.ConflictWithDocumentId == document.Id && item.DriveFileId == document.DriveFileId, ct))
                    await drive.DeleteAsync(document.DriveFileId, ct);
                audit.Log(new AuditEvent
                {
                    Action = "project-document.sync_success",
                    ResourceType = EntityTypes.ProjectDocument,
                    ResourceId = documentId.ToString(),
                    Message = $"Project document #{documentId} synced to Google Drive.",
                    TargetSystem = "GoogleDrive",
                });
            }
        }
        catch (Exception exception)
        {
            await RecordFailureAsync(documentId, token, generation, exception, ct);
        }
    }

    internal async Task MoveToFolderIfNeededAsync(
        ProjectDocument document, ProjectDriveFolder folder, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(document.DriveFileId) &&
            !string.Equals(document.DriveFolderId, folder.DriveFolderId, StringComparison.Ordinal))
        {
            await drive.MoveAsync(document.DriveFileId, folder.DriveFolderId, ct);
        }
    }

    internal async Task<bool> CanDeleteRemoteAsync(
        ProjectDocument document, Guid token, long generation, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(document.DriveFileId)) return true;
        if (IsDrivePrimary(document)) return true;

        var remote = await drive.GetMetadataAsync(document.DriveFileId, ct);
        if (remote is null || remote.IsTrashed || !HasRemoteChanged(document, remote)) return true;

        var authoritative = await db.ProjectDocuments
            .SingleOrDefaultAsync(item => item.Id == document.Id &&
                item.ClaimToken == token && item.Generation == generation &&
                item.SyncStatus == ProjectDocumentSyncStatus.Processing &&
                item.DesiredOperation == ProjectDocumentDesiredOperation.Delete, ct);
        if (authoritative is null) return false;

        await ImportConflictAsync(authoritative, remote, ct);
        return false;
    }

    private async Task<bool> CompleteDeleteAsync(ProjectDocument document, Guid token, long generation, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return await db.ProjectDocuments.Where(MatchesFence(document.Id, token, generation))
            .Where(item => item.SyncStatus == ProjectDocumentSyncStatus.Processing)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.SyncStatus, ProjectDocumentSyncStatus.Deleted)
                .SetProperty(item => item.DesiredOperation, ProjectDocumentDesiredOperation.None)
                .SetProperty(item => item.DriveFileId, (string?)null)
                .SetProperty(item => item.ClaimToken, (Guid?)null)
                .SetProperty(item => item.ClaimExpiresAt, (DateTime?)null)
                .SetProperty(item => item.UpdatedAt, now), ct) == 1;
    }

    private async Task RecordFailureAsync(long documentId, Guid token, long generation, Exception exception, CancellationToken ct)
    {
        var attempt = await db.ProjectDocuments.AsNoTracking().Where(item => item.Id == documentId && item.ClaimToken == token)
            .Select(item => item.SyncAttemptCount).SingleOrDefaultAsync(ct);
        var terminal = attempt >= ProjectDocumentService.MaxSyncAttempts;
        var now = DateTime.UtcNow;
        const string safeError = "Không thể đồng bộ Google Drive. Vui lòng kiểm tra kết nối hoặc thử lại sau.";
        await using var failureTransaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(ct)
            : null;
        var failed = await db.ProjectDocuments.Where(MatchesFence(documentId, token, generation))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.SyncStatus, terminal ? ProjectDocumentSyncStatus.Failed : ProjectDocumentSyncStatus.Pending)
                .SetProperty(item => item.SyncError, safeError)
                .SetProperty(item => item.NextSyncAttemptAt, terminal ? null : now.Add(Backoff(attempt)))
                .SetProperty(item => item.ClaimToken, (Guid?)null)
                .SetProperty(item => item.ClaimExpiresAt, (DateTime?)null)
                .SetProperty(item => item.UpdatedAt, now), ct);
        if (failed == 1)
        {
            await PropagateSurveyFailureAsync(documentId, attempt, terminal, safeError, now, ct);
            if (failureTransaction is not null) await failureTransaction.CommitAsync(ct);
        }
        logger.LogWarning(exception, "Project document {DocumentId} Drive sync attempt {Attempt} failed", documentId, attempt);
        audit.Log(new AuditEvent
        {
            Action = "project-document.sync_failure",
            ResourceType = EntityTypes.ProjectDocument,
            ResourceId = documentId.ToString(),
            Message = $"Project document #{documentId} Drive sync failed.",
            Status = AuditStatus.Failure,
            FailureReason = safeError,
            TargetSystem = "GoogleDrive",
        });
    }

    private async Task PropagateSurveyClaimAsync(long documentId, DateTime claimedAt, CancellationToken ct)
    {
        var source = await GetSurveySourceAsync(documentId, ct);
        if (source is null) return;
        var media = await db.SurveyMedia.FirstOrDefaultAsync(item => item.Id == source.MediaId, ct);
        if (media is null) return;
        media.SyncStatus = SurveyMediaSyncStatus.Processing;
        media.SyncAttemptCount = source.SyncAttemptCount;
        media.SyncError = null;
        media.NextSyncAttemptAt = null;
        media.SyncStartedAt = claimedAt;
        media.LastSyncAttemptAt = claimedAt;
        media.UpdatedAt = claimedAt;
        await RecalculateSurveyAggregateAsync(media, ct);
    }

    internal async Task PropagateSurveySuccessAsync(ProjectDocument document, ProjectDriveFolder folder,
        DriveUpload upload, DateTime completedAt, CancellationToken ct)
    {
        if (!IsSurveyMedia(document)) return;
        var media = await db.SurveyMedia.FirstOrDefaultAsync(item => item.Id == document.SourceRecordId, ct);
        if (media is null) return;
        media.DriveFileId = upload.FileId;
        media.DriveFolderId = folder.DriveFolderId;
        media.DriveFolderLink = folder.DriveWebViewLink;
        media.SyncStatus = SurveyMediaSyncStatus.Synced;
        media.SyncAttemptCount = document.SyncAttemptCount;
        media.SyncError = null;
        media.NextSyncAttemptAt = null;
        media.LastSyncAttemptAt = document.LastSyncAttemptAt;
        media.SyncedAt = completedAt;
        media.UpdatedAt = completedAt;
        await RecalculateSurveyAggregateAsync(media, ct);
    }

    internal async Task PropagateSurveyFailureAsync(long documentId, int attempt, bool terminal,
        string safeError, DateTime failedAt, CancellationToken ct)
    {
        var source = await GetSurveySourceAsync(documentId, ct);
        if (source is null) return;
        var media = await db.SurveyMedia.FirstOrDefaultAsync(item => item.Id == source.MediaId, ct);
        if (media is null) return;
        media.SyncStatus = terminal ? SurveyMediaSyncStatus.Failed : SurveyMediaSyncStatus.Pending;
        media.SyncAttemptCount = attempt;
        media.SyncError = safeError;
        media.NextSyncAttemptAt = terminal ? null : failedAt.Add(Backoff(attempt));
        media.LastSyncAttemptAt = source.LastSyncAttemptAt;
        media.UpdatedAt = failedAt;
        await RecalculateSurveyAggregateAsync(media, ct);
    }

    internal async Task PropagateSurveyDeleteAsync(ProjectDocument document, CancellationToken ct)
    {
        if (!IsSurveyMedia(document)) return;
        var now = DateTime.UtcNow;
        var media = await db.SurveyMedia
            .Include(item => item.Survey)
            .ThenInclude(survey => survey.LinkedOpportunity)
            .FirstOrDefaultAsync(item => item.Id == document.SourceRecordId, ct);
        if (media is null) return;
        var currentProjectId = media.Survey.LinkedOpportunity?.OperationalProjectId;
        if (currentProjectId.HasValue && currentProjectId != document.OperationalProjectId) return;
        if (!string.IsNullOrWhiteSpace(media.DriveFileId) &&
            !string.Equals(media.DriveFileId, document.DriveFileId, StringComparison.Ordinal)) return;
        media.DriveFileId = null;
        media.DriveFolderId = null;
        media.DriveFolderLink = null;
        media.SyncStatus = SurveyMediaSyncStatus.Pending;
        media.SyncError = null;
        media.NextSyncAttemptAt = null;
        media.SyncStartedAt = null;
        media.SyncedAt = null;
        media.UpdatedAt = now;
        await RecalculateSurveyAggregateAsync(media, ct);
    }

    private async Task RecalculateSurveyAggregateAsync(SurveyMedia changedMedia, CancellationToken ct)
    {
        var media = await db.SurveyMedia.Where(item => item.SurveyId == changedMedia.SurveyId).ToListAsync(ct);
        if (media.Count == 0) return;
        var status = media.Any(item => item.SyncStatus == SurveyMediaSyncStatus.Processing)
            ? SurveyDriveSyncStatus.Syncing
            : media.Any(item => item.SyncStatus == SurveyMediaSyncStatus.Failed)
                ? SurveyDriveSyncStatus.Failed
                : media.All(item => item.SyncStatus == SurveyMediaSyncStatus.Synced)
                    ? SurveyDriveSyncStatus.Synced
                    : SurveyDriveSyncStatus.NotSynced;
        var error = media.Where(item => item.SyncStatus == SurveyMediaSyncStatus.Failed)
            .OrderByDescending(item => item.LastSyncAttemptAt).Select(item => item.SyncError).FirstOrDefault();
        var lastSyncedAt = media.Where(item => item.SyncedAt.HasValue).Max(item => item.SyncedAt);
        var survey = await db.Surveys.FirstAsync(item => item.Id == changedMedia.SurveyId, ct);
        survey.DriveSyncStatus = status;
        survey.DriveSyncError = error;
        survey.LastSyncedAt = lastSyncedAt;
        survey.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private async Task<SurveySource?> GetSurveySourceAsync(long documentId, CancellationToken ct) =>
        await db.ProjectDocuments.AsNoTracking()
            .Where(document => document.Id == documentId && document.SourceModule == ProjectDocumentSourceModule.Survey &&
                document.SourceEntityType == EntityTypes.SurveyMedia && document.SourceRecordId.HasValue)
            .Select(document => new SurveySource(
                document.SourceRecordId!.Value,
                document.SyncAttemptCount,
                document.LastSyncAttemptAt))
            .SingleOrDefaultAsync(ct);

    private static bool IsSurveyMedia(ProjectDocument document) =>
        document.SourceModule == ProjectDocumentSourceModule.Survey &&
        document.SourceEntityType == EntityTypes.SurveyMedia &&
        document.SourceRecordId.HasValue;

    private sealed record SurveySource(long MediaId, int SyncAttemptCount, DateTime? LastSyncAttemptAt);

    private async Task ImportUnknownAsync(int projectId, ProjectDriveFolder folder, DriveItem remote, CancellationToken ct)
    {
        var imported = NewImport(projectId, folder, remote, null);
        try
        {
            db.ProjectDocuments.Add(imported);
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.Entry(imported).State = EntityState.Detached;
            if (!await db.ProjectDocuments.AsNoTracking().AnyAsync(document => document.DriveFileId == remote.Id, ct))
                throw;
        }
    }

    private async Task ImportConflictAsync(ProjectDocument authoritative, DriveItem remote, CancellationToken ct)
    {
        var observedVersion = ObservedVersionIdentity(remote);
        if (await db.ProjectDocuments.AnyAsync(document => document.ConflictWithDocumentId == authoritative.Id &&
            document.ConflictObservedDriveFileId == remote.Id && document.ConflictObservedDriveVersion == observedVersion, ct)) return;
        var conflictName = $"{Path.GetFileNameWithoutExtension(remote.Name)}_drive-conflict{Path.GetExtension(remote.Name)}";
        var conflict = NewImport(authoritative.OperationalProjectId,
            new ProjectDriveFolder { Category = authoritative.Category, DriveFolderId = authoritative.DriveFolderId! },
            remote with { Name = conflictName }, authoritative.Id);
        try
        {
            conflict.Category = authoritative.Category;
            conflict.SyncStatus = ProjectDocumentSyncStatus.Conflict;
            conflict.ConflictState = ProjectDocumentConflictState.PendingConfirmation;
            conflict.ConflictObservedDriveFileId = remote.Id;
            conflict.ConflictObservedDriveVersion = observedVersion;
            db.ProjectDocuments.Add(conflict);
            authoritative.DriveFileId = null;
            authoritative.DriveWebViewLink = null;
            authoritative.DriveVersion = null;
            authoritative.DriveModifiedAt = null;
            authoritative.Generation++;
            authoritative.DesiredOperation = ProjectDocumentDesiredOperation.Upsert;
            authoritative.ConflictState = ProjectDocumentConflictState.PendingConfirmation;
            authoritative.SyncStatus = ProjectDocumentSyncStatus.Pending;
            authoritative.SyncAttemptCount = 0;
            authoritative.NextSyncAttemptAt = DateTime.UtcNow;
            authoritative.SyncError = null;
            authoritative.ClaimToken = null;
            authoritative.ClaimExpiresAt = null;
            authoritative.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.Entry(conflict).State = EntityState.Detached;
            await db.Entry(authoritative).ReloadAsync(ct);
            if (!await db.ProjectDocuments.AsNoTracking().AnyAsync(document =>
                    document.ConflictWithDocumentId == authoritative.Id &&
                    document.ConflictObservedDriveFileId == remote.Id &&
                    document.ConflictObservedDriveVersion == observedVersion, ct))
                throw;
        }
    }

    private static ProjectDocument NewImport(
        int projectId,
        ProjectDriveFolder folder,
        DriveItem remote,
        long? conflictWithId) => new()
        {
            OperationalProjectId = projectId,
            Category = folder.Category,
            SourceModule = ProjectDocumentSourceModule.General,
            SourceType = ProjectDocumentSourceType.GoogleDriveImport,
            LocalPath = string.Empty,
            OriginalFileName = remote.Name,
            ContentType = remote.MimeType,
            Size = remote.Size ?? 0,
            Sha256 = string.Empty,
            Origin = ProjectDocumentOrigin.GoogleDrive,
            Generation = 1,
            DesiredOperation = ProjectDocumentDesiredOperation.None,
            SyncStatus = ProjectDocumentSyncStatus.Synced,
            DriveFileId = string.IsNullOrWhiteSpace(remote.Id) ? null : remote.Id,
            DriveFolderId = folder.DriveFolderId,
            DriveWebViewLink = remote.Link,
            DriveVersion = remote.Version,
            DriveModifiedAt = remote.ModifiedAt,
            IsDownloadable = !IsNativeGoogleFile(remote),
            UnsupportedReason = IsNativeGoogleFile(remote) ? UnsupportedNativeReason : null,
            ConflictWithDocumentId = conflictWithId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

    private static bool HasRemoteChanged(ProjectDocument document, DriveItem remote) =>
        !string.IsNullOrWhiteSpace(document.DriveVersion) &&
        !string.Equals(document.DriveVersion, remote.Version, StringComparison.Ordinal) &&
        (!document.DriveModifiedAt.HasValue || remote.ModifiedAt > document.DriveModifiedAt);

    internal static string ReplicaKey(long documentId) => $"project-document:{documentId}";

    private async Task<bool> RenewClaimAsync(long documentId, Guid token, long generation, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return await db.ProjectDocuments.Where(MatchesFence(documentId, token, generation))
            .Where(document => document.SyncStatus == ProjectDocumentSyncStatus.Processing)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(document => document.ClaimExpiresAt, now.Add(ProjectDriveClaimLease.ClaimDuration))
                .SetProperty(document => document.UpdatedAt, now), ct) == 1;
    }

    private async Task<bool> TryAcquireReconciliationLeaseAsync(ProjectDriveFolder folder, Guid token, CancellationToken ct)
    {
        await db.Entry(folder).ReloadAsync(ct);
        var now = DateTime.UtcNow;
        if (folder.ReconciliationClaimToken.HasValue && folder.ReconciliationClaimExpiresAt > now) return false;
        folder.ReconciliationClaimToken = token;
        folder.ReconciliationClaimExpiresAt = now.Add(ReconciliationLease);
        folder.UpdatedAt = now;
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            db.Entry(folder).State = EntityState.Detached;
            return false;
        }
    }

    private async Task ReleaseReconciliationLeaseAsync(ProjectDriveFolder folder, Guid token, bool completed, CancellationToken ct)
    {
        if (db.Entry(folder).State == EntityState.Detached)
            folder = await db.ProjectDriveFolders.SingleAsync(item => item.Id == folder.Id, ct);
        else
            await db.Entry(folder).ReloadAsync(ct);
        if (folder.ReconciliationClaimToken != token) return;
        folder.ReconciliationClaimToken = null;
        folder.ReconciliationClaimExpiresAt = null;
        if (completed) folder.LastReconciledAt = DateTime.UtcNow;
        folder.UpdatedAt = DateTime.UtcNow;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.Entry(folder).State = EntityState.Detached;
        }
    }

    private static bool IsNativeGoogleFile(DriveItem remote) =>
        remote.MimeType.StartsWith(NativeMimePrefix, StringComparison.Ordinal);

    private static string ObservedVersionIdentity(DriveItem remote) => remote.Version ??
        remote.ModifiedAt?.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "unknown";

    private bool TryReadManagedReplica(DriveItem remote, out long documentId, out long generation)
    {
        documentId = 0;
        generation = 0;
        return TryGetNiconProperty(remote.AppProperties, "Instance", out var instance) &&
            string.Equals(instance, currentInstanceId, StringComparison.Ordinal) &&
            TryGetNiconProperty(remote.AppProperties, "ReplicaKey", out var replicaKey) &&
            replicaKey.StartsWith("project-document:", StringComparison.Ordinal) &&
            long.TryParse(replicaKey["project-document:".Length..], out documentId) &&
            TryGetNiconProperty(remote.AppProperties, "Generation", out var generationValue) &&
            long.TryParse(generationValue, out generation);
    }

    private static bool TryGetNiconProperty(
        IReadOnlyDictionary<string, string> properties,
        string suffix,
        out string value) =>
        properties.TryGetValue($"nicon{suffix}", out value!) ||
        properties.TryGetValue($"nihome{suffix}", out value!);

    private static bool IsDrivePrimary(ProjectDocument document) =>
        string.IsNullOrWhiteSpace(document.LocalPath) ||
        document.SourceType == ProjectDocumentSourceType.GoogleDriveImport;

    internal static Expression<Func<ProjectDocument, bool>> MatchesFence(long documentId, Guid token, long generation) =>
        document => document.Id == documentId && document.ClaimToken == token && document.Generation == generation;
    internal static TimeSpan Backoff(int attempt) => attempt <= 1 ? TimeSpan.FromMinutes(1) : TimeSpan.FromMinutes(5);
}

public sealed class ProjectDriveSyncService(
    IServiceScopeFactory scopeFactory,
    ILogger<ProjectDriveSyncService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeSpan.FromSeconds(15);
            try
            {
                using var scope = scopeFactory.CreateScope();
                var settings = scope.ServiceProvider.GetRequiredService<IGoogleDriveSettingsStore>();
                var options = await settings.GetRuntimeAsync(stoppingToken);
                delay = TimeSpan.FromSeconds(Math.Clamp(options.PollIntervalSeconds, 5, 300));
                if (!options.Enabled)
                {
                    await Task.Delay(delay, stoppingToken);
                    continue;
                }
                var processor = scope.ServiceProvider.GetRequiredService<IProjectDriveSyncProcessor>();
                var processed = await processor.ProcessNextOutboundAsync(stoppingToken);
                await processor.ReconcileBoundProjectsAsync(stoppingToken);
                if (!processed) await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.LogError(exception, "Project Drive synchronization failed; polling will resume.");
                await Task.Delay(delay, stoppingToken);
            }
        }
    }
}
