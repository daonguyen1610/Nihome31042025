using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

// Procurement uploads the filled customer BOQ form in its input-price work package. The upload
// becomes a project BOQ revision linked to the package; the package lead, independent of the
// uploader, approves it there and it becomes the project's root tender BOQ.
public sealed partial class DepartmentWorkPackageService
{
    private const string BoqRecordType = "ProjectBoqRevision";

    public async Task<int> RequireBoqUploadProjectAsync(int packageId, int callerUserId, CancellationToken ct = default)
    {
        var package = await RequireBusinessPackageAsync(packageId, WorkPackagePurpose.ProcurementInputPricing, ct);
        await EnsureCanWorkInDepartmentAsync(package, callerUserId, ct);
        return package.OperationalProjectId ?? throw Error("Nhóm công việc phải gắn Dự án trước khi upload BOQ.");
    }

    public async Task<WorkPackageBoqWorkbookResponse> RecordBoqUploadAsync(
        int packageId, int revisionId, int callerUserId, CancellationToken ct = default)
    {
        var response = await LoadBoqWorkbookAsync(packageId, revisionId, ct)
            ?? throw Error("Không tìm thấy BOQ vừa upload.");
        // Confirming the same file again returns the existing Draft; record its creation once.
        if (!await db.DepartmentWorkPackageHistory.AnyAsync(item => item.WorkPackageId == packageId &&
                item.EntityType == BoqRecordType && item.EntityId == revisionId && item.Action == "Created", ct))
        {
            var package = await db.DepartmentWorkPackages.SingleAsync(item => item.Id == packageId, ct);
            TouchPackage(package, callerUserId, DateTime.UtcNow);
            await AddHistoryAsync(packageId, BoqRecordType, revisionId, "Created", response, callerUserId, ct);
        }
        return response;
    }

    // The project of a BOQ the caller may read, or null when the package or BOQ is out of scope.
    public async Task<int?> FindVisibleBoqProjectAsync(
        int packageId, int revisionId, int callerUserId, bool canViewAll, CancellationToken ct = default)
    {
        var package = await BaseQuery().SingleOrDefaultAsync(item => item.Id == packageId, ct);
        if (package is null || !await CanViewAsync(package, callerUserId, canViewAll, ct)) return null;
        return await db.ProjectBoqRevisions.AsNoTracking()
            .Where(item => item.Id == revisionId && item.SourceWorkPackageId == packageId)
            .Select(item => (int?)item.OperationalProjectId).SingleOrDefaultAsync(ct);
    }

    public async Task<WorkPackageApprovalPreview> PreviewBoqApprovalEmailAsync(
        int packageId, int revisionId, int callerUserId, CancellationToken ct = default,
        WorkPackageApprovalRecipientsRequest? recipients = null)
    {
        var package = await RequireBusinessPackageAsync(packageId, WorkPackagePurpose.ProcurementInputPricing, ct);
        await EnsureCanWorkInDepartmentAsync(package, callerUserId, ct);
        var revision = await RequireSubmittableBoqAsync(packageId, revisionId, ct);
        return await approvalEmails.PreviewAsync(package, BoqRecordType, revision.Id, BoqReference(revision),
            revision.PreparedByUserId, CrmConcurrency.Encode(revision.RowVersion), callerUserId, ct, recipients);
    }

