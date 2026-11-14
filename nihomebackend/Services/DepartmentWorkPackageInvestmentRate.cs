using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

// Procurement uploads the filled investment-rate (suất đầu tư) form in its input-price work package;
// the package lead, independent of the uploader, approves it there. The latest approved revision
// prices the project's unit-cost quotes, the same way an approved BOQ prices BOQ quotes.
public sealed partial class DepartmentWorkPackageService
{
    public const string InvestmentRateRecordType = EntityTypes.ProjectInvestmentRateRevision;

    public async Task<int> RequireInvestmentRateUploadProjectAsync(int packageId, int callerUserId, CancellationToken ct = default)
    {
        var package = await RequireBusinessPackageAsync(packageId, WorkPackagePurpose.ProcurementInputPricing, ct);
        await EnsureCanWorkInDepartmentAsync(package, callerUserId, ct);
        return package.OperationalProjectId ?? throw Error("Nhóm công việc phải gắn Dự án trước khi upload suất đầu tư.");
    }

    public async Task<WorkPackageInvestmentRateResponse> RecordInvestmentRateUploadAsync(
        int packageId, int revisionId, int callerUserId, CancellationToken ct = default)
    {
        var response = await LoadInvestmentRateAsync(packageId, revisionId, ct)
            ?? throw Error("Không tìm thấy suất đầu tư vừa upload.");
        // Confirming the same file again returns the existing Draft; record its creation once.
        if (!await db.DepartmentWorkPackageHistory.AnyAsync(item => item.WorkPackageId == packageId &&
                item.EntityType == InvestmentRateRecordType && item.EntityId == revisionId && item.Action == "Created", ct))
        {
            var package = await db.DepartmentWorkPackages.SingleAsync(item => item.Id == packageId, ct);
            TouchPackage(package, callerUserId, DateTime.UtcNow);
            await AddHistoryAsync(packageId, InvestmentRateRecordType, revisionId, "Created", response, callerUserId, ct);
        }
        return response;
    }

    public async Task<bool> CanViewInvestmentRateAsync(
        int packageId, int revisionId, int callerUserId, bool canViewAll, CancellationToken ct = default)
    {
        var package = await BaseQuery().SingleOrDefaultAsync(item => item.Id == packageId, ct);
        if (package is null || !await CanViewAsync(package, callerUserId, canViewAll, ct)) return false;
        return await db.ProjectInvestmentRateRevisions.AsNoTracking()
            .AnyAsync(item => item.Id == revisionId && item.SourceWorkPackageId == packageId, ct);
    }

    public async Task<WorkPackageApprovalPreview> PreviewInvestmentRateApprovalEmailAsync(
        int packageId, int revisionId, int callerUserId, CancellationToken ct = default,
        WorkPackageApprovalRecipientsRequest? recipients = null)
    {
        var package = await RequireBusinessPackageAsync(packageId, WorkPackagePurpose.ProcurementInputPricing, ct);
        await EnsureCanWorkInDepartmentAsync(package, callerUserId, ct);
        var revision = await RequireSubmittableInvestmentRateAsync(packageId, revisionId, ct);
        return await approvalEmails.PreviewAsync(package, InvestmentRateRecordType, revision.Id, InvestmentRateReference(revision),
            revision.PreparedByUserId, CrmConcurrency.Encode(revision.RowVersion), callerUserId, ct, recipients);
    }

