using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public interface IConceptDossierService
{
    Task ValidateForReviewAsync(int revisionId, IReadOnlyCollection<long> documentIds, CancellationToken ct);
    Task<IReadOnlyList<ConceptRevisionResponse>> ListAsync(int optionId, CancellationToken ct);
    Task<ConceptRevisionResponse> CreateRevisionAsync(int optionId, int userId, CancellationToken ct);
    Task<ConceptRevisionResponse> AddArtifactAsync(int optionId, int revisionId, AddConceptArtifactRequest request, int userId, CancellationToken ct);
    Task<ConceptRevisionResponse> RemoveArtifactAsync(int optionId, int revisionId, int artifactId, int userId, CancellationToken ct);
    Task<ConceptRevisionResponse> WaiveAsync(int optionId, int revisionId, WaiveConceptChecklistRequest request, int userId, CancellationToken ct);
    Task<ConceptRevisionResponse> RemoveWaiverAsync(int optionId, int revisionId, ConceptArtifactKind kind, int userId, CancellationToken ct);
    Task<ConceptRevisionResponse> AddFeedbackAsync(int optionId, int revisionId, AddConceptFeedbackRequest request, int userId, CancellationToken ct);
    Task<ConceptRevisionResponse> RespondAsync(int optionId, int revisionId, int feedbackId, RespondConceptFeedbackRequest request, int userId, CancellationToken ct);
}

public sealed class ConceptDossierService(AppDbContext db) : IConceptDossierService
{
    private static readonly ConceptArtifactKind[] RequiredKinds =
    [
        ConceptArtifactKind.Perspective,
        ConceptArtifactKind.SiteRoofPlan,
        ConceptArtifactKind.FloorPlan,
        ConceptArtifactKind.Section,
        ConceptArtifactKind.Presentation,
    ];

    public async Task<IReadOnlyList<ConceptRevisionResponse>> ListAsync(int optionId, CancellationToken ct) =>
        (await Query().Where(item => item.ConceptOptionId == optionId)
            .OrderByDescending(item => item.Number).ToListAsync(ct)).Select(Map).ToList();

    public async Task<ConceptRevisionResponse> CreateRevisionAsync(int optionId, int userId, CancellationToken ct)
    {
        var option = await RequireManagerAsync(optionId, userId, ct);
        if (option.Status is ConceptOptionStatus.Finalized or ConceptOptionStatus.Discarded)
            throw new ConceptOptionOperationException("Không thể tạo revision cho phương án đã chốt hoặc loại bỏ.");
        var last = await db.ConceptRevisions.Where(item => item.ConceptOptionId == optionId)
            .OrderByDescending(item => item.Number).FirstOrDefaultAsync(ct);
        if (last is null || last.FrozenAt is null)
            throw new ConceptOptionOperationException("Revision hiện tại còn là bản nháp.");
        var lastReview = await db.DesignReviews.AsNoTracking()
            .Where(item => item.ConceptRevisionId == last.Id)
            .OrderByDescending(item => item.RevisionNumber)
            .Select(item => new { item.Status, item.ClientAcceptanceDocumentId })
            .FirstOrDefaultAsync(ct);
        if (lastReview is null || lastReview.Status is not
            (DesignReviewStatus.ClientChangesRequested or DesignReviewStatus.Rejected) &&
            !(lastReview.Status == DesignReviewStatus.ClientApproved &&
              lastReview.ClientAcceptanceDocumentId is null))
            throw new ConceptOptionOperationException("Chỉ tạo revision mới sau khi khách yêu cầu sửa hoặc từ chối.");
        var next = new ConceptRevision
        {
            ConceptOptionId = optionId,
            Number = last.Number + 1,
            CreatedByUserId = userId,
        };
        db.ConceptRevisions.Add(next);
        option.Status = ConceptOptionStatus.Drafting;
        option.UpdatedAt = DateTime.UtcNow;
        option.UpdatedByUserId = userId;
        await db.SaveChangesAsync(ct);
        return Map(await Query().SingleAsync(item => item.Id == next.Id, ct));
    }

