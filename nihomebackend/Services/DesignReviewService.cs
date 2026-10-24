using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public sealed class DesignReviewOperationException(string message) : Exception(message);

public interface IDesignReviewService
{
    Task<IReadOnlyList<DesignReviewResponse>?> ListAsync(int projectId, int callerUserId, CancellationToken ct);
    Task<DesignReviewResponse> CreateAsync(int projectId, CreateDesignReviewRequest request, int callerUserId, CancellationToken ct);
    Task<DesignReviewResponse?> SubmitInternalAsync(int projectId, int reviewId, string rowVersion, int callerUserId, CancellationToken ct);
    Task<DesignReviewResponse?> AddCommentAsync(int projectId, int reviewId, AddDesignReviewCommentRequest request, int callerUserId, CancellationToken ct);
    Task<DesignReviewResponse?> ConsolidateAsync(int projectId, int reviewId, ConsolidateDesignReviewRequest request, int callerUserId, CancellationToken ct);
    Task<DesignReviewResponse?> SubmitClientAsync(int projectId, int reviewId, string rowVersion, int callerUserId, CancellationToken ct);
    Task<DesignReviewResponse?> RecordClientDecisionAsync(int projectId, int reviewId, RecordDesignClientDecisionRequest request, int callerUserId, CancellationToken ct);
    Task<int?> ResolveAccessibleDocumentProjectAsync(int projectId, int reviewId, long documentId, int callerUserId, CancellationToken ct);
    Task<IReadOnlyList<DesignReviewRecipientResponse>> ListRecipientCandidatesAsync(int projectId, int callerUserId, CancellationToken ct);
}