    public async Task<WorkPackageBoqWorkbookResponse?> SubmitBoqWorkbookAsync(
        int packageId, int revisionId, SubmitBusinessRecordRequest request, int callerUserId, CancellationToken ct = default)
    {
        var package = await RequireBusinessPackageAsync(packageId, WorkPackagePurpose.ProcurementInputPricing, ct);
        await EnsureCanWorkInDepartmentAsync(package, callerUserId, ct);
        var current = await db.ProjectBoqRevisions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == revisionId && item.SourceWorkPackageId == packageId, ct);
        if (current is null) return null;
        if (current.Status == ProjectBoqRevisionStatus.Submitted) return await LoadBoqWorkbookAsync(packageId, revisionId, ct);
        var revision = await RequireSubmittableBoqAsync(packageId, revisionId, ct);
        await using var transaction = await BeginTransactionAsync(ct);
        CrmConcurrency.EnsureMatches(revision.RowVersion, request.RowVersion);
        var submittedAt = await approvalEmails.QueueAsync(package, BoqRecordType, revision.Id, BoqReference(revision),
            revision.PreparedByUserId, CrmConcurrency.Encode(revision.RowVersion), callerUserId, request.PreviewToken, ct);
        CrmConcurrency.Apply(db, revision, request.RowVersion);
        revision.Status = ProjectBoqRevisionStatus.Submitted;
        revision.SubmittedAt = submittedAt;
        revision.SubmittedByUserId = callerUserId;
        revision.DecisionReason = null;
        revision.UpdatedAt = DateTime.UtcNow;
        TouchPackage(package, callerUserId, revision.UpdatedAt);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        var response = (await LoadBoqWorkbookAsync(packageId, revisionId, ct))!;
        await AddHistoryAsync(packageId, BoqRecordType, revisionId, "Submitted", response, callerUserId, ct);
        await CommitAsync(transaction, ct);
        return response;
    }

    public async Task<WorkPackageBoqWorkbookResponse?> DecideBoqWorkbookAsync(
        int packageId, int revisionId, DecideBusinessRecordRequest request, int callerUserId, CancellationToken ct = default)
    {
        var package = await RequireBusinessPackageAsync(packageId, WorkPackagePurpose.ProcurementInputPricing, ct);
        if (!await IsAdministratorAsync(callerUserId, ct) && package.LeadUserId != callerUserId)
            throw ScopeError();
        var revision = await db.ProjectBoqRevisions
            .SingleOrDefaultAsync(item => item.Id == revisionId && item.SourceWorkPackageId == packageId, ct);
        if (revision is null) return null;
        if (revision.Status != ProjectBoqRevisionStatus.Submitted)
            throw Error("Chỉ BOQ đã gửi duyệt mới được quyết định.");
        if (revision.PreparedByUserId == callerUserId)
            throw Error("Người upload BOQ không được tự phê duyệt hoặc từ chối cùng hồ sơ.");
        if (!request.Approved && string.IsNullOrWhiteSpace(request.Reason))
            throw Error("Vui lòng nhập lý do từ chối BOQ.");
        CrmConcurrency.Apply(db, revision, request.RowVersion);
        var now = DateTime.UtcNow;
        // No signed contract yet: an approved BOQ is the tender BOQ, never an MR/warehouse allowance.
        revision.Status = request.Approved ? ProjectBoqRevisionStatus.TenderApproved : ProjectBoqRevisionStatus.Rejected;
        revision.ApprovedAt = request.Approved ? now : null;
        revision.ApprovedByUserId = request.Approved ? callerUserId : null;
        revision.RejectedAt = request.Approved ? null : now;
        revision.RejectedByUserId = request.Approved ? null : callerUserId;
        revision.DecisionReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        revision.UpdatedAt = now;
        TouchPackage(package, callerUserId, now);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        var response = (await LoadBoqWorkbookAsync(packageId, revisionId, ct))!;
        await AddHistoryAsync(packageId, BoqRecordType, revisionId, request.Approved ? "Approved" : "Rejected", response, callerUserId, ct);
        return response;
    }

    private async Task<ProjectBoqRevision> RequireSubmittableBoqAsync(int packageId, int revisionId, CancellationToken ct)
    {
        var revision = await db.ProjectBoqRevisions.Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.Id == revisionId && item.SourceWorkPackageId == packageId, ct)
            ?? throw Error("Không tìm thấy BOQ trong nhóm công việc.");
        if (revision.Status is not (ProjectBoqRevisionStatus.Draft or ProjectBoqRevisionStatus.Rejected))
            throw Error("BOQ phải ở trạng thái Nháp hoặc Bị từ chối trước khi gửi duyệt.");
        if (revision.Lines.Count == 0) throw Error("BOQ phải có ít nhất một hạng mục.");
        var unpriced = revision.Lines.Count(line => line.PriceMissing);
        if (unpriced > 0)
            throw Error($"Còn {unpriced} hạng mục chưa có đơn giá. Nhập đủ giá trong Excel rồi upload lại trước khi gửi duyệt.");
        return revision;
    }

    private static string BoqReference(ProjectBoqRevision revision) => $"BOQ R{revision.RevisionNumber} · {revision.SourceFileName}";

    private Task<List<WorkPackageBoqWorkbookResponse>> ListBoqWorkbooksAsync(int packageId, CancellationToken ct) =>
        BoqWorkbookQuery(packageId).OrderByDescending(item => item.RevisionNumber).ToListAsync(ct);

    private Task<WorkPackageBoqWorkbookResponse?> LoadBoqWorkbookAsync(int packageId, int revisionId, CancellationToken ct) =>
        BoqWorkbookQuery(packageId).SingleOrDefaultAsync(item => item.Id == revisionId, ct);

    private IQueryable<WorkPackageBoqWorkbookResponse> BoqWorkbookQuery(int packageId) =>
        db.ProjectBoqRevisions.AsNoTracking().Where(item => item.SourceWorkPackageId == packageId)
            .Select(item => new WorkPackageBoqWorkbookResponse
            {
                Id = item.Id,
                RevisionNumber = item.RevisionNumber,
                Status = item.Status.ToString(),
                TemplateCode = item.TemplateCode,
                SourceFileName = item.SourceFileName,
                Currency = item.Currency,
                Total = item.CostTotal,
                LineCount = item.Lines.Count,
                MissingPriceCount = item.Lines.Count(line => line.PriceMissing),
                PreparedByUserId = item.PreparedByUserId,
                PreparedByName = item.PreparedBy.FullName,
                CreatedAt = item.CreatedAt,
                SubmittedAt = item.SubmittedAt,
                ApprovedAt = item.ApprovedAt,
                RejectedAt = item.RejectedAt,
                DecisionReason = item.DecisionReason,
                RowVersion = CrmConcurrency.Encode(item.RowVersion),
            });
}