    public async Task<ConceptRevisionResponse> AddArtifactAsync(
        int optionId, int revisionId, AddConceptArtifactRequest request, int userId, CancellationToken ct)
    {
        var (option, revision) = await RequireEditableAsync(optionId, revisionId, userId, ct);
        if (!Enum.TryParse<ConceptArtifactKind>(request.Kind, true, out var kind) || !Enum.IsDefined(kind))
            throw new ConceptOptionOperationException("Loại tài liệu Concept không hợp lệ.");
        var floor = request.FloorLabel?.Trim();
        if (kind == ConceptArtifactKind.FloorPlan && string.IsNullOrWhiteSpace(floor))
            throw new ConceptOptionOperationException("Bản vẽ mặt bằng phải có tên tầng.");
        if (kind != ConceptArtifactKind.FloorPlan && !string.IsNullOrWhiteSpace(floor))
            throw new ConceptOptionOperationException("Tên tầng chỉ dùng cho bản vẽ mặt bằng.");
        var document = await db.ProjectDocuments.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == request.ProjectDocumentId && item.OperationalProjectId == option.DesignProject.OperationalProjectId &&
            item.Category == ProjectDocumentCategory.DesignConcept &&
            item.SourceModule == ProjectDocumentSourceModule.Design &&
            item.SourceEntityType == nameof(ConceptOption) && item.SourceRecordId == optionId &&
            item.DeletedAt == null, ct);
        if (document is null) throw new ConceptOptionOperationException("File phải thuộc đúng phương án Concept của Dự án.");
        var isImage = document.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
        var isPdf = string.Equals(document.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase);
        if (kind == ConceptArtifactKind.Presentation && !isPdf ||
            kind == ConceptArtifactKind.Perspective && !isImage ||
            (kind is ConceptArtifactKind.SiteRoofPlan or ConceptArtifactKind.FloorPlan or ConceptArtifactKind.Section) &&
            !isImage && !isPdf ||
            kind == ConceptArtifactKind.Model3D && (isImage || isPdf))
            throw new ConceptOptionOperationException("Định dạng file không phù hợp với loại tài liệu Concept.");
        if (await db.ConceptArtifacts.AnyAsync(item => item.ProjectDocumentId == document.Id, ct))
            throw new ConceptOptionOperationException("File đã được gắn vào một revision Concept.");
        if (await db.ConceptChecklistWaivers.AnyAsync(item => item.ConceptRevisionId == revisionId && item.Kind == kind, ct))
            throw new ConceptOptionOperationException("Hạng mục đã được ghi không áp dụng; không thể gắn file cùng loại.");
        revision.Artifacts.Add(new ConceptArtifact
        {
            ProjectDocumentId = document.Id,
            Kind = kind,
            FloorLabel = floor,
            CreatedByUserId = userId,
        });
        await db.SaveChangesAsync(ct);
        return Map(await Query().SingleAsync(item => item.Id == revisionId, ct));
    }

    public async Task<ConceptRevisionResponse> WaiveAsync(
        int optionId, int revisionId, WaiveConceptChecklistRequest request, int userId, CancellationToken ct)
    {
        var (_, revision) = await RequireEditableAsync(optionId, revisionId, userId, ct);
        if (!Enum.TryParse<ConceptArtifactKind>(request.Kind, true, out var kind) ||
            !RequiredKinds.Contains(kind))
            throw new ConceptOptionOperationException("Hạng mục checklist không hợp lệ.");
        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length < 10)
            throw new ConceptOptionOperationException("Lý do không áp dụng phải có ít nhất 10 ký tự.");
        if (await db.ConceptChecklistWaivers.AnyAsync(item => item.ConceptRevisionId == revisionId && item.Kind == kind, ct))
            throw new ConceptOptionOperationException("Hạng mục này đã có lý do không áp dụng.");
        if (await db.ConceptArtifacts.AnyAsync(item => item.ConceptRevisionId == revisionId && item.Kind == kind, ct))
            throw new ConceptOptionOperationException("Hạng mục này đã có tài liệu, không thể đánh dấu không áp dụng.");
        revision.Waivers.Add(new ConceptChecklistWaiver
        {
            Kind = kind, Reason = reason, CreatedByUserId = userId,
        });
        await db.SaveChangesAsync(ct);
        return Map(await Query().SingleAsync(item => item.Id == revisionId, ct));
    }

    public async Task<ConceptRevisionResponse> RemoveArtifactAsync(
        int optionId, int revisionId, int artifactId, int userId, CancellationToken ct)
    {
        await RequireEditableAsync(optionId, revisionId, userId, ct);
        var artifact = await db.ConceptArtifacts.SingleOrDefaultAsync(item =>
            item.Id == artifactId && item.ConceptRevisionId == revisionId, ct)
            ?? throw new ConceptOptionOperationException("File không thuộc revision Concept này.");
        db.ConceptArtifacts.Remove(artifact);
        await db.SaveChangesAsync(ct);
        return Map(await Query().SingleAsync(item => item.Id == revisionId, ct));
    }

    public async Task<ConceptRevisionResponse> RemoveWaiverAsync(
        int optionId, int revisionId, ConceptArtifactKind kind, int userId, CancellationToken ct)
    {
        await RequireEditableAsync(optionId, revisionId, userId, ct);
        var waiver = await db.ConceptChecklistWaivers.SingleOrDefaultAsync(item =>
            item.ConceptRevisionId == revisionId && item.Kind == kind, ct)
            ?? throw new ConceptOptionOperationException("Ngoại lệ không thuộc revision Concept này.");
        db.ConceptChecklistWaivers.Remove(waiver);
        await db.SaveChangesAsync(ct);
        return Map(await Query().SingleAsync(item => item.Id == revisionId, ct));
    }

    public async Task<ConceptRevisionResponse> AddFeedbackAsync(
        int optionId, int revisionId, AddConceptFeedbackRequest request, int userId, CancellationToken ct)
    {
        await RequireManagerAsync(optionId, userId, ct);
        var revision = await db.ConceptRevisions.SingleOrDefaultAsync(item =>
            item.Id == revisionId && item.ConceptOptionId == optionId, ct)
            ?? throw new ConceptOptionOperationException("Revision Concept không tồn tại.");
        if (!await db.DesignReviews.AnyAsync(item => item.ConceptRevisionId == revisionId && item.ClientSubmittedAt != null, ct))
            throw new ConceptOptionOperationException("Chỉ ghi nhận ý kiến khách sau khi trình revision này.");
        if (request.ProjectDocumentId.HasValue &&
            !await db.ConceptArtifacts.AnyAsync(item => item.ConceptRevisionId == revisionId &&
                item.ProjectDocumentId == request.ProjectDocumentId, ct))
            throw new ConceptOptionOperationException("File góp ý phải thuộc revision này.");
        var name = request.CustomerName?.Trim() ?? string.Empty;
        var channel = request.Channel?.Trim() ?? string.Empty;
        var comment = request.Comment ?? string.Empty;
        var evidenceSource = request.EvidenceSource?.Trim() ?? string.Empty;
        if (name.Length == 0 || channel.Length == 0 || string.IsNullOrWhiteSpace(comment) ||
            evidenceSource.Length == 0 || evidenceSource.Length > 1000 ||
            request.OccurredAt == default || request.OccurredAt > DateTime.UtcNow.AddMinutes(5))
            throw new ConceptOptionOperationException("Ý kiến khách cần tên, kênh, thời điểm hợp lệ, nội dung và nguồn bằng chứng (ví dụ: biên bản họp).");
        revision.Feedback.Add(new ConceptFeedback
        {
            CustomerName = name, Channel = channel, Comment = comment, EvidenceSource = evidenceSource,
            OccurredAt = request.OccurredAt.ToUniversalTime(),
            ProjectDocumentId = request.ProjectDocumentId,
            CreatedByUserId = userId,
        });
        await db.SaveChangesAsync(ct);
        return Map(await Query().SingleAsync(item => item.Id == revisionId, ct));
    }

    public async Task<ConceptRevisionResponse> RespondAsync(
        int optionId, int revisionId, int feedbackId, RespondConceptFeedbackRequest request, int userId, CancellationToken ct)
    {
        await RequireManagerAsync(optionId, userId, ct);
        var feedback = await db.ConceptFeedback.SingleOrDefaultAsync(item => item.Id == feedbackId &&
            item.ConceptRevisionId == revisionId && item.ConceptRevision.ConceptOptionId == optionId, ct)
            ?? throw new ConceptOptionOperationException("Ý kiến khách không tồn tại.");
        if (feedback.Response is not null)
            throw new ConceptOptionOperationException("Phản hồi đã ghi nhận không thể ghi đè.");
        var response = request.Response?.Trim() ?? string.Empty;
        if (response.Length == 0) throw new ConceptOptionOperationException("Nội dung phản hồi là bắt buộc.");
        var outcome = request.Outcome?.Trim() ?? string.Empty;
        if (outcome.Length == 0 || outcome.Length > 2000)
            throw new ConceptOptionOperationException("Kết quả xử lý là bắt buộc, tối đa 2000 ký tự.");
        feedback.Response = response;
        feedback.Outcome = outcome;
        feedback.RespondedAt = DateTime.UtcNow;
        feedback.RespondedByUserId = userId;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConceptOptionOperationException("Phản hồi đã được ghi bởi phiên làm việc khác.");
        }
        return Map(await Query().SingleAsync(item => item.Id == revisionId, ct));
    }

    public async Task ValidateForReviewAsync(int revisionId, IReadOnlyCollection<long> documentIds, CancellationToken ct)
    {
        var revision = await Query().SingleOrDefaultAsync(item => item.Id == revisionId, ct)
            ?? throw new DesignReviewOperationException("Revision Concept không tồn tại.");
        var option = await db.ConceptOptions.AsNoTracking().Where(item => item.Id == revision.ConceptOptionId)
            .Select(item => new { item.Status, item.DesignProject.CurrentStage })
            .SingleAsync(ct);
        if (option.CurrentStage != DesignProjectStage.Concept ||
            option.Status is ConceptOptionStatus.Finalized or ConceptOptionStatus.Discarded ||
            await db.ConceptRevisions.AnyAsync(item => item.ConceptOptionId == revision.ConceptOptionId &&
                item.Number > revision.Number, ct))
            throw new DesignReviewOperationException("Chỉ revision Concept mới nhất của phương án đang mở được trình.");
        if (revision.FrozenAt is not null)
            throw new DesignReviewOperationException("Revision Concept đã được trình nội bộ.");
        if (revision.Number > 0 && await db.ConceptFeedback.AnyAsync(item =>
            item.ConceptRevision.ConceptOptionId == revision.ConceptOptionId &&
            item.ConceptRevision.Number < revision.Number && item.Response == null, ct))
            throw new DesignReviewOperationException("Phải ghi phản hồi cho mọi ý kiến khách trước khi trình revision mới.");
        var kinds = revision.Artifacts.Select(item => item.Kind).ToHashSet();
        var missing = RequiredKinds.Where(kind => !kinds.Contains(kind) &&
            !revision.Waivers.Any(waiver => waiver.Kind == kind)).ToList();
        if (missing.Count > 0)
            throw new DesignReviewOperationException($"Hồ sơ Concept thiếu: {string.Join(", ", missing)}.");
        if (!revision.Artifacts.Any(item => item.Kind is ConceptArtifactKind.Model3D or ConceptArtifactKind.Perspective))
            throw new DesignReviewOperationException("Hồ sơ Concept cần mô hình 3D hoặc hình phối cảnh.");
        if (revision.Artifacts.Any(item => item.ProjectDocument.DeletedAt != null ||
            item.ProjectDocument.SyncStatus != ProjectDocumentSyncStatus.Synced))
            throw new DesignReviewOperationException("Mọi file Concept phải còn hiệu lực và đồng bộ Drive trước khi trình.");
        var expected = revision.Artifacts.Select(item => item.ProjectDocumentId).OrderBy(id => id).ToArray();
        if (!expected.SequenceEqual(documentIds.OrderBy(id => id)))
            throw new DesignReviewOperationException("Hồ sơ trình phải chứa đúng các file của revision Concept.");
    }

    private async Task<ConceptOption> RequireManagerAsync(int optionId, int userId, CancellationToken ct)
    {
        var option = await db.ConceptOptions.Include(item => item.DesignProject)
            .SingleOrDefaultAsync(item => item.Id == optionId, ct)
            ?? throw new ConceptOptionOperationException("Phương án Concept không tồn tại.");
        if (option.DesignProject.ProjectManagerUserId != userId &&
            !await db.Users.AsNoTracking().AnyAsync(item => item.Id == userId && item.IsActive &&
                (item.Role == UserRole.SUPER_ADMIN || item.Role == UserRole.ADMIN ||
                 item.RoleEntity != null &&
                 (item.RoleEntity.Code == "SUPER_ADMIN" || item.RoleEntity.Code == "ADMIN")), ct))
            throw new ConceptOptionOperationException("Chỉ Chủ nhiệm thiết kế được quản lý hồ sơ Concept.");
        return option;
    }

    private async Task<(ConceptOption Option, ConceptRevision Revision)> RequireEditableAsync(
        int optionId, int revisionId, int userId, CancellationToken ct)
    {
        var option = await RequireManagerAsync(optionId, userId, ct);
        if (option.Status is ConceptOptionStatus.Finalized or ConceptOptionStatus.Discarded ||
            option.DesignProject.CurrentStage != DesignProjectStage.Concept)
            throw new ConceptOptionOperationException("Giai đoạn Concept đã khóa.");
        var revision = await db.ConceptRevisions.SingleOrDefaultAsync(item => item.Id == revisionId &&
            item.ConceptOptionId == optionId, ct)
            ?? throw new ConceptOptionOperationException("Revision Concept không tồn tại.");
        if (revision.FrozenAt is not null || await db.ConceptRevisions.AnyAsync(item =>
            item.ConceptOptionId == optionId && item.Number > revision.Number, ct))
            throw new ConceptOptionOperationException("Chỉ revision nháp mới nhất được chỉnh sửa.");
        return (option, revision);
    }

    private IQueryable<ConceptRevision> Query() => db.ConceptRevisions.AsNoTracking()
        .Include(item => item.Artifacts).ThenInclude(item => item.ProjectDocument)
        .Include(item => item.Waivers)
        .Include(item => item.Feedback);

    private static ConceptRevisionResponse Map(ConceptRevision item) => new()
    {
        Id = item.Id,
        ConceptOptionId = item.ConceptOptionId,
        Number = item.Number,
        CreatedAt = item.CreatedAt,
        FrozenAt = item.FrozenAt,
        Artifacts = item.Artifacts.OrderBy(artifact => artifact.Id).Select(artifact => new ConceptArtifactResponse
        {
            Id = artifact.Id,
            ProjectDocumentId = artifact.ProjectDocumentId,
            FileName = artifact.ProjectDocument.OriginalFileName,
            Kind = artifact.Kind.ToString(),
            FloorLabel = artifact.FloorLabel,
            SyncStatus = artifact.ProjectDocument.SyncStatus.ToString(),
        }).ToList(),
        Waivers = item.Waivers.Select(waiver => new ConceptChecklistWaiverResponse
        {
            Kind = waiver.Kind.ToString(), Reason = waiver.Reason, CreatedAt = waiver.CreatedAt,
        }).ToList(),
        Feedback = item.Feedback.OrderBy(feedback => feedback.OccurredAt).Select(feedback => new ConceptFeedbackResponse
        {
            Id = feedback.Id, ProjectDocumentId = feedback.ProjectDocumentId,
            CustomerName = feedback.CustomerName, Channel = feedback.Channel, EvidenceSource = feedback.EvidenceSource,
            OccurredAt = feedback.OccurredAt, Comment = feedback.Comment,
            Response = feedback.Response, Outcome = feedback.Outcome, RespondedAt = feedback.RespondedAt,
        }).ToList(),
    };
}