public sealed class DesignReviewService(
    AppDbContext db,
    IConceptDossierService dossier,
    IProjectAccessService access,
    INotificationService notifications,
    ILogger<DesignReviewService> logger) : IDesignReviewService
{
    private static readonly string[] AllowedRecipientRoles = ["SALE", "SALES_MANAGER", "PM", "BGD"];

    public async Task<IReadOnlyList<DesignReviewRecipientResponse>> ListRecipientCandidatesAsync(
        int projectId, int callerUserId, CancellationToken ct)
    {
        await RequireManagerAsync(projectId, callerUserId, ct);
        return await db.Users.AsNoTracking()
            .Where(item => item.IsActive && item.Id != callerUserId &&
                item.RoleEntity != null && AllowedRecipientRoles.Contains(item.RoleEntity.Code))
            .OrderBy(item => item.FullName).ThenBy(item => item.Email)
            .Select(item => new DesignReviewRecipientResponse
            {
                UserId = item.Id,
                Name = item.FullName ?? item.Email,
            }).ToListAsync(ct);
    }

    public async Task<int?> ResolveAccessibleDocumentProjectAsync(
        int projectId, int reviewId, long documentId, int callerUserId, CancellationToken ct)
    {
        var canManage = await CanManageAsync(projectId, callerUserId, ct);
        return await db.DesignReviews.AsNoTracking()
            .Where(review => review.Id == reviewId && review.DesignProjectId == projectId &&
                review.DesignProject.OperationalProjectId != null &&
                review.Documents.Any(document => document.ProjectDocumentId == documentId) &&
                (canManage || review.Recipients.Any(recipient => recipient.UserId == callerUserId)))
            .Select(review => review.DesignProject.OperationalProjectId)
            .SingleOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<DesignReviewResponse>?> ListAsync(
        int projectId, int callerUserId, CancellationToken ct)
    {
        if (!await CanViewProjectOrAssignedReviewAsync(projectId, callerUserId, ct)) return null;
        var canManage = await CanManageAsync(projectId, callerUserId, ct);
        var rows = await Query(projectId)
            .Where(item => canManage || item.Recipients.Any(recipient => recipient.UserId == callerUserId))
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(ct);
        return rows.Select(item => Map(item, callerUserId, canManage)).ToList();
    }

    public async Task<DesignReviewResponse> CreateAsync(
        int projectId, CreateDesignReviewRequest request, int callerUserId, CancellationToken ct)
    {
        var conceptOptionId = request.ConceptRevisionId.HasValue
            ? await db.ConceptRevisions.AsNoTracking().Where(item => item.Id == request.ConceptRevisionId.Value)
                .Select(item => (int?)item.ConceptOptionId).SingleOrDefaultAsync(ct)
            : null;
        await using var transaction = conceptOptionId.HasValue
            ? await ConceptDossierTransaction.BeginAsync(db, conceptOptionId.Value, ct) : null;
        var project = await RequireManagerAsync(projectId, callerUserId, ct);
        if (!Enum.TryParse<DesignReviewStage>(request.Stage?.Trim(), true, out var stage) || !Enum.IsDefined(stage))
            throw new DesignReviewOperationException("Giai đoạn trình duyệt không hợp lệ.");
        var title = request.Title?.Trim() ?? string.Empty;
        if (title.Length == 0) throw new DesignReviewOperationException("Tiêu đề hồ sơ trình là bắt buộc.");
        var recipientIds = request.RecipientUserIds.Distinct().ToList();
        var documentIds = request.ProjectDocumentIds.Distinct().ToList();
        if (recipientIds.Count == 0) throw new DesignReviewOperationException("Phải chỉ định ít nhất một người nhận nội bộ.");
        if (documentIds.Count == 0) throw new DesignReviewOperationException("Phải đính kèm ít nhất một tài liệu dự án.");
        await ValidateRecipientsAsync(recipientIds, callerUserId, ct);
        var documents = await ValidateDocumentsAsync(project, stage, documentIds, ct);
        if (stage == DesignReviewStage.Concept)
        {
            if (!request.ConceptRevisionId.HasValue)
                throw new DesignReviewOperationException("Hồ sơ Concept phải chọn revision của một phương án.");
            var optionId = await db.ConceptRevisions.AsNoTracking()
                .Where(item => item.Id == request.ConceptRevisionId.Value)
                .Select(item => (int?)item.ConceptOptionId).SingleOrDefaultAsync(ct);
            if (!optionId.HasValue || documents.Any(item => item.SourceRecordId != optionId.Value))
                throw new DesignReviewOperationException("Hồ sơ trình chỉ được chứa một phương án Concept đúng revision.");
            await dossier.ValidateForReviewAsync(request.ConceptRevisionId.Value, documentIds, ct);
        }
        else if (request.ConceptRevisionId.HasValue)
            throw new DesignReviewOperationException("Revision Concept chỉ áp dụng cho giai đoạn Concept.");

        var revision = await db.DesignReviews
            .Where(item => item.DesignProjectId == projectId && item.Stage == stage)
            .Select(item => (int?)item.RevisionNumber).MaxAsync(ct) ?? 0;
        var now = DateTime.UtcNow;
        var review = new DesignReview
        {
            DesignProjectId = projectId,
            Stage = stage,
            ConceptRevisionId = request.ConceptRevisionId,
            RevisionNumber = revision + 1,
            Title = title,
            DesignManagerUserId = callerUserId,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedByUserId = callerUserId,
            UpdatedByUserId = callerUserId,
            Recipients = recipientIds.Select(userId => new DesignReviewRecipient { UserId = userId }).ToList(),
            Documents = documents.Select(document => new DesignReviewDocument
            {
                ProjectDocumentId = document.Id,
                SourceEntityTypeSnapshot = document.SourceEntityType!,
                SourceRecordIdSnapshot = document.SourceRecordId!.Value,
                Sha256Snapshot = document.Sha256,
            }).ToList(),
        };
        db.DesignReviews.Add(review);
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return Map(await LoadAsync(projectId, review.Id, ct), callerUserId, true);
    }

    public async Task<DesignReviewResponse?> SubmitInternalAsync(
        int projectId, int reviewId, string rowVersion, int callerUserId, CancellationToken ct)
    {
        await using var transaction = await ConceptDossierTransaction.BeginForReviewAsync(db, projectId, reviewId, ct);
        var project = await RequireManagerAsync(projectId, callerUserId, ct);
        var review = await QueryTracked(projectId).SingleOrDefaultAsync(item => item.Id == reviewId, ct);
        if (review is null) return null;
        EnsureManager(review, project, callerUserId);
        if (review.Status != DesignReviewStatus.Draft)
            throw new DesignReviewOperationException("Chỉ hồ sơ nháp mới có thể trình nội bộ.");
        if (review.Recipients.Count == 0 || review.Documents.Count == 0)
            throw new DesignReviewOperationException("Hồ sơ trình phải có người nhận và tài liệu.");
        await EnsureStageTasksApprovedAsync(project, review.Stage, ct);
        await EnsureEvidenceUnchangedAsync(review, ct);
        if (review.ConceptRevisionId.HasValue)
        {
            await dossier.ValidateForReviewAsync(review.ConceptRevisionId.Value,
                review.Documents.Select(item => item.ProjectDocumentId).ToArray(), ct);
            var revision = await db.ConceptRevisions.SingleAsync(item => item.Id == review.ConceptRevisionId.Value, ct);
            revision.FrozenAt = DateTime.UtcNow;
            var option = await db.ConceptOptions.SingleAsync(item => item.Id == revision.ConceptOptionId, ct);
            option.Status = ConceptOptionStatus.PendingInternalReview;
            option.UpdatedAt = DateTime.UtcNow;
            option.UpdatedByUserId = callerUserId;
        }
        ApplyVersion(review, rowVersion);
        review.Status = DesignReviewStatus.InternalReview;
        review.InternalSubmittedAt = DateTime.UtcNow;
        Touch(review, callerUserId);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        await NotifyAsync(review.Recipients.Select(item => item.UserId), "design-review.internal", review, ct);
        return Map(await LoadAsync(projectId, reviewId, ct), callerUserId, true);
    }

    public async Task<DesignReviewResponse?> AddCommentAsync(
        int projectId, int reviewId, AddDesignReviewCommentRequest request, int callerUserId, CancellationToken ct)
    {
        var review = await QueryTracked(projectId).SingleOrDefaultAsync(item => item.Id == reviewId, ct);
        if (review is null) return null;
        var canManage = await CanManageAsync(projectId, callerUserId, ct);
        var isRecipient = review.Recipients.Any(item => item.UserId == callerUserId);
        if (!canManage && !isRecipient) return null;
        if (review.Status != DesignReviewStatus.InternalReview)
            throw new DesignReviewOperationException("Chỉ bình luận được khi hồ sơ đang trình nội bộ.");
        var body = request.Body?.Trim() ?? string.Empty;
        if (body.Length == 0) throw new DesignReviewOperationException("Nội dung bình luận là bắt buộc.");
        if (request.ProjectDocumentId.HasValue &&
            !review.Documents.Any(document => document.ProjectDocumentId == request.ProjectDocumentId.Value))
            throw new DesignReviewOperationException("Tài liệu bình luận phải thuộc hồ sơ trình nội bộ này.");
        review.Comments.Add(new DesignReviewComment
        {
            AuthorUserId = callerUserId,
            Body = body,
            ProjectDocumentId = request.ProjectDocumentId,
        });
        Touch(review, callerUserId);
        await db.SaveChangesAsync(ct);
        return Map(await LoadAsync(projectId, reviewId, ct), callerUserId, canManage);
    }

    public async Task<DesignReviewResponse?> ConsolidateAsync(
        int projectId, int reviewId, ConsolidateDesignReviewRequest request, int callerUserId, CancellationToken ct)
    {
        var project = await RequireManagerAsync(projectId, callerUserId, ct);
        var review = await QueryTracked(projectId).SingleOrDefaultAsync(item => item.Id == reviewId, ct);
        if (review is null) return null;
        EnsureManager(review, project, callerUserId);
        if (review.Status != DesignReviewStatus.InternalReview)
            throw new DesignReviewOperationException("Chỉ tổng hợp được hồ sơ đang trình nội bộ.");
        var summary = request.InternalSummary?.Trim() ?? string.Empty;
        if (summary.Length == 0) throw new DesignReviewOperationException("Nội dung tổng hợp ý kiến là bắt buộc.");
        await EnsureEvidenceUnchangedAsync(review, ct);
        ApplyVersion(review, request.RowVersion!);
        review.InternalSummary = summary;
        review.Status = DesignReviewStatus.ReadyForClient;
        Touch(review, callerUserId);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        return Map(await LoadAsync(projectId, reviewId, ct), callerUserId, true);
    }

    public async Task<DesignReviewResponse?> SubmitClientAsync(
        int projectId, int reviewId, string rowVersion, int callerUserId, CancellationToken ct)
    {
        await using var transaction = await ConceptDossierTransaction.BeginForReviewAsync(db, projectId, reviewId, ct);
        var project = await RequireManagerAsync(projectId, callerUserId, ct);
        var review = await QueryTracked(projectId).SingleOrDefaultAsync(item => item.Id == reviewId, ct);
        if (review is null) return null;
        EnsureManager(review, project, callerUserId);
        if (review.Status != DesignReviewStatus.ReadyForClient)
            throw new DesignReviewOperationException("Phải tổng hợp ý kiến nội bộ trước khi trình khách hàng.");
        await EnsureEvidenceUnchangedAsync(review, ct);
        ApplyVersion(review, rowVersion);
        review.Status = DesignReviewStatus.SubmittedToClient;
        review.ClientSubmittedAt = DateTime.UtcNow;
        if (review.ConceptRevisionId.HasValue)
        {
            var option = await db.ConceptOptions.SingleAsync(item =>
                db.ConceptRevisions.Any(revision => revision.Id == review.ConceptRevisionId &&
                    revision.ConceptOptionId == item.Id), ct);
            option.Status = ConceptOptionStatus.PresentedToClient;
            option.PresentedAt = review.ClientSubmittedAt;
            option.UpdatedAt = DateTime.UtcNow;
            option.UpdatedByUserId = callerUserId;
        }
        Touch(review, callerUserId);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return Map(await LoadAsync(projectId, reviewId, ct), callerUserId, true);
    }

    public async Task<DesignReviewResponse?> RecordClientDecisionAsync(
        int projectId, int reviewId, RecordDesignClientDecisionRequest request, int callerUserId, CancellationToken ct)
    {
        await using var transaction = await ConceptDossierTransaction.BeginForReviewAsync(db, projectId, reviewId, ct);
        var project = await RequireManagerAsync(projectId, callerUserId, ct);
        var review = await QueryTracked(projectId).SingleOrDefaultAsync(item => item.Id == reviewId, ct);
        if (review is null) return null;
        EnsureManager(review, project, callerUserId);
        if (review.Status != DesignReviewStatus.SubmittedToClient)
            throw new DesignReviewOperationException("Chỉ ghi nhận quyết định khi hồ sơ đã trình khách hàng.");
        await EnsureEvidenceUnchangedAsync(review, ct);
        if (!Enum.TryParse<DesignReviewStatus>(request.Decision?.Trim(), true, out var decision) ||
            decision is not (DesignReviewStatus.ClientApproved or DesignReviewStatus.ClientChangesRequested or DesignReviewStatus.Rejected))
            throw new DesignReviewOperationException("Quyết định hợp lệ: ClientApproved, ClientChangesRequested hoặc Rejected.");
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (decision != DesignReviewStatus.ClientApproved && note is null)
            throw new DesignReviewOperationException("Phải ghi lý do khi khách hàng yêu cầu sửa hoặc từ chối.");
        if (review.Stage == DesignReviewStage.Concept && decision == DesignReviewStatus.ClientApproved)
        {
            if (!review.ConceptRevisionId.HasValue || !request.ClientAcceptanceDocumentId.HasValue)
                throw new DesignReviewOperationException("Duyệt Concept cần revision và biên bản TK-M03 đã ký.");
            var signer = request.ClientAcceptanceSignedBy?.Trim() ?? string.Empty;
            if (signer.Length == 0 || request.ClientAcceptanceSignedAt is null ||
                request.ClientAcceptanceSignedAt.Value == default ||
                request.ClientAcceptanceSignedAt.Value > DateTime.UtcNow.AddMinutes(5))
                throw new DesignReviewOperationException("Biên bản TK-M03 cần tên người ký và ngày ký hợp lệ.");
            var optionId = await db.ConceptRevisions.AsNoTracking()
                .Where(item => item.Id == review.ConceptRevisionId.Value)
                .Select(item => item.ConceptOptionId).SingleAsync(ct);
            var acceptance = await db.ProjectDocuments.AsNoTracking().SingleOrDefaultAsync(item =>
                item.Id == request.ClientAcceptanceDocumentId &&
                item.OperationalProjectId == project.OperationalProjectId &&
                item.Category == ProjectDocumentCategory.DesignConcept &&
                item.SourceModule == ProjectDocumentSourceModule.Design &&
                item.SourceEntityType == nameof(ConceptOption) && item.SourceRecordId == optionId &&
                item.DeletedAt == null && item.SyncStatus == ProjectDocumentSyncStatus.Synced &&
                (item.ContentType == "application/pdf" || item.ContentType == "image/jpeg" ||
                 item.ContentType == "image/png"), ct);
            if (acceptance is null)
                throw new DesignReviewOperationException("Biên bản TK-M03 phải là PDF hoặc ảnh đã đồng bộ, thuộc đúng phương án.");
            if (review.Documents.Any(item => item.ProjectDocumentId == acceptance.Id))
                throw new DesignReviewOperationException("Biên bản TK-M03 phải là file nghiệm thu riêng, không phải file phương án đã trình.");
            review.ClientAcceptanceDocumentId = acceptance.Id;
            review.ClientAcceptanceSignedBy = signer;
            review.ClientAcceptanceSignedAt = request.ClientAcceptanceSignedAt.Value.ToUniversalTime();
        }
        ApplyVersion(review, request.RowVersion!);
        review.Status = decision;
        review.ClientDecisionNote = note;
        review.ClientDecidedAt = DateTime.UtcNow;
        review.ClientDecisionRecordedByUserId = callerUserId;
        if (review.ConceptRevisionId.HasValue && decision == DesignReviewStatus.ClientChangesRequested)
        {
            var optionId = await db.ConceptRevisions.AsNoTracking()
                .Where(item => item.Id == review.ConceptRevisionId.Value)
                .Select(item => item.ConceptOptionId).SingleAsync(ct);
            var option = await db.ConceptOptions.SingleAsync(item => item.Id == optionId, ct);
            option.Status = ConceptOptionStatus.ClientRequestedChanges;
            option.UpdatedAt = DateTime.UtcNow;
            option.UpdatedByUserId = callerUserId;
        }
        Touch(review, callerUserId);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        await NotifyAsync(review.Recipients.Select(item => item.UserId), "design-review.client-decision", review, ct);
        return Map(await LoadAsync(projectId, reviewId, ct), callerUserId, true);
    }

    private async Task<DesignProject> RequireManagerAsync(int projectId, int callerUserId, CancellationToken ct)
    {
        var project = await db.DesignProjects.AsNoTracking().SingleOrDefaultAsync(item => item.Id == projectId, ct)
            ?? throw new DesignReviewOperationException("Dự án thiết kế không tồn tại.");
        if (project.ProjectManagerUserId != callerUserId && !await HasReviewAdministrativeBypassAsync(callerUserId, ct))
            throw new DesignReviewOperationException("Chỉ Chủ nhiệm thiết kế được quản lý hồ sơ trình.");
        if (!project.OperationalProjectId.HasValue)
            throw new DesignReviewOperationException("Dự án thiết kế chưa liên kết Dự án vận hành.");
        return project;
    }

    private async Task<bool> CanManageAsync(int projectId, int callerUserId, CancellationToken ct) =>
        await db.DesignProjects.AsNoTracking().AnyAsync(item => item.Id == projectId && item.ProjectManagerUserId == callerUserId, ct) ||
        await HasReviewAdministrativeBypassAsync(callerUserId, ct);

    private Task<bool> HasReviewAdministrativeBypassAsync(int userId, CancellationToken ct) =>
        db.Users.AsNoTracking().AnyAsync(user => user.Id == userId && user.IsActive &&
            (user.Role == UserRole.SUPER_ADMIN || user.Role == UserRole.ADMIN ||
             user.RoleEntity != null &&
             (user.RoleEntity.Code == "SUPER_ADMIN" || user.RoleEntity.Code == "ADMIN")), ct);

    private async Task<bool> CanViewProjectOrAssignedReviewAsync(int projectId, int callerUserId, CancellationToken ct) =>
        await access.CanViewDesignProjectAsync(callerUserId, projectId, ct) ||
        await db.DesignReviewRecipients.AsNoTracking().AnyAsync(item =>
            item.DesignReview.DesignProjectId == projectId && item.UserId == callerUserId, ct);

    private async Task ValidateRecipientsAsync(IReadOnlyCollection<int> ids, int managerId, CancellationToken ct)
    {
        if (ids.Contains(managerId)) throw new DesignReviewOperationException("Chủ nhiệm thiết kế không cần nằm trong danh sách người nhận.");
        var count = await db.Users.AsNoTracking().CountAsync(item =>
            ids.Contains(item.Id) && item.IsActive &&
            item.RoleEntity != null && AllowedRecipientRoles.Contains(item.RoleEntity.Code), ct);
        if (count != ids.Count) throw new DesignReviewOperationException("Danh sách người nhận chứa người dùng không tồn tại hoặc ngừng hoạt động.");
    }

    private async Task<List<ProjectDocument>> ValidateDocumentsAsync(
        DesignProject project, DesignReviewStage stage, IReadOnlyCollection<long> ids, CancellationToken ct)
    {
        var category = stage switch
        {
            DesignReviewStage.Concept => ProjectDocumentCategory.DesignConcept,
            DesignReviewStage.BasicDesign => ProjectDocumentCategory.DesignBasic,
            _ => ProjectDocumentCategory.DesignShopDrawing,
        };
        var sourceType = stage switch
        {
            DesignReviewStage.Concept => nameof(ConceptOption),
            DesignReviewStage.BasicDesign => nameof(BasicDesignDoc),
            _ => nameof(ShopDrawing),
        };
        var documents = await db.ProjectDocuments.AsNoTracking().Where(item =>
            ids.Contains(item.Id) && item.OperationalProjectId == project.OperationalProjectId &&
            item.Category == category && item.SourceModule == ProjectDocumentSourceModule.Design &&
            item.SourceEntityType == sourceType && item.SourceRecordId != null &&
            item.DeletedAt == null && item.SyncStatus == ProjectDocumentSyncStatus.Synced &&
            item.Sha256.Length == 64).ToListAsync(ct);
        if (documents.Count != ids.Count)
            throw new DesignReviewOperationException(
                "Tài liệu trình phải đã đồng bộ Google Drive và gắn đúng hồ sơ nguồn của Dự án/giai đoạn Thiết kế.");

        var recordIds = documents.Select(item => (int)item.SourceRecordId!.Value).Distinct().ToList();
        var validRecordCount = stage switch
        {
            DesignReviewStage.Concept => await db.ConceptOptions.AsNoTracking().CountAsync(item =>
                recordIds.Contains(item.Id) && item.DesignProjectId == project.Id, ct),
            DesignReviewStage.BasicDesign => await db.BasicDesignDocs.AsNoTracking().CountAsync(item =>
                recordIds.Contains(item.Id) && item.DesignProjectId == project.Id, ct),
            _ => await db.ShopDrawings.AsNoTracking().CountAsync(item =>
                recordIds.Contains(item.Id) && item.DesignProjectId == project.Id, ct),
        };
        if (validRecordCount != recordIds.Count)
            throw new DesignReviewOperationException("Hồ sơ nguồn của tài liệu trình không thuộc Dự án Thiết kế.");
        return documents;
    }

    private IQueryable<DesignReview> Query(int projectId) => db.DesignReviews.AsNoTracking()
        .Where(item => item.DesignProjectId == projectId)
        .Include(item => item.DesignManager)
        .Include(item => item.ConceptRevision)
        .Include(item => item.Recipients).ThenInclude(item => item.User)
        .Include(item => item.Comments).ThenInclude(item => item.Author)
        .Include(item => item.Documents).ThenInclude(item => item.ProjectDocument);

    private IQueryable<DesignReview> QueryTracked(int projectId) => db.DesignReviews
        .Where(item => item.DesignProjectId == projectId)
        .Include(item => item.Recipients)
        .Include(item => item.ConceptRevision)
        .Include(item => item.Comments)
        .Include(item => item.Documents).ThenInclude(item => item.ProjectDocument);

    private Task<DesignReview> LoadAsync(int projectId, int reviewId, CancellationToken ct) =>
        Query(projectId).SingleAsync(item => item.Id == reviewId, ct);

    private static void EnsureManager(DesignReview review, DesignProject project, int callerUserId)
    {
        if (project.ProjectManagerUserId == callerUserId && review.DesignManagerUserId != callerUserId)
            review.DesignManagerUserId = callerUserId;
    }

    private async Task EnsureStageTasksApprovedAsync(
        DesignProject project, DesignReviewStage stage, CancellationToken ct)
    {
        var phaseCode = stage switch
        {
            DesignReviewStage.Concept => DesignSchedulePhaseCode.Concept,
            DesignReviewStage.BasicDesign => DesignSchedulePhaseCode.BasicDesign,
            _ => DesignSchedulePhaseCode.ShopDrawing,
        };
        var tasks = db.DesignScheduleTasks.AsNoTracking().Where(item =>
            item.DesignProjectId == project.Id && item.Phase.Code == phaseCode);
        if (!await tasks.AnyAsync(ct) || await tasks.AnyAsync(item =>
                item.Status != DesignScheduleStatus.Completed ||
                item.ApprovalStatus != DesignTaskApprovalStatus.Approved, ct))
            throw new DesignReviewOperationException(
                "Mọi đầu việc của giai đoạn phải hoàn thành và được Design Lead duyệt trước khi trình nội bộ.");
    }

    private async Task EnsureEvidenceUnchangedAsync(DesignReview review, CancellationToken ct)
    {
        var documentIds = review.Documents.Select(item => item.ProjectDocumentId).ToList();
        var current = await db.ProjectDocuments.AsNoTracking().Where(item => documentIds.Contains(item.Id))
            .Select(item => new
            {
                item.Id,
                item.SourceEntityType,
                item.SourceRecordId,
                item.Sha256,
                item.SyncStatus,
                item.DeletedAt,
            }).ToListAsync(ct);
        var unchanged = current.Count == review.Documents.Count && review.Documents.All(snapshot =>
        {
            var document = current.SingleOrDefault(item => item.Id == snapshot.ProjectDocumentId);
            return document is not null && document.DeletedAt == null &&
                document.SyncStatus == ProjectDocumentSyncStatus.Synced &&
                document.SourceEntityType == snapshot.SourceEntityTypeSnapshot &&
                document.SourceRecordId == snapshot.SourceRecordIdSnapshot &&
                document.Sha256 == snapshot.Sha256Snapshot;
        });
        if (!unchanged)
            throw new DesignReviewOperationException(
                "Tài liệu trình đã thay đổi, bị xóa hoặc chưa đồng bộ; hãy tạo revision hồ sơ trình mới.");
    }

    private void ApplyVersion(DesignReview review, string version) =>
        CrmConcurrency.Apply(db, review, version);

    private static void Touch(DesignReview review, int userId)
    {
        review.UpdatedAt = DateTime.UtcNow;
        review.UpdatedByUserId = userId;
    }

    private async Task NotifyAsync(IEnumerable<int> userIds, string template, DesignReview review, CancellationToken ct)
    {
        try
        {
            await notifications.NotifyManyFromTemplateAsync(userIds, template,
                new Dictionary<string, string>
                {
                    ["title"] = review.Title,
                    ["stage"] = review.Stage.ToString(),
                    ["status"] = review.Status.ToString(),
                }, "DesignReview", review.Id, $"/admin/design-reviews/{review.DesignProjectId}?reviewId={review.Id}");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to notify design review {ReviewId}", review.Id);
        }
    }

    private static DesignReviewResponse Map(DesignReview item, int callerUserId, bool canManage) => new()
    {
        Id = item.Id,
        DesignProjectId = item.DesignProjectId,
        Stage = item.Stage.ToString(),
        RevisionNumber = item.RevisionNumber,
        Title = item.Title,
        Status = item.Status.ToString(),
        DesignManagerUserId = item.DesignManagerUserId,
        DesignManagerName = item.DesignManager?.FullName ?? item.DesignManager?.Email,
        InternalSummary = item.InternalSummary,
        InternalSubmittedAt = item.InternalSubmittedAt,
        ClientSubmittedAt = item.ClientSubmittedAt,
        ClientDecisionNote = item.ClientDecisionNote,
        ClientDecidedAt = item.ClientDecidedAt,
        ConceptRevisionId = item.ConceptRevisionId,
        ConceptOptionId = item.ConceptRevision?.ConceptOptionId,
        ClientAcceptanceDocumentId = item.ClientAcceptanceDocumentId,
        ClientAcceptanceSignedBy = item.ClientAcceptanceSignedBy,
        ClientAcceptanceSignedAt = item.ClientAcceptanceSignedAt.HasValue
            ? DateTime.SpecifyKind(item.ClientAcceptanceSignedAt.Value, DateTimeKind.Utc) : null,
        CanManage = canManage,
        CanComment = item.Status == DesignReviewStatus.InternalReview &&
            (canManage || item.Recipients.Any(recipient => recipient.UserId == callerUserId)),
        Recipients = item.Recipients.Select(recipient => new DesignReviewRecipientResponse
        {
            UserId = recipient.UserId,
            Name = recipient.User.FullName ?? recipient.User.Email,
        }).ToList(),
        Comments = item.Comments.OrderBy(comment => comment.CreatedAt).Select(comment => new DesignReviewCommentResponse
        {
            Id = comment.Id,
            ProjectDocumentId = comment.ProjectDocumentId,
            AuthorUserId = comment.AuthorUserId,
            AuthorName = comment.Author.FullName ?? comment.Author.Email,
            Body = comment.Body,
            CreatedAt = comment.CreatedAt,
        }).ToList(),
        ProjectDocumentIds = item.Documents.Select(document => document.ProjectDocumentId).ToList(),
        Documents = item.Documents.Select(document => new DesignReviewDocumentResponse
        {
            ProjectDocumentId = document.ProjectDocumentId,
            OriginalFileName = document.ProjectDocument.OriginalFileName,
            SourceEntityType = document.SourceEntityTypeSnapshot,
            SourceRecordId = document.SourceRecordIdSnapshot,
            Sha256 = document.Sha256Snapshot,
        }).ToList(),
        RowVersion = Convert.ToBase64String(item.RowVersion),
    };
}
