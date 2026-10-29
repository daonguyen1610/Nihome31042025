using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services.HardDelete;

namespace NihomeBackend.Services;

/// <summary>
/// Tender (Gói thầu) service — see <see cref="ITenderService"/>.
///
/// Behaviour highlights:
/// - <b>Create</b> validates SubmissionDeadline &gt; now, generates a
///   sequential <c>TD-{year}-{seq:D4}</c> code, and seeds the checklist
///   from the <c>tender_checklist_default</c> master-data category so
///   every new tender ships with the 7 standard items.
/// - <b>Update</b> enforces per-status edit lanes: only Deadline /
///   Preparer / Note may change while Status = Preparing; other statuses
///   accept Note only (per NIH-96 AC).
/// - <b>Delete</b> is blocked once Status ≥ Submitted so audit trails
///   cannot silently disappear after a bid has been filed.
/// - <b>Notification</b> is fired for the preparer on create + when the
///   preparer is reassigned during edit (assign / reassign events per AC).
/// </summary>
public class TenderService(
    AppDbContext db,
    INotificationService notifications,
    ILogger<TenderService> logger,
    ICrmHardDeletePlanService hardDeletePlans,
    IHardDeleteOperationService hardDeleteOperations) : ITenderService
{
    private const int MaxPageSize = 100;
    private const int DeadlineImminentDays = 3;
    private const int MaxCodeAttempts = 3;
    private const string ChecklistTemplateCategory = "tender_checklist_default";

    // ------------------------------ List / Get ------------------------------

    public async Task<IReadOnlyList<TenderCustomerOptionResponse>> ListReportCustomersAsync(CancellationToken ct = default)
        => await db.Tenders.AsNoTracking()
            .Select(item => new { Id = item.CustomerId, item.Customer.Name })
            .Distinct().OrderBy(item => item.Name)
            .Select(item => new TenderCustomerOptionResponse { Id = item.Id, Name = item.Name })
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TenderAssigneeOptionResponse>> ListReportPreparersAsync(CancellationToken ct = default)
        => await db.Tenders.AsNoTracking().Where(item => item.PreparerUserId != null)
            .Select(item => new TenderAssigneeOptionResponse
            {
                Id = item.PreparerUserId!.Value,
                FullName = item.Preparer!.FullName ?? string.Empty,
                RoleCode = item.Preparer!.RoleEntity != null ? item.Preparer!.RoleEntity.Code : item.Preparer!.Role.ToString(),
                RoleName = item.Preparer!.RoleEntity != null ? item.Preparer!.RoleEntity.Name : item.Preparer!.Role.ToString(),
                RoleLabelKey = item.Preparer!.RoleEntity != null ? item.Preparer!.RoleEntity.LabelKey : null,
                DepartmentId = item.Preparer!.RoleEntity != null && item.Preparer!.RoleEntity.RoleGroup != null && item.Preparer!.RoleEntity.RoleGroup.IsActive ? item.Preparer!.RoleEntity.RoleGroupId : null,
                DepartmentLabelKey = item.Preparer!.RoleEntity != null && item.Preparer!.RoleEntity.RoleGroup != null && item.Preparer!.RoleEntity.RoleGroup.IsActive ? item.Preparer!.RoleEntity.RoleGroup.LabelKey : null,
            })
.Distinct().OrderBy(item => item.FullName)
            .ToListAsync(ct);

    public async Task<TenderStatisticsResponse> StatisticsAsync(TenderStatisticsParams p, CancellationToken ct = default)
    {
        var scoped = db.Tenders.AsNoTracking().AsQueryable();
        if (p.CustomerId.HasValue) scoped = scoped.Where(item => item.CustomerId == p.CustomerId.Value);
        if (p.PreparerUserId.HasValue) scoped = scoped.Where(item => item.PreparerUserId == p.PreparerUserId.Value);
        var first = TenderReporting.FirstSubmissions(db.TenderSubmissions.AsNoTracking(), p.StartUtc, p.EndUtc);
        var counts = await scoped.Where(item => first.Any(submission => submission.TenderId == item.Id))
            .GroupBy(item => item.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() }).ToListAsync(ct);
        var total = counts.Sum(item => item.Count);
        var won = counts.Where(item => item.Status == TenderStatus.Won).Sum(item => item.Count);
        return new TenderStatisticsResponse
        {
            SubmittedCount = total,
            WonCount = won,
            LostCount = counts.Where(item => item.Status == TenderStatus.Lost).Sum(item => item.Count),
            PendingCount = counts.Where(item => item.Status == TenderStatus.Submitted).Sum(item => item.Count),
            CancelledCount = counts.Where(item => item.Status == TenderStatus.Cancelled).Sum(item => item.Count),
            WinRate = total == 0 ? null : Math.Round(100m * won / total, 2),
            MissingSubmissionCount = await scoped.CountAsync(item =>
                (item.Status == TenderStatus.Submitted || item.Status == TenderStatus.Won || item.Status == TenderStatus.Lost ||
                    item.Status == TenderStatus.Cancelled && item.SubmittedAt != null) &&
                !db.TenderSubmissions.Any(submission => submission.TenderId == item.Id && submission.Sequence == 1), ct),
            CalculatedAt = DateTime.UtcNow,
        };
    }

    public async Task<TenderListResponse> ListAsync(TenderListParams p, CancellationToken ct = default)
    {
        var page = p.Page < 1 ? 1 : p.Page;
        var pageSize = Math.Clamp(p.PageSize <= 0 ? 20 : p.PageSize, 1, MaxPageSize);

        var q = db.Tenders
            .AsNoTracking()
            .Include(t => t.Customer)
            .Include(t => t.Preparer)
            .Include(t => t.OperationalProject)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(p.Status))
        {
            var statuses = p.Status.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => Enum.TryParse<TenderStatus>(s, true, out var v) ? (TenderStatus?)v : null)
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();
            if (statuses.Count > 0)
            {
                q = q.Where(t => statuses.Contains(t.Status));
            }
        }
        if (p.CustomerId.HasValue) q = q.Where(t => t.CustomerId == p.CustomerId.Value);
        if (p.PreparerUserId.HasValue) q = q.Where(t => t.PreparerUserId == p.PreparerUserId.Value);
        if (p.SubmittedOnly || p.FromDate.HasValue || p.ToDate.HasValue)
        {
            var first = TenderReporting.FirstSubmissions(db.TenderSubmissions.AsNoTracking(), p.StartUtc, p.EndUtc);
            q = q.Where(t => first.Any(submission => submission.TenderId == t.Id));
        }
        if (p.OpeningMonth.HasValue) q = q.Where(t => t.OpeningDate != null && t.OpeningDate!.Value.Month == p.OpeningMonth.Value);
        if (p.OpeningYear.HasValue) q = q.Where(t => t.OpeningDate != null && t.OpeningDate!.Value.Year == p.OpeningYear.Value);
        if (!string.IsNullOrWhiteSpace(p.Search))
        {
            var term = p.Search.Trim();
            q = q.Where(t => EF.Functions.Like(t.Name, $"%{term}%")
                          || EF.Functions.Like(t.Customer.Name, $"%{term}%")
                          || EF.Functions.Like(t.Code, $"%{term}%"));
        }

        var total = await q.CountAsync(ct);

        // Spec NIH-95: default sort SubmissionDeadline ASC so soon-to-expire
        // rows surface first.
        var rows = await q
            .OrderBy(t => t.SubmissionDeadline)
            .ThenByDescending(t => t.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new
            {
                Tender = t,
                DoneCount = db.TenderChecklistItems.Count(i => i.TenderId == t.Id
                    && i.IsRequired
                    && (i.Status == TenderChecklistItemStatus.Done || i.Status == TenderChecklistItemStatus.Submitted)),
                TotalCount = db.TenderChecklistItems.Count(i => i.TenderId == t.Id && i.IsRequired),
            })
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        return new TenderListResponse
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Items = rows.Select(r => new TenderListItemResponse
            {
                Id = r.Tender.Id,
                Code = r.Tender.Code,
                Name = r.Tender.Name,
                CustomerId = r.Tender.CustomerId,
                CustomerName = r.Tender.Customer.Name,
                OpeningDate = r.Tender.OpeningDate,
                SubmissionDeadline = r.Tender.SubmissionDeadline,
                PreparerUserId = r.Tender.PreparerUserId,
                PreparerName = r.Tender.Preparer?.FullName,
                Status = r.Tender.Status.ToString(),
                ChecklistCompletionPercent = ComputePercent(r.DoneCount, r.TotalCount),
                IsDeadlineImminent = IsDeadlineImminent(r.Tender.Status, r.Tender.SubmissionDeadline, now)
                    && ComputePercent(r.DoneCount, r.TotalCount) < 100,
                UpdatedAt = r.Tender.UpdatedAt,
            }).ToList(),
        };
    }

    public async Task<TenderResponse?> GetAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.Tenders
            .AsNoTracking()
            .Include(t => t.Customer)
            .Include(t => t.Preparer)
            .Include(t => t.InvitationReceivedByUser)
            .Include(t => t.OperationalProject)
            .Include(t => t.ChecklistItems).ThenInclude(i => i.Owner)
            .Include(t => t.ChecklistItems).ThenInclude(i => i.EvidenceRevisions)
            .Include(t => t.Submissions).ThenInclude(s => s.SubmittedBy)
            .Include(t => t.Submissions).ThenInclude(s => s.Items)
            .Include(t => t.Milestones)
            .Include(t => t.InvitationDocuments)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
        if (entity is null) return null;

        // Resolve linked opportunity name + lost-reason label so the FE
        // Result tab renders full names without extra roundtrips. Cheap:
        // one row from each table when a terminal transition has been
        // recorded, no query at all otherwise.
        var wonOpportunityName = entity.WonOpportunityId.HasValue
            ? await db.Opportunities.AsNoTracking()
                .Where(o => o.Id == entity.WonOpportunityId.Value)
                .Select(o => o.Name)
                .FirstOrDefaultAsync(ct)
            : null;
        var lostReasonLabel = string.IsNullOrWhiteSpace(entity.LostReasonCode)
            ? null
            : await db.MasterDataOptions.AsNoTracking()
                .Where(m => m.Category == "opportunity_lost_reason" && m.Code == entity.LostReasonCode)
                .Select(m => m.Name)
                .FirstOrDefaultAsync(ct);

        return MapDetail(entity, wonOpportunityName, lostReasonLabel);
    }

    public async Task<IReadOnlyList<TenderAssigneeOptionResponse>> ListAssigneeOptionsAsync(
        CancellationToken ct = default) =>
        await db.Users.AsNoTracking()
            .Where(user => user.IsActive &&
                user.RoleEntity != null && user.RoleEntity.IsActive &&
                user.RoleEntity.RolePermissions.Any(grant =>
                    grant.Permission.Module == "crm.tenders" && grant.Permission.Action == "manage"))
            .OrderBy(user => user.FullName)
            .ThenBy(user => user.Id)
            .Select(user => new TenderAssigneeOptionResponse
            {
                Id = user.Id,
                FullName = user.FullName ?? user.Email,
                RoleCode = user.RoleEntity != null ? user.RoleEntity.Code : user.Role.ToString(),
                RoleName = user.RoleEntity != null ? user.RoleEntity.Name : user.Role.ToString(),
                RoleLabelKey = user.RoleEntity != null ? user.RoleEntity.LabelKey : null,
                DepartmentId = user.RoleEntity != null && user.RoleEntity.RoleGroup != null && user.RoleEntity.RoleGroup.IsActive ? user.RoleEntity.RoleGroupId : null,
                DepartmentLabelKey = user.RoleEntity != null && user.RoleEntity.RoleGroup != null && user.RoleEntity.RoleGroup.IsActive ? user.RoleEntity.RoleGroup.LabelKey : null,
            })
            .ToListAsync(ct);

    // ------------------------------ Create ----------------------------------

    public async Task<TenderResponse> CreateAsync(CreateTenderRequest request, int callerUserId, CancellationToken ct = default)
    {
        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new TenderOperationException("Tên gói thầu là bắt buộc.");
        }

        if (request.SubmissionDeadline <= DateTime.UtcNow)
        {
            throw new TenderOperationException("Deadline nộp phải lớn hơn hiện tại.");
        }

        ValidateInvitation(request.InvitationReceivedAt, request.SubmissionDeadline);
        var invitationSource = TrimOrNull(request.InvitationSource)
            ?? throw new TenderOperationException("Nguồn nhận hồ sơ mời thầu là bắt buộc, ví dụ: Cổng đấu thầu quốc gia.");

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId, ct)
            ?? throw new TenderOperationException($"Khách hàng #{request.CustomerId} không tồn tại.");
        if (!request.OperationalProjectId.HasValue)
            throw new TenderOperationException("Gói thầu phải gắn với Dự án vận hành đã có.");
        await ValidateTenderProjectAsync(request.OperationalProjectId, customer.Id, ct);

        if (request.PreparerUserId.HasValue &&
            !await db.Users.AnyAsync(u => u.Id == request.PreparerUserId.Value, ct))
        {
            throw new TenderOperationException($"Người phụ trách #{request.PreparerUserId} không tồn tại.");
        }

        // Default the preparer to the caller when the client didn't pick one.
        // Sales users don't have users.view so the FE picker isn't visible
        // to them — auto-assignment keeps the tender owned by a real person.
        var effectivePreparerId = request.PreparerUserId ?? callerUserId;
        var invitationReceivedByUserId = request.InvitationReceivedByUserId ?? callerUserId;
        if (!await db.Users.AnyAsync(user => user.Id == invitationReceivedByUserId && user.IsActive, ct))
            throw new TenderOperationException($"Người tiếp nhận HSMT #{invitationReceivedByUserId} không tồn tại hoặc đã ngừng hoạt động.");

        var codePrefix = $"TD-{DateTime.UtcNow.Year}-";
        var entity = new Tender
        {
            Code = await SequentialCodes.NextAsync(db.Tenders.Select(t => t.Code), codePrefix, 4, ct),
            Name = name,
            CustomerId = customer.Id,
            OperationalProjectId = request.OperationalProjectId,
            OpeningDate = request.OpeningDate,
            InvitationReceivedAt = request.InvitationReceivedAt,
            InvitationReference = TrimOrNull(request.InvitationReference),
            InvitationSource = invitationSource,
            InvitationReceivedByUserId = invitationReceivedByUserId,
            InvitationPortalUrl = ValidateOptionalHttpUrl(request.InvitationPortalUrl),
            SubmissionDeadline = request.SubmissionDeadline,
            PreparerUserId = effectivePreparerId,
            InfoSource = TrimOrNull(request.InfoSource),
            Note = TrimOrNull(request.Note),
            Status = TenderStatus.Preparing,
            CreatedByUserId = callerUserId,
            UpdatedByUserId = callerUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Tenders.Add(entity);
        // Two users saving at the same moment can both read the same highest
        // code; the unique index rejects the second, which then takes the next.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.SaveChangesAsync(ct);
                break;
            }
            catch (DbUpdateException exception) when (
                SequentialCodes.IsUniqueViolation(exception) && attempt < MaxCodeAttempts)
            {
                entity.Code = await SequentialCodes.NextAsync(
                    db.Tenders.AsNoTracking().Select(t => t.Code), codePrefix, 4, ct);
            }
            catch (DbUpdateException exception) when (SequentialCodes.IsUniqueViolation(exception))
            {
                throw new TenderOperationException(
                    "Hệ thống đang cấp mã gói thầu cho nhiều người cùng lúc. Vui lòng bấm Lưu lại.");
            }
        }

        await SeedDefaultChecklistAsync(entity.Id, ct);
        await NotifyAssigneeAsync(entity, isReassignment: false, ct);

        logger.LogInformation("Tender {Id} ({Code}) created for customer {CustomerId} by user {UserId}",
            entity.Id, entity.Code, customer.Id, callerUserId);

        return (await GetAsync(entity.Id, ct))!;
    }

    // ------------------------------ Update ----------------------------------

    public async Task<TenderResponse?> UpdateAsync(int id, UpdateTenderRequest request, int callerUserId, CancellationToken ct = default)
    {
        var entity = await db.Tenders.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (entity is null) return null;

        GuardNotTerminal(entity, "chỉnh sửa");

        if (request.SubmissionDeadline <= DateTime.UtcNow && entity.Status == TenderStatus.Preparing)
        {
            throw new TenderOperationException("Deadline nộp phải lớn hơn hiện tại.");
        }
        if (entity.Status == TenderStatus.Preparing)
            ValidateInvitation(request.InvitationReceivedAt ?? entity.InvitationReceivedAt, request.SubmissionDeadline);

        if (request.PreparerUserId.HasValue &&
            !await db.Users.AnyAsync(u => u.Id == request.PreparerUserId.Value, ct))
        {
            throw new TenderOperationException($"Người phụ trách #{request.PreparerUserId} không tồn tại.");
        }
        var customerId = entity.CustomerId;
        await ValidateTenderProjectAsync(request.OperationalProjectId, customerId, ct);

        // Per NIH-96 AC: only Deadline / Preparer / Note (+ derived Note) may
        // change while Status = Preparing; other statuses accept Note only.
        var previousPreparerId = entity.PreparerUserId;
        if (entity.Status == TenderStatus.Preparing)
        {
            var name = (request.Name ?? string.Empty).Trim();
            if (name.Length == 0)
                throw new TenderOperationException("Tên gói thầu là bắt buộc, ví dụ: Gói thầu xây dựng nhà máy A.");
            entity.Name = name;
            entity.OpeningDate = request.OpeningDate;
            if (request.InvitationReceivedAt.HasValue)
                entity.InvitationReceivedAt = request.InvitationReceivedAt;
            if (request.InvitationReference is not null)
                entity.InvitationReference = TrimOrNull(request.InvitationReference);
            if (request.InvitationSource is not null)
                entity.InvitationSource = TrimOrNull(request.InvitationSource)
                    ?? throw new TenderOperationException("Nguồn nhận hồ sơ mời thầu là bắt buộc.");
            if (request.InvitationReceivedByUserId.HasValue)
            {
                if (!await db.Users.AnyAsync(user => user.Id == request.InvitationReceivedByUserId && user.IsActive, ct))
                    throw new TenderOperationException($"Người tiếp nhận HSMT #{request.InvitationReceivedByUserId} không tồn tại hoặc đã ngừng hoạt động.");
                entity.InvitationReceivedByUserId = request.InvitationReceivedByUserId.Value;
            }
            if (request.InvitationPortalUrl is not null)
                entity.InvitationPortalUrl = ValidateOptionalHttpUrl(request.InvitationPortalUrl);
            entity.SubmissionDeadline = request.SubmissionDeadline;
            entity.PreparerUserId = request.PreparerUserId;
            entity.InfoSource = TrimOrNull(request.InfoSource);
            // An omitted field must not unlink the shared project: submission and
            // the Finance – Accounting dossier review both depend on the link, and
            // older clients do not send it on every save.
            if (request.OperationalProjectId is > 0)
                entity.OperationalProjectId = request.OperationalProjectId;
        }
        entity.Note = TrimOrNull(request.Note);
        entity.UpdatedByUserId = callerUserId;
        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        if (entity.Status == TenderStatus.Preparing
            && entity.PreparerUserId != previousPreparerId
            && entity.PreparerUserId.HasValue)
        {
            await NotifyAssigneeAsync(entity, isReassignment: true, ct);
        }

        logger.LogInformation("Tender {Id} updated by user {UserId} (status={Status})",
            entity.Id, callerUserId, entity.Status);
        return await GetAsync(entity.Id, ct);
    }

    // ------------------------------ Delete ----------------------------------

    public async Task<DeletionImpactResponse?> GetDeletionImpactAsync(
        int id, CancellationToken ct = default) =>
        (await hardDeletePlans.ForTenderAsync(id, ct))?.Impact;

    public async Task<HardDeleteOperationResult?> DeleteAsync(
        int id,
        ConfirmDeletionRequest request,
        int callerUserId,
        CancellationToken ct = default)
    {
        var result = await DurableHardDeleteStarter.StartAsync(
            db, hardDeleteOperations, () => hardDeletePlans.ForTenderAsync(id, ct), plan =>
            {
                if (!plan.Impact.CanDelete)
                    throw new TenderOperationException("Chỉ gói thầu đang Chuẩn bị mới có thể bị xóa.");
                if (!string.Equals(request.PlanToken?.Trim(), plan.Impact.PlanToken, StringComparison.Ordinal))
                    throw new DeletionPlanChangedException(
                        "Dữ liệu liên quan đã thay đổi. Vui lòng xem lại danh sách ảnh hưởng trước khi xoá.");
                if (!string.Equals(request.Confirmation, plan.Impact.RequiredConfirmation, StringComparison.Ordinal))
                    throw new TenderOperationException(
                        $"Mã xác nhận không đúng. Vui lòng nhập chính xác '{plan.Impact.RequiredConfirmation}'.");
                return new CreateHardDeleteOperationRequest(
                    EntityTypes.Tender,
                    id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    plan.Impact.ResourceLabel,
                    plan.Impact.PlanToken,
                    request.Confirmation,
                    callerUserId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    plan.Items);
            }, ct);
        if (result is null) return null;
        logger.LogInformation("Tender {Id} durable hard-delete is {Status}", id, result.Status);
        return result;
    }

    // ------------------------------ NIH-97 Detail-page workflow ------------------------------

    public async Task<TenderResponse?> UpdateChecklistItemAsync(int tenderId, int itemId,
        UpdateTenderChecklistItemRequest request, int callerUserId, CancellationToken ct = default)
    {
        var item = await db.TenderChecklistItems
            .Include(i => i.Tender)
            .FirstOrDefaultAsync(i => i.Id == itemId && i.TenderId == tenderId, ct);
        if (item is null) return null;
        GuardChecklistMutable(item.Tender);

        if (request.IsRequired.HasValue)
        {
            item.IsRequired = request.IsRequired.Value;
            if (item.IsRequired && item.Status == TenderChecklistItemStatus.NotApplicable)
            {
                item.Status = TenderChecklistItemStatus.NotStarted;
                item.NotApplicableReason = null;
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<TenderChecklistItemStatus>(request.Status, ignoreCase: true, out var next))
            {
                throw new TenderOperationException($"Trạng thái checklist '{request.Status}' không hợp lệ.");
            }
            if (next is TenderChecklistItemStatus.Done or TenderChecklistItemStatus.Submitted)
            {
                EnsureChecklistEvidence(item);
            }
            if (next == TenderChecklistItemStatus.Submitted)
            {
                if (!item.CompletedAt.HasValue || !item.CompletedByUserId.HasValue)
                    throw new TenderOperationException("Hồ sơ phải được người phụ trách hoàn thành trước khi xác nhận độc lập.");
                if (item.CompletedByUserId == callerUserId)
                    throw new TenderOperationException("Người hoàn thành hồ sơ không được tự xác nhận hồ sơ của chính mình.");
            }
            if (next == TenderChecklistItemStatus.NotApplicable)
            {
                if (item.IsRequired)
                    throw new TenderOperationException("Chỉ hồ sơ không bắt buộc mới được đánh dấu Không áp dụng.");
                var reason = TrimOrNull(request.NotApplicableReason);
                if (reason is null)
                    throw new TenderOperationException("Vui lòng nhập lý do khi đánh dấu hồ sơ Không áp dụng.");
                item.NotApplicableReason = reason;
            }
            item.Status = next;
            if (next == TenderChecklistItemStatus.Done)
            {
                item.CompletedAt ??= DateTime.UtcNow;
                item.CompletedByUserId ??= callerUserId;
                item.VerifiedAt = null;
                item.VerifiedByUserId = null;
                item.NotApplicableReason = null;
            }
            else if (next == TenderChecklistItemStatus.Submitted)
            {
                item.VerifiedAt = DateTime.UtcNow;
                item.VerifiedByUserId = callerUserId;
                item.NotApplicableReason = null;
            }
            else
            {
                item.VerifiedAt = null;
                item.VerifiedByUserId = null;
                if (next != TenderChecklistItemStatus.NotApplicable)
                    item.NotApplicableReason = null;
            }
        }

        var previousOwnerUserId = item.OwnerUserId;
        if (request.ClearOwner)
        {
            item.OwnerUserId = null;
        }
        else if (request.OwnerUserId.HasValue)
        {
            var isEligibleAssignee = await db.Users.AnyAsync(user =>
                user.Id == request.OwnerUserId.Value && user.IsActive &&
                user.RoleEntity != null && user.RoleEntity.IsActive &&
                user.RoleEntity.RolePermissions.Any(grant =>
                    grant.Permission.Module == "crm.tenders" && grant.Permission.Action == "manage"), ct);
            if (!isEligibleAssignee)
            {
                throw new TenderOperationException(
                    $"Người phụ trách #{request.OwnerUserId} không hoạt động hoặc không có quyền quản lý gói thầu.");
            }
            item.OwnerUserId = request.OwnerUserId.Value;
        }

        if (request.ClearInternalDeadline)
        {
            item.InternalDeadline = null;
        }
        else if (request.InternalDeadline.HasValue)
        {
            var deadline = request.InternalDeadline.Value;
            if (deadline.Date < DateTime.UtcNow.Date)
            {
                throw new TenderOperationException("Deadline nội bộ không được trước ngày hiện tại.");
            }
            if (deadline.Date > item.Tender.SubmissionDeadline.Date)
            {
                throw new TenderOperationException("Deadline nội bộ không được sau deadline nộp thầu.");
            }
            item.InternalDeadline = deadline;
        }

        if (request.ClearWorkTask)
        {
            item.WorkTaskId = null;
        }
        else if (request.WorkTaskId.HasValue)
        {
            var belongsToTenderPlan = await db.DepartmentWorkTasks.AsNoTracking().AnyAsync(task =>
                task.Id == request.WorkTaskId.Value &&
                task.WorkPackage.SourceTenderId == tenderId &&
                (task.WorkPackage.Purpose == WorkPackagePurpose.TenderPlanning ||
                    task.WorkPackage.Purpose == WorkPackagePurpose.General), ct);
            if (!belongsToTenderPlan)
                throw new TenderOperationException("Task kế hoạch không tồn tại hoặc không thuộc gói thầu này.");
            item.WorkTaskId = request.WorkTaskId.Value;
        }

        var now = DateTime.UtcNow;
        item.UpdatedAt = now;

        // Bump parent tender's UpdatedAt so list views + optimistic-UI
        // detect the change without a targeted reload.
        var tender = item.Tender;
        tender.UpdatedAt = now;
        tender.UpdatedByUserId = callerUserId;

        await db.SaveChangesAsync(ct);

        if (item.OwnerUserId.HasValue && item.OwnerUserId != previousOwnerUserId)
        {
            await NotifyChecklistAssigneeAsync(item, ct);
        }

        logger.LogInformation("Tender {TenderId} checklist item {ItemId} updated by user {UserId}",
            tenderId, itemId, callerUserId);
        return await GetAsync(tenderId, ct);
    }

    public async Task<TenderResponse?> CreateChecklistItemAsync(int tenderId,
        CreateTenderChecklistItemRequest request, int callerUserId, CancellationToken ct = default)
    {
        var tender = await db.Tenders.FirstOrDefaultAsync(item => item.Id == tenderId, ct);
        if (tender is null) return null;
        GuardChecklistMutable(tender);

        var title = (request.Title ?? string.Empty).Trim();
        if (title.Length == 0)
            throw new TenderOperationException("Tên hồ sơ là bắt buộc, ví dụ: Bản vẽ dự thầu kiến trúc.");
        if (await db.TenderChecklistItems.AnyAsync(item => item.TenderId == tenderId && item.Title == title, ct))
            throw new TenderOperationException($"Gói thầu đã có mục hồ sơ '{title}'.");
        ValidateChecklistDeadline(request.InternalDeadline, tender.SubmissionDeadline);
        if (request.OwnerUserId.HasValue)
            await EnsureEligibleChecklistAssigneeAsync(request.OwnerUserId.Value, ct);
        if (!Enum.TryParse<TenderDocumentKind>(request.DocumentKind, true, out var documentKind))
            throw new TenderOperationException($"Loại hồ sơ '{request.DocumentKind}' không hợp lệ.");
        if (request.WorkTaskId.HasValue && !await db.DepartmentWorkTasks.AsNoTracking().AnyAsync(task =>
                task.Id == request.WorkTaskId.Value && task.WorkPackage.SourceTenderId == tenderId &&
                (task.WorkPackage.Purpose == WorkPackagePurpose.TenderPlanning ||
                    task.WorkPackage.Purpose == WorkPackagePurpose.General), ct))
            throw new TenderOperationException("Task kế hoạch không tồn tại hoặc không thuộc gói thầu này.");

        var nextSortOrder = await db.TenderChecklistItems
            .Where(item => item.TenderId == tenderId)
            .Select(item => (int?)item.SortOrder)
            .MaxAsync(ct) ?? 0;
        var now = DateTime.UtcNow;
        db.TenderChecklistItems.Add(new TenderChecklistItem
        {
            TenderId = tenderId,
            TemplateCode = null,
            Title = title,
            IsRequired = request.IsRequired,
            DocumentKind = documentKind,
            OwnerUserId = request.OwnerUserId,
            WorkTaskId = request.WorkTaskId,
            InternalDeadline = request.InternalDeadline,
            Status = TenderChecklistItemStatus.NotStarted,
            SortOrder = nextSortOrder + 1,
            CreatedAt = now,
            UpdatedAt = now,
        });
        tender.UpdatedAt = now;
        tender.UpdatedByUserId = callerUserId;
        await db.SaveChangesAsync(ct);
        return await GetAsync(tenderId, ct);
    }

    public async Task<TenderResponse?> DeleteChecklistItemAsync(int tenderId, int itemId,
        int callerUserId, CancellationToken ct = default)
    {
        var item = await db.TenderChecklistItems.Include(row => row.Tender)
            .FirstOrDefaultAsync(row => row.Id == itemId && row.TenderId == tenderId, ct);
        if (item is null) return null;
        GuardChecklistMutable(item.Tender);
        if (!string.IsNullOrWhiteSpace(item.TemplateCode))
            throw new TenderOperationException("Hồ sơ thuộc mẫu chuẩn không thể xóa; hãy chuyển thành Không bắt buộc khi HSMT không yêu cầu.");
        if (!string.IsNullOrWhiteSpace(item.FilePath) || item.CapabilityDocumentId.HasValue)
            throw new TenderOperationException("Không thể xóa mục hồ sơ đã có file. Hãy xử lý file trước khi xóa.");

        db.TenderChecklistItems.Remove(item);
        item.Tender.UpdatedAt = DateTime.UtcNow;
        item.Tender.UpdatedByUserId = callerUserId;
        await db.SaveChangesAsync(ct);
        return await GetAsync(tenderId, ct);
    }

    private async Task NotifyChecklistAssigneeAsync(TenderChecklistItem item, CancellationToken _)
    {
        try
        {
            await notifications.NotifyFromTemplateAsync(
                item.OwnerUserId!.Value,
                "tender.checklist.assigned",
                new Dictionary<string, string>
                {
                    ["tenderCode"] = item.Tender.Code,
                    ["tenderName"] = item.Tender.Name,
                    ["itemTitle"] = item.Title,
                    ["internalDeadline"] = item.InternalDeadline?.ToString("dd/MM/yyyy") ?? "—",
                },
                EntityTypes.Tender,
                item.TenderId,
                $"/admin/tenders/{item.TenderId}");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Tender {TenderId} checklist item {ItemId} persisted but assignee notification failed.",
                item.TenderId, item.Id);
        }
    }

    public Task<TenderResponse?> AttachChecklistFileAsync(int tenderId, int itemId,
        string filePath, string originalFileName, int callerUserId, CancellationToken ct = default) =>
        AttachChecklistFileAsync(tenderId, itemId, filePath, originalFileName, 0,
            "application/octet-stream", string.Empty, callerUserId, ct);

    public async Task<TenderResponse?> AttachChecklistFileAsync(int tenderId, int itemId,
        string filePath, string originalFileName, long fileSize, string contentType,
        string sha256, int callerUserId, CancellationToken ct = default)
    {
        var tender = await db.Tenders
            .Include(t => t.Customer)
            .Include(t => t.Preparer)
            .Include(t => t.ChecklistItems).ThenInclude(i => i.Owner)
            .FirstOrDefaultAsync(t => t.Id == tenderId, ct);
        var item = tender?.ChecklistItems.FirstOrDefault(i => i.Id == itemId);
        if (tender is null || item is null) return null;
        GuardChecklistMutable(tender);
        if (string.Equals(item.TemplateCode, "capability", StringComparison.OrdinalIgnoreCase))
            throw new TenderOperationException(
                "Hồ sơ năng lực phải chọn từ thư viện dùng chung, không tải file riêng cho từng gói thầu.");

        item.FilePath = filePath;
        item.OriginalFileName = originalFileName;
        item.CapabilityDocumentId = null;
        var now = DateTime.UtcNow;
        var evidence = new TenderChecklistEvidence
        {
            ChecklistItemId = item.Id,
            Revision = await NextEvidenceRevisionAsync(item.Id, ct),
            Source = TenderEvidenceSource.DirectUpload,
            FilePath = filePath,
            OriginalFileName = originalFileName,
            FileSize = fileSize,
            ContentType = contentType,
            Sha256 = sha256,
            CreatedByUserId = callerUserId,
            CreatedAt = now,
        };
        db.TenderChecklistEvidence.Add(evidence);
        item.CurrentEvidence = evidence;
        if (item.Status == TenderChecklistItemStatus.Submitted)
        {
            item.Status = TenderChecklistItemStatus.Done;
            item.VerifiedAt = null;
            item.VerifiedByUserId = null;
        }
        AutoAdvanceIfUnfinished(item, callerUserId, now);
        item.UpdatedAt = now;

        tender.UpdatedAt = now;
        tender.UpdatedByUserId = callerUserId;

        await db.SaveChangesAsync(ct);
        return MapDetail(tender);
    }

    public async Task<TenderChecklistFileReference?> GetChecklistFileAsync(
        int tenderId, int itemId, CancellationToken ct = default)
    {
        return await db.TenderChecklistItems
            .AsNoTracking()
            .Where(item => item.Id == itemId
                && item.TenderId == tenderId
                && item.FilePath != null
                && item.OriginalFileName != null)
            .Select(item => new TenderChecklistFileReference(item.FilePath!, item.OriginalFileName!))
            .SingleOrDefaultAsync(ct);
    }

    public Task<bool> IsChecklistFileAttachedAsync(
        int tenderId, int itemId, string filePath, CancellationToken ct = default)
    {
        return db.TenderChecklistItems.AsNoTracking().AnyAsync(
            item => item.TenderId == tenderId
                && item.Id == itemId
                && item.FilePath == filePath,
            ct);
    }

    public async Task<TenderResponse?> AttachInvitationDocumentAsync(int tenderId, string filePath,
        string originalFileName, long fileSize, string contentType, string sha256,
        int callerUserId, CancellationToken ct = default)
    {
        var tender = await db.Tenders.FirstOrDefaultAsync(item => item.Id == tenderId, ct);
        if (tender is null) return null;
        GuardChecklistMutable(tender);
        db.TenderInvitationDocuments.Add(new TenderInvitationDocument
        {
            TenderId = tenderId,
            FilePath = filePath,
            OriginalFileName = originalFileName,
            FileSize = fileSize,
            ContentType = contentType,
            Sha256 = sha256,
            CreatedByUserId = callerUserId,
            CreatedAt = DateTime.UtcNow,
        });
        tender.UpdatedAt = DateTime.UtcNow;
        tender.UpdatedByUserId = callerUserId;
        await db.SaveChangesAsync(ct);
        return await GetAsync(tenderId, ct);
    }

    public Task<TenderChecklistFileReference?> GetInvitationDocumentAsync(
        int tenderId, int documentId, CancellationToken ct = default) =>
        db.TenderInvitationDocuments.AsNoTracking()
            .Where(item => item.Id == documentId && item.TenderId == tenderId &&
                item.FilePath != null && item.OriginalFileName != null)
            .Select(item => new TenderChecklistFileReference(item.FilePath!, item.OriginalFileName!))
            .SingleOrDefaultAsync(ct);

    public Task<bool> IsInvitationDocumentAttachedAsync(
        int tenderId, string filePath, CancellationToken ct = default) =>
        db.TenderInvitationDocuments.AsNoTracking().AnyAsync(
            item => item.TenderId == tenderId && item.FilePath == filePath, ct);

    public async Task<TenderResponse?> AttachChecklistFromLibraryAsync(int tenderId,
        AttachTenderChecklistFromLibraryRequest request, int callerUserId, CancellationToken ct = default)
    {
        var tender = await db.Tenders.FirstOrDefaultAsync(t => t.Id == tenderId, ct);
        if (tender is null) return null;
        GuardChecklistMutable(tender);

        if (request.Items is null || request.Items.Count == 0)
        {
            throw new TenderOperationException("Danh sách file cần gán rỗng.");
        }

        var itemIds = request.Items.Select(i => i.ChecklistItemId).Distinct().ToList();
        var docIds = request.Items.Select(i => i.CapabilityDocumentId).Distinct().ToList();

        var items = await db.TenderChecklistItems
            .Where(i => itemIds.Contains(i.Id) && i.TenderId == tenderId)
            .ToListAsync(ct);
        if (items.Count != itemIds.Count)
        {
            throw new TenderOperationException("Có checklist item không thuộc gói thầu này.");
        }
        if (items.Any(item => !string.Equals(
                item.TemplateCode, "capability", StringComparison.OrdinalIgnoreCase)))
        {
            throw new TenderOperationException(
                "Chỉ Hồ sơ năng lực được chọn từ thư viện dùng chung; các hồ sơ khác phải tải file riêng cho gói thầu.");
        }

        var docs = await db.CapabilityDocuments
            .Where(d => docIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, ct);
        var missingDoc = docIds.FirstOrDefault(id => !docs.ContainsKey(id));
        if (missingDoc != 0)
        {
            throw new TenderOperationException($"Hồ sơ năng lực #{missingDoc} không tồn tại.");
        }

        var now = DateTime.UtcNow;
        foreach (var pair in request.Items)
        {
            var item = items.First(i => i.Id == pair.ChecklistItemId);
            var doc = docs[pair.CapabilityDocumentId];
            item.FilePath = doc.FilePath;
            item.OriginalFileName = doc.OriginalFileName;
            item.CapabilityDocumentId = doc.Id;
            var evidence = new TenderChecklistEvidence
            {
                ChecklistItemId = item.Id,
                Revision = await NextEvidenceRevisionAsync(item.Id, ct),
                Source = TenderEvidenceSource.CapabilityLibrary,
                FilePath = doc.FilePath,
                OriginalFileName = doc.OriginalFileName,
                FileSize = doc.FileSize,
                ContentType = doc.ContentType,
                CapabilityDocumentId = doc.Id,
                CapabilityDocumentVersion = doc.CurrentVersion,
                CreatedByUserId = callerUserId,
                CreatedAt = now,
            };
            db.TenderChecklistEvidence.Add(evidence);
            item.CurrentEvidence = evidence;
            if (item.Status == TenderChecklistItemStatus.Submitted)
            {
                item.Status = TenderChecklistItemStatus.Done;
                item.VerifiedAt = null;
                item.VerifiedByUserId = null;
            }
            AutoAdvanceIfUnfinished(item, callerUserId, now);
            item.UpdatedAt = now;
        }
        tender.UpdatedAt = now;
        tender.UpdatedByUserId = callerUserId;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Tender {TenderId} attached {Count} library docs by user {UserId}",
            tenderId, request.Items.Count, callerUserId);
        return await GetAsync(tenderId, ct);
    }

    public async Task<TenderResponse?> MarkWonAsync(int tenderId, MarkTenderWonRequest request,
        int callerUserId, CancellationToken ct = default)
    {
        if (request.CreateOpportunity == request.OpportunityId.HasValue)
        {
            throw new TenderOperationException(
                "Hãy chọn một cơ hội có sẵn của khách hàng, hoặc chọn tạo cơ hội mới từ gói thầu.");
        }
        if (!request.CreateOpportunity)
        {
            return await TransitionAsync(tenderId, new TransitionTenderRequest
            {
                Status = nameof(TenderStatus.Won),
                OpportunityId = request.OpportunityId,
                Note = request.Note,
            }, callerUserId, ct);
        }

        var tender = await db.Tenders.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenderId, ct);
        if (tender is null) return null;
        GuardNotTerminal(tender, "chuyển trạng thái");
        if (tender.Status != TenderStatus.Submitted)
        {
            throw new TenderOperationException($"Không thể chuyển gói thầu từ {tender.Status} sang {TenderStatus.Won}.");
        }

        // The opportunity and Won result land together or not at all. The
        // Tender already belongs to the customer project, so winning must not
        // create a duplicate project root.
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(ct)
            : null;
        var opportunity = await OpenOpportunityFromTenderAsync(tender, callerUserId, ct);
        var result = await TransitionAsync(tenderId, new TransitionTenderRequest
        {
            Status = nameof(TenderStatus.Won),
            OpportunityId = opportunity.Id,
            Note = request.Note,
        }, callerUserId, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        logger.LogInformation(
            "Tender {TenderId} won; opportunity {OpportunityId} continued in project {ProjectId} by user {UserId}",
            tenderId, opportunity.Id, opportunity.OperationalProjectId, callerUserId);
        return result;
    }

    /// <summary>
    /// A won tender skips prospecting and quoting: the bid was the proposal, so
    /// the opportunity opens in Negotiation, valued at the approved bid estimate,
    /// in the Tender's existing project so the contract can be raised without
    /// duplicating the NICON business root.
    /// </summary>
    private async Task<Opportunity> OpenOpportunityFromTenderAsync(
        Tender tender,
        int callerUserId,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var bidTotal = await db.TenderEstimateRevisions.AsNoTracking()
            .Where(revision => revision.TenderId == tender.Id &&
                revision.Status == TenderEstimateRevisionStatus.Approved)
            .OrderByDescending(revision => revision.Id)
            .Select(revision => (decimal?)revision.GrandBidTotal)
            .FirstOrDefaultAsync(ct) ?? 0m;

        if (!tender.OperationalProjectId.HasValue)
            throw new TenderOperationException("Gói thầu phải gắn với Dự án vận hành trước khi ghi nhận trúng thầu.");
        var opportunity = new Opportunity
        {
            Name = tender.Name,
            CustomerId = tender.CustomerId,
            OperationalProjectId = tender.OperationalProjectId,
            OwnerUserId = callerUserId,
            Stage = OpportunityStage.Negotiation,
            EstimatedValue = bidTotal,
            WinProbability = 75,
            Note = $"Tạo từ gói thầu trúng {tender.Code}.",
            CreatedAt = now,
            CreatedByUserId = callerUserId,
            UpdatedAt = now,
            UpdatedByUserId = callerUserId,
        };
        db.Opportunities.Add(opportunity);
        await db.SaveChangesAsync(ct);
        return opportunity;
    }

    public Task<TenderResponse?> MarkLostAsync(int tenderId, MarkTenderLostRequest request,
        int callerUserId, CancellationToken ct = default) => TransitionAsync(tenderId, new TransitionTenderRequest
        {
            Status = nameof(TenderStatus.Lost),
            ReasonCode = request.ReasonCode,
            Note = request.Note,
        }, callerUserId, ct);

    public async Task<TenderReadinessResponse?> GetReadinessAsync(
        int tenderId, int callerUserId, CancellationToken ct = default)
    {
        var tender = await db.Tenders.AsNoTracking()
            .Include(item => item.Milestones)
            .FirstOrDefaultAsync(item => item.Id == tenderId, ct);
        if (tender is null) return null;

        var blockers = new List<TenderReadinessBlockerResponse>();
        void Block(string code, string area, string message, string tab) => blockers.Add(new()
        {
            Code = code,
            Area = area,
            Message = message,
            ActionPath = $"/admin/tenders/{tenderId}?tab={tab}",
        });

        if (!tender.InvitationReceivedAt.HasValue || string.IsNullOrWhiteSpace(tender.InvitationSource) ||
            tender.InvitationReceivedByUserId <= 0)
            Block("INVITATION_METADATA_REQUIRED", "Invitation", "Chưa ghi đủ ngày, nguồn và người tiếp nhận HSMT.", "info");
        if (!tender.OperationalProjectId.HasValue)
            Block("OPERATIONAL_PROJECT_REQUIRED", "Overview", "Gói thầu chưa gắn Dự án vận hành.", "info");

        var plans = await db.DepartmentWorkPackages.AsNoTracking()
            .Where(package => package.SourceTenderId == tenderId &&
                (package.Purpose == WorkPackagePurpose.TenderPlanning || package.Purpose == WorkPackagePurpose.General) &&
                package.Status != WorkPackageStatus.Cancelled)
            .Select(package => new { package.Id, package.Name, package.Status })
            .ToListAsync(ct);
        var planningReady = plans.Count > 0 && plans.All(package => package.Status == WorkPackageStatus.Completed);
        if (plans.Count == 0)
            Block("TENDER_PLAN_REQUIRED", "Planning", "Chưa có Kế hoạch đấu thầu theo phòng ban.", "plan");
        else
        {
            var incomplete = plans.Where(package => package.Status != WorkPackageStatus.Completed).ToList();
            if (incomplete.Count > 0)
                Block("TENDER_PLAN_INCOMPLETE", "Planning",
                    $"Còn {incomplete.Count} nhóm việc kế hoạch chưa hoàn thành/xác nhận.", "plan");
        }

        var checklist = await db.TenderChecklistItems.AsNoTracking()
            .Where(item => item.TenderId == tenderId)
            .ToListAsync(ct);
        var required = checklist.Where(item => item.IsRequired).ToList();
        if (required.Count == 0)
            Block("DOSSIER_REQUIRED", "Dossier", "Checklist chưa có mục hồ sơ bắt buộc.", "checklist");
        foreach (var item in required)
        {
            if (!HasRequiredChecklistEvidence(item))
                Block("DOSSIER_EVIDENCE_MISSING", "Dossier", $"{item.Title} chưa có bằng chứng hợp lệ.", "checklist");
            else if (item.Status == TenderChecklistItemStatus.Done)
                Block("DOSSIER_NOT_VERIFIED", "Dossier",
                    $"{item.Title} đã hoàn thành nhưng chưa được người khác xác nhận độc lập.", "checklist");
            else if (item.Status != TenderChecklistItemStatus.Submitted || !item.VerifiedAt.HasValue ||
                     !item.VerifiedByUserId.HasValue)
                Block("DOSSIER_NOT_COMPLETED", "Dossier", $"{item.Title} chưa hoàn thành/xác nhận.", "checklist");
        }
        var dossierReady = required.Count > 0 && !blockers.Any(item => item.Area == "Dossier");

        var approvedEstimate = await db.TenderEstimateRevisions.AsNoTracking()
            .Where(revision => revision.TenderId == tenderId && revision.Status == TenderEstimateRevisionStatus.Approved)
            .OrderByDescending(revision => revision.Id).Select(revision => (int?)revision.Id).FirstOrDefaultAsync(ct);
        if (!approvedEstimate.HasValue)
            Block("ESTIMATE_NOT_APPROVED", "Estimate", "Chưa có revision dự toán được phê duyệt.", "estimate");

        var financeApproved = tender.OperationalProjectId.HasValue && await db.DepartmentWorkPackages.AsNoTracking()
            .AnyAsync(package => package.SourceTenderId == tenderId &&
                package.OperationalProjectId == tender.OperationalProjectId &&
                package.Purpose == WorkPackagePurpose.FinanceTenderReview &&
                package.FinanceReview != null && package.FinanceReview.Status == FinanceReviewStatus.Approved, ct);
        if (!financeApproved)
            Block("FINANCE_REVIEW_NOT_APPROVED", "Finance", "Rà soát tài chính/bảo lãnh đang chờ TC–KT phê duyệt.", "plan");

        var internalReviewReady = approvedEstimate.HasValue && financeApproved;
        var achieved = new List<TenderMilestoneKind>();
        if (planningReady) achieved.Add(TenderMilestoneKind.PlanningReady);
        if (dossierReady) achieved.Add(TenderMilestoneKind.DossierReady);
        if (internalReviewReady) achieved.Add(TenderMilestoneKind.InternalReviewReady);
        if (blockers.Count == 0) achieved.Add(TenderMilestoneKind.ReadyToSubmit);
        var existingKinds = tender.Milestones.Select(item => item.Kind).ToHashSet();
        var now = DateTime.UtcNow;
        foreach (var kind in achieved.Where(kind => !existingKinds.Contains(kind)))
            db.TenderMilestones.Add(new TenderMilestone
            {
                TenderId = tenderId,
                Kind = kind,
                AchievedAt = now,
                AchievedByUserId = callerUserId,
            });
        if (db.ChangeTracker.HasChanges())
        {
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException exception) when (SequentialCodes.IsUniqueViolation(exception))
            {
                // Another request observed the same first-achieved gate. The
                // unique (TenderId, Kind) key keeps one canonical milestone.
                db.ChangeTracker.Clear();
            }
        }

        var milestones = await db.TenderMilestones.AsNoTracking().Where(item => item.TenderId == tenderId)
            .OrderBy(item => item.AchievedAt).Select(item => new TenderMilestoneResponse
            {
                Kind = item.Kind.ToString(),
                AchievedAt = item.AchievedAt,
                AchievedByUserId = item.AchievedByUserId,
            }).ToListAsync(ct);
        var currentStep = blockers.Count == 0 ? "ReadyToSubmit" :
            !planningReady ? "Planning" : !dossierReady ? "Dossier" : "InternalReview";
        return new TenderReadinessResponse
        {
            TenderId = tenderId,
            CurrentStep = currentStep,
            CanSubmit = blockers.Count == 0 && tender.Status is TenderStatus.Preparing or TenderStatus.Submitted,
            Blockers = blockers,
            Milestones = milestones,
        };
    }

    public async Task<TenderResponse?> SubmitAsync(
        int tenderId, SubmitTenderRequest request, int callerUserId, CancellationToken ct = default)
    {
        var channel = TrimOrNull(request.SubmissionChannel)
            ?? throw new TenderOperationException("Kênh nộp thầu là bắt buộc, ví dụ: Cổng đấu thầu quốc gia.");
        var readiness = await GetReadinessAsync(tenderId, callerUserId, ct);
        if (readiness is null) return null;
        if (!readiness.CanSubmit)
            throw new TenderOperationException(string.Join(" ", readiness.Blockers.Select(item => item.Message)));

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct)
            : null;
        var tender = await db.Tenders.FirstOrDefaultAsync(item => item.Id == tenderId, ct);
        if (tender is null) return null;
        if (tender.Status is not (TenderStatus.Preparing or TenderStatus.Submitted))
            throw new TenderOperationException("Chỉ gói thầu đang chuẩn bị hoặc đã nộp mới được tạo lần nộp bổ sung.");
        CrmConcurrency.Apply(db, tender, request.RowVersion);

        var checklist = await db.TenderChecklistItems.AsNoTracking()
            .Include(item => item.CurrentEvidence)
            .Where(item => item.TenderId == tenderId && (item.IsRequired ||
                item.Status == TenderChecklistItemStatus.Submitted || item.Status == TenderChecklistItemStatus.NotApplicable))
            .ToListAsync(ct);
        var estimate = await db.TenderEstimateRevisions.AsNoTracking()
            .Where(item => item.TenderId == tenderId && item.Status == TenderEstimateRevisionStatus.Approved)
            .OrderByDescending(item => item.Id).FirstAsync(ct);
        var planSnapshot = await db.DepartmentWorkPackages.AsNoTracking()
            .Where(item => item.SourceTenderId == tenderId && item.Status != WorkPackageStatus.Cancelled &&
                (item.Purpose == WorkPackagePurpose.TenderPlanning || item.Purpose == WorkPackagePurpose.General))
            .Select(item => new
            {
                SchemaVersion = 1,
                item.Id,
                item.Code,
                item.Name,
                item.Description,
                item.Status,
                item.CompletedAt,
                item.OperationalProjectId,
                item.ContractId,
                item.DepartmentRoleGroupId,
                item.LeadUserId,
                item.PlannedStart,
                item.PlannedEnd,
                item.UpdatedAt,
                item.UpdatedByUserId,
                item.RowVersion,
                Tasks = item.Tasks.OrderBy(task => task.Id).Select(task => new
                {
                    task.Id,
                    task.WbsCode,
                    task.Title,
                    task.Description,
                    task.Resources,
                    task.DepartmentRoleGroupId,
                    task.AssigneeUserId,
                    task.ApproverUserId,
                    task.Status,
                    task.ProgressPercent,
                    task.PlannedStart,
                    task.PlannedEnd,
                    task.ActualStart,
                    task.CompletedAt,
                    task.VerifiedAt,
                    task.VerifiedByUserId,
                    task.UpdatedAt,
                    task.UpdatedByUserId,
                    task.RowVersion,
                    CcUserIds = task.CcUsers.OrderBy(cc => cc.UserId).Select(cc => cc.UserId).ToArray(),
                    PredecessorIds = task.Predecessors.OrderBy(link => link.PredecessorTaskId)
                        .Select(link => link.PredecessorTaskId).ToArray(),
                }).ToArray(),
            })
            .ToListAsync(ct);
        var finance = await db.DepartmentWorkPackages.AsNoTracking()
            .Where(item => item.SourceTenderId == tenderId && item.OperationalProjectId == tender.OperationalProjectId &&
                item.Purpose == WorkPackagePurpose.FinanceTenderReview && item.FinanceReview != null &&
                item.FinanceReview.Status == FinanceReviewStatus.Approved)
            .OrderByDescending(item => item.Id)
            .Select(item => new
            {
                SchemaVersion = 1,
                item.Id,
                item.Code,
                item.Name,
                item.DepartmentRoleGroupId,
                item.LeadUserId,
                item.OperationalProjectId,
                item.ContractId,
                item.SourceTenderId,
                item.UpdatedAt,
                item.UpdatedByUserId,
                item.RowVersion,
                item.FinanceReview!.Status,
                item.FinanceReview.ReviewedAt,
                item.FinanceReview.ReviewedByUserId,
                ReviewId = item.FinanceReview.Id,
                item.FinanceReview.ContractConditionsReviewed,
                item.FinanceReview.GuaranteesReviewed,
                item.FinanceReview.PaymentTermsReviewed,
                item.FinanceReview.FinancialDossierReviewed,
                item.FinanceReview.Findings,
                item.FinanceReview.DecisionReason,
                item.FinanceReview.SubmittedAt,
                ReviewUpdatedAt = item.FinanceReview.UpdatedAt,
                ReviewUpdatedByUserId = item.FinanceReview.UpdatedByUserId,
                ReviewRowVersion = item.FinanceReview.RowVersion,
            })
            .FirstAsync(ct);
        var submittedAt = DateTime.UtcNow;
        var submission = new TenderSubmission
        {
            TenderId = tenderId,
            Sequence = (await db.TenderSubmissions.Where(item => item.TenderId == tenderId)
                .Select(item => (int?)item.Sequence).MaxAsync(ct) ?? 0) + 1,
            SubmittedAt = submittedAt,
            SubmittedByUserId = callerUserId,
            ApprovedEstimateRevisionId = estimate.Id,
            FinanceReviewWorkPackageId = finance.Id,
            SubmissionChannel = channel,
            ReceiptReference = TrimOrNull(request.ReceiptReference),
            PlanningSnapshotJson = System.Text.Json.JsonSerializer.Serialize(planSnapshot),
            EstimateSnapshotJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                estimate.Id,
                estimate.VersionNumber,
                estimate.SourceSha256,
                estimate.GrandBidTotal,
                estimate.Currency,
            }),
            FinanceReviewSnapshotJson = System.Text.Json.JsonSerializer.Serialize(finance),
            Items = checklist.Select(item => new TenderSubmissionItem
            {
                ChecklistItemId = item.Id,
                EvidenceId = item.CurrentEvidenceId,
                TemplateCode = item.TemplateCode,
                Title = item.Title,
                IsRequired = item.IsRequired,
                Status = item.Status,
                FilePath = item.CurrentEvidence?.FilePath ?? item.FilePath,
                OriginalFileName = item.CurrentEvidence?.OriginalFileName ?? item.OriginalFileName,
                CapabilityDocumentId = item.CurrentEvidence?.CapabilityDocumentId ?? item.CapabilityDocumentId,
                CapabilityDocumentVersion = item.CurrentEvidence?.CapabilityDocumentVersion,
                Sha256 = item.CurrentEvidence?.Sha256,
                NotApplicableReason = item.NotApplicableReason,
            }).ToList(),
        };
        db.TenderSubmissions.Add(submission);
        tender.Status = TenderStatus.Submitted;
        tender.SubmittedAt ??= submittedAt;
        tender.Note = TrimOrNull(request.Note) ?? tender.Note;
        tender.UpdatedAt = submittedAt;
        tender.UpdatedByUserId = callerUserId;
        await CrmConcurrency.SaveChangesAsync(db, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return await GetAsync(tenderId, ct);
    }

    public async Task<TenderResponse?> TransitionAsync(int tenderId, TransitionTenderRequest request,
        int callerUserId, CancellationToken ct = default)
    {
        var tender = await db.Tenders.FirstOrDefaultAsync(t => t.Id == tenderId, ct);
        if (tender is null) return null;
        GuardNotTerminal(tender, "chuyển trạng thái");
        if (!Enum.TryParse<TenderStatus>(request.Status, true, out var nextStatus))
        {
            throw new TenderOperationException($"Trạng thái gói thầu '{request.Status}' không hợp lệ.");
        }
        if (nextStatus == TenderStatus.Submitted)
        {
            throw new TenderOperationException(
                "Nộp thầu chỉ được thực hiện qua endpoint /submit có Idempotency-Key và RowVersion.");
        }
        var allowed = tender.Status switch
        {
            TenderStatus.Preparing => nextStatus == TenderStatus.Cancelled,
            TenderStatus.Submitted => nextStatus is TenderStatus.Won or TenderStatus.Lost or TenderStatus.Cancelled,
            _ => false,
        };
        if (!allowed)
        {
            throw new TenderOperationException($"Không thể chuyển gói thầu từ {tender.Status} sang {nextStatus}.");
        }

        if (nextStatus == TenderStatus.Won)
        {
            var opportunity = request.OpportunityId.HasValue
                ? await db.Opportunities.FirstOrDefaultAsync(item => item.Id == request.OpportunityId.Value, ct)
                : null;
            if (opportunity is null || opportunity.CustomerId != tender.CustomerId ||
                !tender.OperationalProjectId.HasValue ||
                opportunity.OperationalProjectId != tender.OperationalProjectId ||
                opportunity.Stage is OpportunityStage.Won or OpportunityStage.Lost)
            {
                throw new TenderOperationException(
                    $"Cơ hội #{request.OpportunityId} phải đang hoạt động và thuộc đúng khách hàng, Dự án của gói thầu.");
            }
            opportunity.Stage = OpportunityStage.Negotiation;
            opportunity.ClosedAt = null;
            opportunity.LostReasonCode = null;
            opportunity.LostNote = null;
            opportunity.UpdatedAt = DateTime.UtcNow;
            opportunity.UpdatedByUserId = callerUserId;
            tender.WonOpportunityId = opportunity.Id;
            tender.LostReasonCode = null;
            tender.LostNote = null;
        }
        else if (nextStatus == TenderStatus.Lost)
        {
            var reasonCode = (request.ReasonCode ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(reasonCode))
            {
                throw new TenderOperationException("Vui lòng chọn lý do trượt thầu.");
            }
            if (!await db.MasterDataOptions.AnyAsync(option => option.Category == "opportunity_lost_reason" &&
                    option.Code == reasonCode && option.IsActive, ct))
            {
                throw new TenderOperationException($"Lý do '{reasonCode}' không hợp lệ.");
            }
            tender.LostReasonCode = reasonCode;
            tender.LostNote = TrimOrNull(request.Note);
            tender.WonOpportunityId = null;
        }

        var previousStatus = tender.Status;
        tender.Status = nextStatus;
        if (!string.IsNullOrWhiteSpace(request.Note) && nextStatus != TenderStatus.Lost)
        {
            tender.Note = request.Note.Trim();
        }
        var now = DateTime.UtcNow;
        tender.ClosedAt = nextStatus is TenderStatus.Won or TenderStatus.Lost or TenderStatus.Cancelled ? now : null;
        tender.UpdatedAt = now;
        tender.UpdatedByUserId = callerUserId;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Tender {TenderId} transitioned from {PreviousStatus} to {NextStatus} by user {UserId}",
            tenderId, previousStatus, nextStatus, callerUserId);
        return await GetAsync(tenderId, ct);
    }

    public async Task<List<TenderTimelineEvent>?> GetTimelineAsync(int tenderId, int limit, CancellationToken ct = default)
    {
        var exists = await db.Tenders.AsNoTracking().AnyAsync(t => t.Id == tenderId, ct);
        if (!exists) return null;

        if (limit < 1) limit = 1;
        if (limit > 500) limit = 500;

        var idText = tenderId.ToString();
        var rows = await db.AuditLogs
            .AsNoTracking()
            .Where(a => a.ResourceType == EntityTypes.Tender && a.ResourceId == idText)
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .Select(a => new
            {
                a.Id,
                a.CreatedAt,
                a.Action,
                a.Message,
                a.ActorUserId,
                UserName = a.ActorUserId != null
                    ? db.Users.Where(u => u.Id == a.ActorUserId).Select(u => u.FullName).FirstOrDefault()
                    : null,
            })
            .ToListAsync(ct);

        return rows.Select(a => new TenderTimelineEvent
        {
            Id = a.Id,
            OccurredAt = a.CreatedAt,
            Action = a.Action,
            Message = a.Message,
            UserId = a.ActorUserId,
            UserName = a.UserName,
        }).ToList();
    }

    private static void AutoAdvanceIfUnfinished(TenderChecklistItem item, int callerUserId, DateTime now)
    {
        // Attaching a file also completes the row when it's still in an
        // unfinished state — matches the AC "upload file → % checklist tăng".
        if (item.Status is TenderChecklistItemStatus.NotStarted or TenderChecklistItemStatus.Preparing)
        {
            item.Status = TenderChecklistItemStatus.Done;
            item.CompletedAt = now;
            item.CompletedByUserId = callerUserId;
            item.NotApplicableReason = null;
        }
    }

    private static void GuardNotTerminal(Tender tender, string action)
    {
        if (tender.Status is TenderStatus.Won or TenderStatus.Lost or TenderStatus.Cancelled)
        {
            throw new TenderOperationException($"Gói thầu đã ở trạng thái kết thúc, không thể {action}.");
        }
    }

    private static void GuardChecklistMutable(Tender tender)
    {
        if (tender.Status is TenderStatus.Won or TenderStatus.Lost or TenderStatus.Cancelled)
        {
            throw new TenderOperationException(
                "Gói thầu đã kết thúc, không thể chuẩn bị hồ sơ bổ sung.");
        }
    }

    private static bool HasRequiredChecklistEvidence(TenderChecklistItem item)
    {
        var hasFile = !string.IsNullOrWhiteSpace(item.FilePath) &&
            !string.IsNullOrWhiteSpace(item.OriginalFileName);
        return string.Equals(item.TemplateCode, "capability", StringComparison.OrdinalIgnoreCase)
            ? item.CapabilityDocumentId.HasValue && hasFile
            : !item.CapabilityDocumentId.HasValue && hasFile;
    }

    private static void EnsureChecklistEvidence(TenderChecklistItem item)
    {
        if (!HasRequiredChecklistEvidence(item)) throw MissingChecklistEvidence(item);
    }

    private static TenderOperationException MissingChecklistEvidence(TenderChecklistItem item)
    {
        return string.Equals(item.TemplateCode, "capability", StringComparison.OrdinalIgnoreCase)
            ? new TenderOperationException(
                "Hồ sơ năng lực phải được chọn từ thư viện dùng chung trước khi đánh dấu hoàn tất.")
            : new TenderOperationException(
                $"Mục hồ sơ '{item.Title}' phải có file tải riêng cho gói thầu trước khi đánh dấu hoàn tất.");
    }

    // ------------------------------ Helpers ---------------------------------

    private static string? TrimOrNull(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private async Task<int> NextEvidenceRevisionAsync(int checklistItemId, CancellationToken ct) =>
        (await db.TenderChecklistEvidence.Where(item => item.ChecklistItemId == checklistItemId)
            .Select(item => (int?)item.Revision).MaxAsync(ct) ?? 0) + 1;

    private static string? ValidateOptionalHttpUrl(string? value)
    {
        var normalized = TrimOrNull(value);
        if (normalized is null) return null;
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new TenderOperationException("URL cổng đấu thầu phải dùng http hoặc https, ví dụ: https://muasamcong.mpi.gov.vn/.");
        return normalized;
    }

    private static void ValidateInvitation(DateTime? receivedAt, DateTime submissionDeadline)
    {
        if (!receivedAt.HasValue)
            throw new TenderOperationException("Ngày nhận hồ sơ mời thầu là bắt buộc.");
        if (receivedAt.Value > DateTime.UtcNow.AddMinutes(5))
            throw new TenderOperationException("Ngày nhận hồ sơ mời thầu không được ở tương lai.");
        if (receivedAt.Value >= submissionDeadline)
            throw new TenderOperationException("Ngày nhận hồ sơ mời thầu phải trước deadline nộp thầu.");
    }

    private static void ValidateChecklistDeadline(DateTime? deadline, DateTime submissionDeadline)
    {
        if (!deadline.HasValue) return;
        if (deadline.Value.Date < DateTime.UtcNow.Date)
            throw new TenderOperationException("Deadline nội bộ không được trước ngày hiện tại.");
        if (deadline.Value.Date > submissionDeadline.Date)
            throw new TenderOperationException("Deadline nội bộ không được sau deadline nộp thầu.");
    }

    private async Task EnsureEligibleChecklistAssigneeAsync(int userId, CancellationToken ct)
    {
        var eligible = await db.Users.AnyAsync(user =>
            user.Id == userId && user.IsActive &&
            user.RoleEntity != null && user.RoleEntity.IsActive &&
            user.RoleEntity.RolePermissions.Any(grant =>
                grant.Permission.Module == "crm.tenders" && grant.Permission.Action == "manage"), ct);
        if (!eligible)
            throw new TenderOperationException(
                $"Người phụ trách #{userId} không hoạt động hoặc không có quyền quản lý gói thầu.");
    }

    private static int ComputePercent(int done, int total) =>
        total <= 0 ? 0 : (int)Math.Round(100.0 * done / total);

    private static bool IsDeadlineImminent(TenderStatus status, DateTime deadline, DateTime now)
    {
        if (status is TenderStatus.Won or TenderStatus.Lost or TenderStatus.Cancelled) return false;
        var daysLeft = (deadline - now).TotalDays;
        return daysLeft <= DeadlineImminentDays;
    }

    private async Task SeedDefaultChecklistAsync(int tenderId, CancellationToken ct)
    {
        var templates = await db.MasterDataOptions
            .AsNoTracking()
            .Where(o => o.Category == ChecklistTemplateCategory && o.IsActive)
            .OrderBy(o => o.SortOrder)
            .ThenBy(o => o.Name)
            .ToListAsync(ct);

        if (templates.Count == 0)
        {
            logger.LogWarning("Tender {TenderId} created but no default checklist templates seeded", tenderId);
            return;
        }

        var now = DateTime.UtcNow;
        var items = templates.Select((tpl, idx) => new TenderChecklistItem
        {
            TenderId = tenderId,
            TemplateCode = tpl.Code,
            Title = tpl.Name,
            Status = TenderChecklistItemStatus.NotStarted,
            IsRequired = !string.Equals(tpl.Code, "drawings", StringComparison.OrdinalIgnoreCase),
            DocumentKind = tpl.Code.ToLowerInvariant() switch
            {
                "capability" => TenderDocumentKind.Capability,
                "legal" => TenderDocumentKind.Legal,
                "bid-security" => TenderDocumentKind.BidSecurity,
                "boq" => TenderDocumentKind.Boq,
                "method-statement" => TenderDocumentKind.MethodStatement,
                "drawings" => TenderDocumentKind.Drawings,
                "schedule" => TenderDocumentKind.Schedule,
                _ => TenderDocumentKind.Custom,
            },
            SortOrder = tpl.SortOrder != 0 ? tpl.SortOrder : idx + 1,
            CreatedAt = now,
            UpdatedAt = now,
        }).ToList();
        db.TenderChecklistItems.AddRange(items);
        await db.SaveChangesAsync(ct);
    }

    private async Task NotifyAssigneeAsync(Tender tender, bool isReassignment, CancellationToken _)
    {
        if (!tender.PreparerUserId.HasValue) return;
        try
        {
            var title = isReassignment
                ? $"Bạn được gán lại phụ trách gói thầu {tender.Code}"
                : $"Bạn được gán phụ trách gói thầu {tender.Code}";
            var body = $"Gói thầu: {tender.Name}. Deadline nộp: {tender.SubmissionDeadline:dd/MM/yyyy HH:mm}.";
            var linkUrl = $"/admin/tenders/{tender.Id}";
            await notifications.CreateAsync(tender.PreparerUserId.Value, "crm.tenders.assigned", title, body, linkUrl);
        }
        catch (Exception ex)
        {
            // Notification failure must not break the CRUD write path.
            logger.LogWarning(ex, "Failed to notify preparer {UserId} about tender {TenderId}",
                tender.PreparerUserId, tender.Id);
        }
    }

    private static TenderResponse MapDetail(Tender t,
        string? wonOpportunityName = null,
        string? lostReasonLabel = null)
    {
        var now = DateTime.UtcNow;
        var requiredItems = t.ChecklistItems.Where(i => i.IsRequired).ToList();
        var doneCount = requiredItems.Count(i =>
            i.Status == TenderChecklistItemStatus.Done || i.Status == TenderChecklistItemStatus.Submitted);
        var totalCount = requiredItems.Count;
        var percent = ComputePercent(doneCount, totalCount);

        return new TenderResponse
        {
            Id = t.Id,
            Code = t.Code,
            Name = t.Name,
            CustomerId = t.CustomerId,
            CustomerName = t.Customer?.Name ?? string.Empty,
            OperationalProjectId = t.OperationalProjectId,
            OperationalProjectCode = t.OperationalProject?.Code,
            OperationalProjectName = t.OperationalProject?.Name,
            OpeningDate = t.OpeningDate,
            InvitationReceivedAt = t.InvitationReceivedAt,
            InvitationReference = t.InvitationReference,
            InvitationSource = t.InvitationSource,
            InvitationReceivedByUserId = t.InvitationReceivedByUserId,
            InvitationReceivedByName = t.InvitationReceivedByUser?.FullName,
            InvitationPortalUrl = t.InvitationPortalUrl,
            SubmissionDeadline = t.SubmissionDeadline,
            PreparerUserId = t.PreparerUserId,
            PreparerName = t.Preparer?.FullName,
            InfoSource = t.InfoSource,
            Status = t.Status.ToString(),
            Note = t.Note,
            WonOpportunityId = t.WonOpportunityId,
            WonOpportunityName = wonOpportunityName,
            LostReasonCode = t.LostReasonCode,
            LostReasonLabel = lostReasonLabel,
            LostNote = t.LostNote,
            ClosedAt = t.ClosedAt,
            SubmittedAt = t.SubmittedAt,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt,
            RowVersion = CrmConcurrency.Encode(t.RowVersion),
            ChecklistItems = t.ChecklistItems
                .OrderBy(i => i.SortOrder)
                .Select(i => new TenderChecklistItemResponse
                {
                    Id = i.Id,
                    TemplateCode = i.TemplateCode,
                    Title = i.Title,
                    Status = i.Status.ToString(),
                    IsRequired = i.IsRequired,
                    NotApplicableReason = i.NotApplicableReason,
                    DocumentKind = i.DocumentKind.ToString(),
                    OwnerUserId = i.OwnerUserId,
                    OwnerName = i.Owner?.FullName,
                    InternalDeadline = i.InternalDeadline,
                    FilePath = i.FilePath,
                    OriginalFileName = i.OriginalFileName,
                    CapabilityDocumentId = i.CapabilityDocumentId,
                    SortOrder = i.SortOrder,
                    CompletedAt = i.CompletedAt,
                    CompletedByUserId = i.CompletedByUserId,
                    VerifiedAt = i.VerifiedAt,
                    VerifiedByUserId = i.VerifiedByUserId,
                    WorkTaskId = i.WorkTaskId,
                    CurrentEvidenceId = i.CurrentEvidenceId,
                    EvidenceRevisions = i.EvidenceRevisions.OrderByDescending(evidence => evidence.Revision)
                        .Select(evidence => new TenderChecklistEvidenceResponse
                        {
                            Id = evidence.Id,
                            Revision = evidence.Revision,
                            Source = evidence.Source.ToString(),
                            OriginalFileName = evidence.OriginalFileName,
                            FileSize = evidence.FileSize,
                            ContentType = evidence.ContentType,
                            Sha256 = evidence.Sha256,
                            CapabilityDocumentId = evidence.CapabilityDocumentId,
                            CapabilityDocumentVersion = evidence.CapabilityDocumentVersion,
                            CreatedByUserId = evidence.CreatedByUserId,
                            CreatedAt = evidence.CreatedAt,
                        }).ToList(),
                }).ToList(),
            Submissions = t.Submissions
                .OrderByDescending(item => item.Sequence)
                .Select(item => new TenderSubmissionResponse
                {
                    Id = item.Id,
                    Sequence = item.Sequence,
                    SubmittedAt = item.SubmittedAt,
                    SubmittedByUserId = item.SubmittedByUserId,
                    SubmittedByName = item.SubmittedBy?.FullName,
                    ApprovedEstimateRevisionId = item.ApprovedEstimateRevisionId,
                    FinanceReviewWorkPackageId = item.FinanceReviewWorkPackageId,
                    SubmissionChannel = item.SubmissionChannel,
                    ReceiptReference = item.ReceiptReference,
                    LegacyUnknown = item.LegacyUnknown,
                    ItemCount = item.Items.Count,
                    Items = item.Items.Select(snapshot => new TenderSubmissionItemResponse
                    {
                        ChecklistItemId = snapshot.ChecklistItemId,
                        TemplateCode = snapshot.TemplateCode,
                        Title = snapshot.Title,
                        IsRequired = snapshot.IsRequired,
                        Status = snapshot.Status.ToString(),
                        FilePath = snapshot.FilePath,
                        OriginalFileName = snapshot.OriginalFileName,
                        CapabilityDocumentId = snapshot.CapabilityDocumentId,
                        EvidenceId = snapshot.EvidenceId,
                        CapabilityDocumentVersion = snapshot.CapabilityDocumentVersion,
                        Sha256 = snapshot.Sha256,
                        NotApplicableReason = snapshot.NotApplicableReason,
                    }).ToList(),
                }).ToList(),
            InvitationDocuments = t.InvitationDocuments.OrderByDescending(item => item.CreatedAt)
                .Select(item => new TenderInvitationDocumentResponse
                {
                    Id = item.Id,
                    OriginalFileName = item.OriginalFileName,
                    FileSize = item.FileSize,
                    ContentType = item.ContentType,
                    ExternalUrl = item.ExternalUrl,
                    CreatedByUserId = item.CreatedByUserId,
                    CreatedAt = item.CreatedAt,
                }).ToList(),
            ChecklistCompletionPercent = percent,
            IsDeadlineImminent = IsDeadlineImminent(t.Status, t.SubmissionDeadline, now) && percent < 100,
            Milestones = t.Milestones.OrderBy(item => item.AchievedAt).Select(item => new TenderMilestoneResponse
            {
                Kind = item.Kind.ToString(),
                AchievedAt = item.AchievedAt,
                AchievedByUserId = item.AchievedByUserId,
            }).ToList(),
        };
    }

    private async Task ValidateTenderProjectAsync(int? projectId, int customerId, CancellationToken ct)
    {
        if (!projectId.HasValue) return;
        if (!await db.OperationalProjects.AsNoTracking().AnyAsync(project =>
                project.Id == projectId.Value && project.CustomerId == customerId, ct))
            throw new TenderOperationException("Dự án vận hành không tồn tại hoặc không thuộc khách hàng của gói thầu.");
    }
}
