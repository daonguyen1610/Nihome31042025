using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services.GoogleDrive;

namespace NihomeBackend.Services;

public sealed class ProjectDocumentService(
    AppDbContext db,
    IProjectDocumentStorageService storage,
    IGoogleDriveAdapter drive,
    IProjectDriveFolderService folders,
    IGoogleDriveSettingsStore driveSettings) : IProjectDocumentService, IProjectDocumentStagingService
{
    public const int MaxSyncAttempts = 3;

    public async Task<IReadOnlyList<ProjectDocumentResponse>?> ListAsync(
        int projectId, int callerUserId, bool canSeeAll, CancellationToken ct = default)
    {
        if (!await CanAccessProjectAsync(projectId, callerUserId, canSeeAll, ct)) return null;
        return await db.ProjectDocuments.AsNoTracking()
            .Where(document => document.OperationalProjectId == projectId && document.SyncStatus != ProjectDocumentSyncStatus.Deleted)
            .OrderBy(document => document.Category).ThenByDescending(document => document.UpdatedAt)
            .Select(document => Map(document)).ToListAsync(ct);
    }

    public async Task<ProjectDocumentResponse?> UploadAsync(
        int projectId, ProjectDocumentUploadRequest request, int callerUserId, bool canSeeAll, CancellationToken ct = default)
    {
        if (!await CanAccessProjectAsync(projectId, callerUserId, canSeeAll, ct)) return null;
        ValidateCategory(request.Category, allowUnclassified: false);
        await ValidateMetadataAsync(projectId, request.SourceModule, request.SourceRecordId, request.CustomerId, request.ContractId, ct);
        await ValidateManualDesignSourceAsync(projectId, request, ct);
        var stored = await storage.StoreAsync(projectId, request.File, ct);
        try
        {
            var document = CreateDocument(projectId, request.Category, request.SourceModule,
                ProjectDocumentSourceType.ManualUpload, request.SourceRecordId, request.CustomerId,
                request.ContractId, stored, ProjectDocumentOrigin.Nicon, callerUserId);
            document.SourceEntityType = string.IsNullOrWhiteSpace(request.SourceEntityType)
                ? null
                : request.SourceEntityType.Trim();
            // A manual upload is a distinct file, even when several files share
            // one source record and have no local path (Drive-only storage).
            document.SourceSlot = $"manual-upload:{Guid.NewGuid():N}";
            document.NextSyncAttemptAt = (await driveSettings.GetRuntimeAsync(ct)).Enabled ? DateTime.UtcNow : null;
            db.ProjectDocuments.Add(document);
            await db.SaveChangesAsync(ct);
            return Map(document);
        }
        catch
        {
            storage.DeleteOwned(projectId, stored.LocalPath);
            throw;
        }
    }

    public async Task<ProjectDocument> StageExistingManagedFileAsync(
        int projectId, ProjectDocumentCategory category, ProjectDocumentSourceModule sourceModule,
        string sourceEntityType, string sourceSlot, long sourceRecordId, string localPath,
        string originalFileName, int? customerId, int? contractId, int? userId, CancellationToken ct = default)
    {
        ValidateCategory(category, allowUnclassified: false);
        if (string.IsNullOrWhiteSpace(sourceEntityType) || string.IsNullOrWhiteSpace(sourceSlot) || sourceRecordId <= 0)
            throw new ProjectDocumentValidationException("Định danh nguồn nội bộ của tệp dự án không hợp lệ.");
        await ValidateMetadataAsync(projectId, sourceModule, sourceRecordId, customerId, contractId, ct);
        var stored = await storage.InspectExistingAsync(sourceModule, localPath.Trim(), originalFileName, ct);
        var normalizedEntityType = sourceEntityType.Trim();
        var normalizedSlot = sourceSlot.Trim();
        var existing = db.ProjectDocuments.Local.FirstOrDefault(document =>
            document.OperationalProjectId == projectId && document.SourceModule == sourceModule &&
            document.SourceEntityType == normalizedEntityType && document.SourceSlot == normalizedSlot &&
            document.SourceRecordId == sourceRecordId && document.LocalPath == stored.LocalPath)
            ?? await db.ProjectDocuments.FirstOrDefaultAsync(document =>
                document.OperationalProjectId == projectId && document.SourceModule == sourceModule &&
                document.SourceEntityType == normalizedEntityType && document.SourceSlot == normalizedSlot &&
                document.SourceRecordId == sourceRecordId && document.LocalPath == stored.LocalPath, ct);
        if (existing is not null)
        {
            if (existing.SyncStatus == ProjectDocumentSyncStatus.Deleted || existing.DesiredOperation == ProjectDocumentDesiredOperation.Delete)
            {
                existing.Category = category;
                existing.Generation++;
                existing.DesiredOperation = ProjectDocumentDesiredOperation.Upsert;
                existing.SyncStatus = ProjectDocumentSyncStatus.Pending;
                existing.SyncAttemptCount = 0;
                existing.NextSyncAttemptAt = DateTime.UtcNow;
                existing.SyncError = null;
                existing.DeletedAt = null;
                existing.DeletedByUserId = null;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.UpdatedByUserId = userId;
            }
            return existing;
        }

        var document = CreateDocument(projectId, category, sourceModule,
            ProjectDocumentSourceType.ExistingManagedFile, sourceRecordId, customerId,
            contractId, stored, ProjectDocumentOrigin.Nicon, userId);
        document.SourceEntityType = normalizedEntityType;
        document.SourceSlot = normalizedSlot;
        db.ProjectDocuments.Add(document);
        return document;
    }

    public async Task<bool> StageExistingManagedFileDeleteAsync(int projectId, ProjectDocumentSourceModule sourceModule,
        string sourceEntityType, string sourceSlot, long sourceRecordId, string localPath, int? userId,
        CancellationToken ct = default)
    {
        var normalizedEntityType = sourceEntityType.Trim();
        var normalizedSlot = sourceSlot.Trim();
        var normalizedPath = localPath.Trim();
        var document = db.ProjectDocuments.Local.FirstOrDefault(item => item.OperationalProjectId == projectId &&
            item.SourceModule == sourceModule && item.SourceEntityType == normalizedEntityType &&
            item.SourceSlot == normalizedSlot && item.SourceRecordId == sourceRecordId && item.LocalPath == normalizedPath)
            ?? await db.ProjectDocuments.FirstOrDefaultAsync(item => item.OperationalProjectId == projectId &&
                item.SourceModule == sourceModule && item.SourceEntityType == normalizedEntityType &&
                item.SourceSlot == normalizedSlot && item.SourceRecordId == sourceRecordId &&
                item.LocalPath == normalizedPath, ct);
        if (document?.SyncStatus == ProjectDocumentSyncStatus.Processing)
            throw new ProjectDocumentConflictException("Tệp đang được đồng bộ. Vui lòng chờ lần xử lý hiện tại hoàn tất trước khi xoá.");
        if (document is null || document.DesiredOperation == ProjectDocumentDesiredOperation.Delete ||
            document.SyncStatus == ProjectDocumentSyncStatus.Deleted) return document is not null;
        if (await db.ConstructionResourceRequests.AnyAsync(x => x.SupplyDocumentId == document.Id, ct))
            throw new ProjectDocumentValidationException("This document is supply evidence for a construction resource request and cannot be deleted.");
        document.DesiredOperation = ProjectDocumentDesiredOperation.Delete;
        document.SyncStatus = ProjectDocumentSyncStatus.Pending;
        document.SyncAttemptCount = 0;
        document.NextSyncAttemptAt = DateTime.UtcNow;
        document.SyncError = null;
        document.DeletedAt = DateTime.UtcNow;
        document.DeletedByUserId = userId;
        document.UpdatedAt = DateTime.UtcNow;
        document.UpdatedByUserId = userId;
        return true;
    }

    public async Task<bool> RetryExistingManagedFileAsync(int projectId, ProjectDocumentSourceModule sourceModule,
        string sourceEntityType, string sourceSlot, long sourceRecordId, string localPath, int? userId,
        CancellationToken ct = default)
    {
        var normalizedEntityType = sourceEntityType.Trim();
        var normalizedSlot = sourceSlot.Trim();
        var normalizedPath = localPath.Trim();
        var document = await db.ProjectDocuments.FirstOrDefaultAsync(item => item.OperationalProjectId == projectId &&
            item.SourceModule == sourceModule && item.SourceEntityType == normalizedEntityType &&
            item.SourceSlot == normalizedSlot && item.SourceRecordId == sourceRecordId &&
            item.LocalPath == normalizedPath, ct);
        if (document is null) return false;
        if (document.SyncStatus == ProjectDocumentSyncStatus.Processing)
            throw new ProjectDocumentConflictException("Tệp đang được đồng bộ. Vui lòng chờ lần xử lý hiện tại hoàn tất trước khi thử lại.");
        if (document.DesiredOperation != ProjectDocumentDesiredOperation.Upsert ||
            document.SyncStatus is not (ProjectDocumentSyncStatus.Failed or ProjectDocumentSyncStatus.Pending) ||
            document.SyncAttemptCount >= MaxSyncAttempts)
            throw new ProjectDocumentValidationException("Tệp không thể được đưa lại vào hàng đợi đồng bộ dự án.");
        document.SyncStatus = ProjectDocumentSyncStatus.Pending;
        document.NextSyncAttemptAt = DateTime.UtcNow;
        document.SyncError = null;
        document.ClaimToken = null;
        document.ClaimExpiresAt = null;
        document.UpdatedAt = DateTime.UtcNow;
        document.UpdatedByUserId = userId;
        return true;
    }

    public async Task StageExistingManagedFilesMoveAsync(
        int? oldProjectId, int? newProjectId,
        IReadOnlyCollection<ProjectDocumentMoveDescriptor> files,
        int? userId, CancellationToken ct = default)
    {
        if (oldProjectId == newProjectId || files.Count == 0) return;

        foreach (var file in files)
        {
            if (newProjectId.HasValue)
            {
                await StageExistingManagedFileAsync(
                    newProjectId.Value, file.Category, file.SourceModule,
                    file.SourceEntityType, file.SourceSlot, file.SourceRecordId,
                    file.LocalPath, file.OriginalFileName, file.CustomerId,
                    file.ContractId, userId, ct);
            }

            if (oldProjectId.HasValue)
            {
                await StageExistingManagedFileDeleteAsync(
                    oldProjectId.Value, file.SourceModule, file.SourceEntityType,
                    file.SourceSlot, file.SourceRecordId, file.LocalPath, userId, ct);
            }
        }
    }

    public async Task<ProjectDocumentDownload?> DownloadAsync(
        int projectId, long documentId, int callerUserId, bool canSeeAll, CancellationToken ct = default)
    {
        if (!await CanAccessProjectAsync(projectId, callerUserId, canSeeAll, ct)) return null;
        var document = await FindAsync(projectId, documentId, ct);
        if (document is null || !document.IsDownloadable) return null;
        var stagedManualUpload = document.SourceType == ProjectDocumentSourceType.ManualUpload &&
            document.DesiredOperation == ProjectDocumentDesiredOperation.Upsert &&
            !string.IsNullOrWhiteSpace(document.LocalPath);
        if (!stagedManualUpload && !string.IsNullOrWhiteSpace(document.DriveFileId))
        {
            return new ProjectDocumentDownload(
                document.OriginalFileName,
                document.ContentType,
                (destination, cancellationToken) => drive.DownloadAsync(document.DriveFileId, destination, cancellationToken));
        }
        if (string.IsNullOrWhiteSpace(document.LocalPath)) return null;
        return new ProjectDocumentDownload(
            document.OriginalFileName,
            document.ContentType,
            async (destination, cancellationToken) =>
            {
                await using var source = storage.OpenRead(projectId, document.LocalPath);
                await source.CopyToAsync(destination, cancellationToken);
            });
    }

    public async Task<ProjectDocumentResponse?> ClassifyAsync(
        int projectId, long documentId, ClassifyProjectDocumentRequest request, int callerUserId, bool canSeeAll, CancellationToken ct = default)
    {
        if (!await CanAccessProjectAsync(projectId, callerUserId, canSeeAll, ct)) return null;
        ValidateCategory(request.Category, allowUnclassified: false);
        var document = await FindAsync(projectId, documentId, ct);
        if (document is null) return null;
        if (document.Category != ProjectDocumentCategory.Unclassified)
            throw new ProjectDocumentValidationException("Chỉ tệp chưa phân loại mới có thể được phân loại bằng thao tác này.");
        if (string.IsNullOrWhiteSpace(document.DriveFileId))
            throw new ProjectDocumentValidationException("Tệp chưa có trên Google Drive nên không thể phân loại.");
        var project = await db.OperationalProjects.SingleAsync(item => item.Id == projectId, ct);
        var destination = await folders.EnsureAsync(project, request.Category, callerUserId, ct);
        var previousFolderId = document.DriveFolderId;
        await drive.MoveAsync(document.DriveFileId, destination.DriveFolderId, ct);
        try
        {
            document.Category = request.Category;
            document.DriveFolderId = destination.DriveFolderId;
            document.DesiredOperation = ProjectDocumentDesiredOperation.None;
            document.SyncStatus = ProjectDocumentSyncStatus.Synced;
            document.SyncAttemptCount = 0;
            document.SyncError = null;
            document.UpdatedAt = DateTime.UtcNow;
            document.UpdatedByUserId = callerUserId;
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(previousFolderId))
                await drive.MoveAsync(document.DriveFileId, previousFolderId, CancellationToken.None);
            throw;
        }
        return Map(document);
    }

    public async Task<ProjectDocumentResponse?> ResolveConflictAsync(
        int projectId, long documentId, ResolveProjectDocumentConflictRequest request, int callerUserId, bool canSeeAll, CancellationToken ct = default)
    {
        if (!await CanAccessProjectAsync(projectId, callerUserId, canSeeAll, ct)) return null;
        var document = await FindAsync(projectId, documentId, ct);
        if (document is null) return null;
        if (document.ConflictState != ProjectDocumentConflictState.PendingConfirmation)
            throw new ProjectDocumentValidationException("Tệp không có xung đột đang chờ xác nhận.");
        if (!request.ConfirmKeepBoth)
            throw new ProjectDocumentValidationException("Phải xác nhận giữ cả hai phiên bản để giải quyết xung đột an toàn.");
        var linkedId = document.ConflictWithDocumentId ?? document.Id;
        var conflictDocuments = await db.ProjectDocuments.Where(item =>
            item.Id == document.Id || item.Id == linkedId || item.ConflictWithDocumentId == linkedId).ToListAsync(ct);
        var now = DateTime.UtcNow;
        foreach (var conflictDocument in conflictDocuments)
        {
            conflictDocument.ConflictState = ProjectDocumentConflictState.None;
            if (conflictDocument.ConflictWithDocumentId.HasValue)
            {
                conflictDocument.SyncStatus = ProjectDocumentSyncStatus.Synced;
                conflictDocument.DesiredOperation = ProjectDocumentDesiredOperation.None;
            }
            else if (conflictDocument.SyncStatus == ProjectDocumentSyncStatus.Conflict)
            {
                conflictDocument.SyncStatus = conflictDocument.DesiredOperation == ProjectDocumentDesiredOperation.Upsert
                    ? ProjectDocumentSyncStatus.Pending
                    : ProjectDocumentSyncStatus.Synced;
            }
            conflictDocument.UpdatedAt = now;
            conflictDocument.UpdatedByUserId = callerUserId;
        }
        await db.SaveChangesAsync(ct);
        return Map(document);
    }

    public async Task<bool> DeleteAsync(
        int projectId, long documentId, int callerUserId, bool canSeeAll, CancellationToken ct = default)
    {
        if (!await CanAccessProjectAsync(projectId, callerUserId, canSeeAll, ct)) return false;
        var optionId = await db.ProjectDocuments.AsNoTracking()
            .Where(item => item.Id == documentId && item.OperationalProjectId == projectId &&
                item.SourceEntityType == nameof(ConceptOption))
            .Select(item => item.SourceRecordId).SingleOrDefaultAsync(ct);
        await using var transaction = optionId.HasValue
            ? await ConceptDossierTransaction.BeginAsync(db, checked((int)optionId.Value), ct) : null;
        var document = await FindAsync(projectId, documentId, ct);
        if (document is null) return false;
        if (await db.ConceptArtifacts.AnyAsync(item => item.ProjectDocumentId == documentId &&
                item.ConceptRevision.FrozenAt != null, ct) ||
            await db.DesignReviews.AnyAsync(review => review.Stage == DesignReviewStage.Concept &&
                (review.ClientAcceptanceDocumentId == documentId ||
                 review.ClientSubmittedAt != null && review.Documents.Any(item => item.ProjectDocumentId == documentId)), ct))
            throw new ProjectDocumentValidationException("Hồ sơ Concept đã trình và biên bản nghiệm thu phải được giữ nguyên; hãy tạo revision mới.");
        if (await db.ConstructionResourceRequests.AnyAsync(x => x.SupplyDocumentId == documentId, ct))
            throw new ProjectDocumentValidationException("This document is supply evidence for a construction resource request and cannot be deleted.");
        if (await db.RfqDocuments.AnyAsync(link => link.ProjectDocumentId == documentId, ct))
            throw new ProjectDocumentValidationException("Tệp là bằng chứng RFQ/báo giá; không thể xoá tài liệu đã liên kết.");
        if (document.SourceType == ProjectDocumentSourceType.ExistingManagedFile)
            throw new ProjectDocumentValidationException(
                "Tệp thuộc bản ghi nghiệp vụ nguồn; vui lòng xoá hoặc thay thế tệp tại chức năng đã tạo tệp.");
        if (document.SyncStatus == ProjectDocumentSyncStatus.Processing)
            throw new ProjectDocumentConflictException("Tệp đang được đồng bộ. Vui lòng chờ lần xử lý hiện tại hoàn tất trước khi xoá.");
        document.DesiredOperation = ProjectDocumentDesiredOperation.Delete;
        document.SyncStatus = ProjectDocumentSyncStatus.Pending;
        document.SyncAttemptCount = 0;
        document.NextSyncAttemptAt = DateTime.UtcNow;
        document.SyncError = null;
        document.DeletedAt = DateTime.UtcNow;
        document.DeletedByUserId = callerUserId;
        document.UpdatedAt = DateTime.UtcNow;
        document.UpdatedByUserId = callerUserId;
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        if (string.IsNullOrWhiteSpace(document.DriveFileId))
        {
            try
            {
                storage.DeleteOwned(projectId, document.LocalPath);
                document.LocalPath = string.Empty;
                document.SyncStatus = ProjectDocumentSyncStatus.Deleted;
                document.DesiredOperation = ProjectDocumentDesiredOperation.None;
                document.NextSyncAttemptAt = null;
                await db.SaveChangesAsync(ct);
            }
            catch
            {
                // Keep the durable delete intent visible and retryable.
                document.SyncStatus = ProjectDocumentSyncStatus.Failed;
                document.DesiredOperation = ProjectDocumentDesiredOperation.Delete;
                document.SyncError = "Không xóa được bản lưu trên host; hãy thử xóa lại.";
                await db.SaveChangesAsync(CancellationToken.None);
                throw;
            }
        }
        return true;
    }

    public async Task<ProjectDocumentResponse?> RetryAsync(
        int projectId, long documentId, int callerUserId, bool canSeeAll, CancellationToken ct = default)
    {
        if (!await CanAccessProjectAsync(projectId, callerUserId, canSeeAll, ct)) return null;
        var document = await FindAsync(projectId, documentId, ct);
        if (document is null) return null;
        if (document.SyncStatus == ProjectDocumentSyncStatus.Processing)
            throw new ProjectDocumentConflictException("Tệp đang được đồng bộ. Vui lòng chờ trước khi thử lại.");
        if (document.SyncStatus is not
            (ProjectDocumentSyncStatus.Failed or ProjectDocumentSyncStatus.Pending))
            throw new ProjectDocumentValidationException("Chỉ tệp đang chờ hoặc đồng bộ lỗi mới có thể gửi lại.");
        document.SyncStatus = ProjectDocumentSyncStatus.Pending;
        document.SyncAttemptCount = 0;
        document.NextSyncAttemptAt = DateTime.UtcNow;
        document.SyncError = null;
        document.ClaimToken = null;
        document.ClaimExpiresAt = null;
        document.UpdatedAt = DateTime.UtcNow;
        document.UpdatedByUserId = callerUserId;
        await db.SaveChangesAsync(ct);
        return Map(document);
    }

    private async Task<bool> CanAccessProjectAsync(int projectId, int callerUserId, bool canSeeAll, CancellationToken ct) =>
        await db.OperationalProjects.AsNoTracking().AnyAsync(project => project.Id == projectId &&
            (canSeeAll || project.ProjectManagerUserId == callerUserId || project.CreatedByUserId == callerUserId), ct);

    private async Task ValidateMetadataAsync(int projectId, ProjectDocumentSourceModule sourceModule,
        long? sourceRecordId, int? customerId, int? contractId, CancellationToken ct)
    {
        if (!Enum.IsDefined(sourceModule)) throw new ProjectDocumentValidationException("Phân hệ nguồn không hợp lệ.");
        if (sourceModule != ProjectDocumentSourceModule.General && !sourceRecordId.HasValue)
            throw new ProjectDocumentValidationException("Mã bản ghi nguồn là bắt buộc khi chọn phân hệ nguồn.");
        var project = await db.OperationalProjects.AsNoTracking().Where(item => item.Id == projectId)
            .Select(item => new { item.CustomerId }).SingleAsync(ct);
        if (customerId.HasValue && customerId.Value != project.CustomerId)
            throw new ProjectDocumentValidationException("Khách hàng nguồn không thuộc dự án đã chọn.");
        var trackedContract = contractId.HasValue
            ? db.Contracts.Local.FirstOrDefault(contract => contract.Id == contractId.Value)
            : null;
        if (contractId.HasValue &&
            (trackedContract is not null
                ? trackedContract.OperationalProjectId != projectId
                : !await db.Contracts.AsNoTracking().AnyAsync(contract =>
                    contract.Id == contractId && contract.OperationalProjectId == projectId, ct)))
            throw new ProjectDocumentValidationException("Hợp đồng nguồn không thuộc dự án đã chọn.");
    }

    private async Task ValidateManualDesignSourceAsync(
        int projectId, ProjectDocumentUploadRequest request, CancellationToken ct)
    {
        if (request.SourceModule != ProjectDocumentSourceModule.Design)
        {
            if (!string.IsNullOrWhiteSpace(request.SourceEntityType))
                throw new ProjectDocumentValidationException(
                    "Loại bản ghi nguồn chỉ được khai báo cho tài liệu phân hệ Thiết kế.");
            return;
        }

        var sourceType = request.SourceEntityType?.Trim();
        if (string.IsNullOrWhiteSpace(sourceType) || !request.SourceRecordId.HasValue)
            throw new ProjectDocumentValidationException(
                "Tài liệu Thiết kế phải gắn với đúng hồ sơ nguồn Concept, Basic Design hoặc Shop Drawing.");

        var designProjectId = await db.DesignProjects.AsNoTracking()
            .Where(item => item.OperationalProjectId == projectId)
            .Select(item => (int?)item.Id)
            .SingleOrDefaultAsync(ct);
        if (!designProjectId.HasValue)
            throw new ProjectDocumentValidationException("Dự án vận hành chưa có Dự án Thiết kế liên kết.");

        if (request.SourceRecordId.Value > int.MaxValue)
            throw new ProjectDocumentValidationException("Mã hồ sơ nguồn Thiết kế không hợp lệ.");
        var recordId = (int)request.SourceRecordId.Value;
        var valid = sourceType switch
        {
            nameof(ConceptOption) => request.Category == ProjectDocumentCategory.DesignConcept &&
                await db.ConceptOptions.AsNoTracking().AnyAsync(item =>
                    item.Id == recordId && item.DesignProjectId == designProjectId.Value, ct),
            nameof(BasicDesignDoc) => request.Category == ProjectDocumentCategory.DesignBasic &&
                await db.BasicDesignDocs.AsNoTracking().AnyAsync(item =>
                    item.Id == recordId && item.DesignProjectId == designProjectId.Value, ct),
            nameof(ShopDrawing) => request.Category == ProjectDocumentCategory.DesignShopDrawing &&
                await db.ShopDrawings.AsNoTracking().AnyAsync(item =>
                    item.Id == recordId && item.DesignProjectId == designProjectId.Value, ct),
            _ => false,
        };
        if (!valid)
            throw new ProjectDocumentValidationException(
                "Hồ sơ nguồn không thuộc đúng Dự án hoặc category giai đoạn Thiết kế.");
    }

    private Task<ProjectDocument?> FindAsync(int projectId, long documentId, CancellationToken ct) =>
        db.ProjectDocuments.FirstOrDefaultAsync(document => document.Id == documentId &&
            document.OperationalProjectId == projectId && document.SyncStatus != ProjectDocumentSyncStatus.Deleted, ct);

    private static void ValidateCategory(ProjectDocumentCategory category, bool allowUnclassified)
    {
        if (!Enum.IsDefined(category) || !allowUnclassified && category == ProjectDocumentCategory.Unclassified)
            throw new ProjectDocumentValidationException("Danh mục tệp không hợp lệ; vui lòng chọn một trong chín danh mục dự án.");
    }

    private static ProjectDocument CreateDocument(int projectId, ProjectDocumentCategory category,
        ProjectDocumentSourceModule sourceModule, ProjectDocumentSourceType sourceType, long? sourceRecordId,
        int? customerId, int? contractId, StoredProjectDocument stored, ProjectDocumentOrigin origin, int? userId)
    {
        var now = DateTime.UtcNow;
        return new ProjectDocument
        {
            OperationalProjectId = projectId,
            Category = category,
            SourceModule = sourceModule,
            SourceType = sourceType,
            SourceRecordId = sourceRecordId,
            CustomerId = customerId,
            ContractId = contractId,
            LocalPath = stored.LocalPath,
            OriginalFileName = stored.OriginalFileName,
            ContentType = stored.ContentType,
            Size = stored.Size,
            Sha256 = stored.Sha256,
            Origin = origin,
            Generation = 1,
            DesiredOperation = ProjectDocumentDesiredOperation.Upsert,
            SyncStatus = ProjectDocumentSyncStatus.Pending,
            NextSyncAttemptAt = now,
            CreatedAt = now,
            CreatedByUserId = userId,
            UpdatedAt = now,
            UpdatedByUserId = userId,
        };
    }

    internal static ProjectDocumentResponse Map(ProjectDocument document) => new()
    {
        Id = document.Id,
        OperationalProjectId = document.OperationalProjectId,
        Category = document.Category.ToString(),
        SourceModule = document.SourceModule.ToString(),
        SourceType = document.SourceType.ToString(),
        SourceEntityType = document.SourceEntityType,
        SourceSlot = document.SourceSlot,
        SourceRecordId = document.SourceRecordId,
        CustomerId = document.CustomerId,
        ContractId = document.ContractId,
        OriginalFileName = document.OriginalFileName,
        ContentType = document.ContentType,
        Size = document.Size,
        Sha256 = document.Sha256,
        Origin = document.Origin.ToString(),
        Generation = document.Generation,
        DesiredOperation = document.DesiredOperation.ToString(),
        SyncStatus = document.SyncStatus.ToString(),
        SyncAttemptCount = document.SyncAttemptCount,
        MaxSyncAttempts = MaxSyncAttempts,
        SyncError = document.SyncError,
        NextSyncAttemptAt = document.NextSyncAttemptAt,
        DriveWebViewLink = document.DriveWebViewLink,
        DriveModifiedAt = document.DriveModifiedAt,
        IsDownloadable = document.IsDownloadable,
        UnsupportedReason = document.UnsupportedReason,
        ConflictState = document.ConflictState.ToString(),
        ConflictWithDocumentId = document.ConflictWithDocumentId,
        CreatedAt = document.CreatedAt,
        UpdatedAt = document.UpdatedAt,
    };
}