    public async Task<WorkPackageInvestmentRateResponse?> SubmitInvestmentRateAsync(
        int packageId, int revisionId, SubmitBusinessRecordRequest request, int callerUserId, CancellationToken ct = default)
    {
        var package = await RequireBusinessPackageAsync(packageId, WorkPackagePurpose.ProcurementInputPricing, ct);
        await EnsureCanWorkInDepartmentAsync(package, callerUserId, ct);
        var current = await db.ProjectInvestmentRateRevisions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == revisionId && item.SourceWorkPackageId == packageId, ct);
        if (current is null) return null;
        if (current.Status == ProjectInvestmentRateStatus.Submitted) return await LoadInvestmentRateAsync(packageId, revisionId, ct);
        var revision = await RequireSubmittableInvestmentRateAsync(packageId, revisionId, ct);
        await using var transaction = await BeginTransactionAsync(ct);
        CrmConcurrency.EnsureMatches(revision.RowVersion, request.RowVersion);
        var submittedAt = await approvalEmails.QueueAsync(package, InvestmentRateRecordType, revision.Id, InvestmentRateReference(revision),
            revision.PreparedByUserId, CrmConcurrency.Encode(revision.RowVersion), callerUserId, request.PreviewToken, ct);
        CrmConcurrency.Apply(db, revision, request.RowVersion);
        revision.Status = ProjectInvestmentRateStatus.Submitted;
        revision.SubmittedAt = submittedAt;
        revision.SubmittedByUserId = callerUserId;
        revision.DecisionReason = null;
        revision.UpdatedAt = DateTime.UtcNow;
        TouchPackage(package, callerUserId, revision.UpdatedAt);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        var response = (await LoadInvestmentRateAsync(packageId, revisionId, ct))!;
        await AddHistoryAsync(packageId, InvestmentRateRecordType, revisionId, "Submitted", response, callerUserId, ct);
        await CommitAsync(transaction, ct);
        return response;
    }

    public async Task<WorkPackageInvestmentRateResponse?> DecideInvestmentRateAsync(
        int packageId, int revisionId, DecideBusinessRecordRequest request, int callerUserId, CancellationToken ct = default)
    {
        var package = await RequireBusinessPackageAsync(packageId, WorkPackagePurpose.ProcurementInputPricing, ct);
        if (!await IsAdministratorAsync(callerUserId, ct) && package.LeadUserId != callerUserId)
            throw ScopeError();
        var revision = await db.ProjectInvestmentRateRevisions
            .SingleOrDefaultAsync(item => item.Id == revisionId && item.SourceWorkPackageId == packageId, ct);
        if (revision is null) return null;
        if (revision.Status != ProjectInvestmentRateStatus.Submitted)
            throw Error("Chỉ suất đầu tư đã gửi duyệt mới được quyết định.");
        if (revision.PreparedByUserId == callerUserId)
            throw Error("Người upload suất đầu tư không được tự phê duyệt hoặc từ chối cùng hồ sơ.");
        if (!request.Approved && string.IsNullOrWhiteSpace(request.Reason))
            throw Error("Vui lòng nhập lý do từ chối suất đầu tư.");
        CrmConcurrency.Apply(db, revision, request.RowVersion);
        var now = DateTime.UtcNow;
        revision.Status = request.Approved ? ProjectInvestmentRateStatus.Approved : ProjectInvestmentRateStatus.Rejected;
        revision.ApprovedAt = request.Approved ? now : null;
        revision.ApprovedByUserId = request.Approved ? callerUserId : null;
        revision.RejectedAt = request.Approved ? null : now;
        revision.RejectedByUserId = request.Approved ? null : callerUserId;
        revision.DecisionReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        revision.UpdatedAt = now;
        TouchPackage(package, callerUserId, now);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        var response = (await LoadInvestmentRateAsync(packageId, revisionId, ct))!;
        await AddHistoryAsync(packageId, InvestmentRateRecordType, revisionId, request.Approved ? "Approved" : "Rejected",
            response, callerUserId, ct);
        return response;
    }

    private async Task<ProjectInvestmentRateRevision> RequireSubmittableInvestmentRateAsync(int packageId, int revisionId, CancellationToken ct)
    {
        var revision = await db.ProjectInvestmentRateRevisions
            .SingleOrDefaultAsync(item => item.Id == revisionId && item.SourceWorkPackageId == packageId, ct)
            ?? throw Error("Không tìm thấy suất đầu tư trong nhóm công việc.");
        if (revision.Status is not (ProjectInvestmentRateStatus.Draft or ProjectInvestmentRateStatus.Rejected))
            throw Error("Suất đầu tư phải ở trạng thái Nháp hoặc Bị từ chối trước khi gửi duyệt.");
        if (revision.ConvertedAreaSqm <= 0 || revision.RatePerSqm <= 0)
            throw Error("Suất đầu tư phải có diện tích quy đổi và đơn giá/m² lớn hơn 0. Nhập đủ số liệu trong Excel rồi upload lại trước khi gửi duyệt.");
        return revision;
    }

    private static string InvestmentRateReference(ProjectInvestmentRateRevision revision) =>
        $"Suất đầu tư R{revision.RevisionNumber} · {revision.SourceFileName}";

    private async Task<List<WorkPackageInvestmentRateResponse>> ListInvestmentRatesAsync(int packageId, CancellationToken ct) =>
        WithLines(await InvestmentRateRows(db.ProjectInvestmentRateRevisions.AsNoTracking()
            .Where(item => item.SourceWorkPackageId == packageId)
            .OrderByDescending(item => item.RevisionNumber)).ToListAsync(ct));

    private async Task<WorkPackageInvestmentRateResponse?> LoadInvestmentRateAsync(int packageId, int revisionId, CancellationToken ct) =>
        WithLines(await InvestmentRateRows(db.ProjectInvestmentRateRevisions.AsNoTracking()
            .Where(item => item.SourceWorkPackageId == packageId && item.Id == revisionId)).ToListAsync(ct)).SingleOrDefault();

    // Filter and order before this projection: EF cannot translate either after it.
    private static IQueryable<InvestmentRateRow> InvestmentRateRows(IQueryable<ProjectInvestmentRateRevision> revisions) =>
        revisions.Select(item => new InvestmentRateRow(item, item.PreparedBy.FullName));

    private static List<WorkPackageInvestmentRateResponse> WithLines(List<InvestmentRateRow> rows) => rows.Select(row => new WorkPackageInvestmentRateResponse
    {
        Id = row.Revision.Id,
        RevisionNumber = row.Revision.RevisionNumber,
        Status = row.Revision.Status.ToString(),
        TemplateCode = row.Revision.TemplateCode,
        SourceFileName = row.Revision.SourceFileName,
        Currency = row.Revision.Currency,
        ConvertedAreaSqm = row.Revision.ConvertedAreaSqm,
        RatePerSqm = row.Revision.RatePerSqm,
        Total = row.Revision.Total,
        Areas = InvestmentRateWorkbookService.ReadAreas(row.Revision.AreaLinesJson),
        Groups = InvestmentRateWorkbookService.ReadGroups(row.Revision.GroupLinesJson),
        PreparedByUserId = row.Revision.PreparedByUserId,
        PreparedByName = row.PreparedByName,
        CreatedAt = row.Revision.CreatedAt,
        SubmittedAt = row.Revision.SubmittedAt,
        ApprovedAt = row.Revision.ApprovedAt,
        RejectedAt = row.Revision.RejectedAt,
        DecisionReason = row.Revision.DecisionReason,
        RowVersion = CrmConcurrency.Encode(row.Revision.RowVersion),
    }).ToList();

    private sealed record InvestmentRateRow(ProjectInvestmentRateRevision Revision, string? PreparedByName);
}
