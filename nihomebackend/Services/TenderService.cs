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
///   every new tender ships with the 6 standard items.
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

    public async Task<TenderListResponse> ListAsync(TenderListParams p, CancellationToken ct = default)
    {
        var page = p.Page < 1 ? 1 : p.Page;
        var pageSize = Math.Clamp(p.PageSize <= 0 ? 20 : p.PageSize, 1, MaxPageSize);

        var q = db.Tenders
            .AsNoTracking()
            .Include(t => t.Customer)
            .Include(t => t.Preparer)
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
                    && (i.Status == TenderChecklistItemStatus.Done || i.Status == TenderChecklistItemStatus.Submitted)),
                TotalCount = db.TenderChecklistItems.Count(i => i.TenderId == t.Id),
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
            .Include(t => t.ChecklistItems).ThenInclude(i => i.Owner)
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

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId, ct)
            ?? throw new TenderOperationException($"Khách hàng #{request.CustomerId} không tồn tại.");

        if (request.PreparerUserId.HasValue &&
            !await db.Users.AnyAsync(u => u.Id == request.PreparerUserId.Value, ct))
        {
            throw new TenderOperationException($"Người phụ trách #{request.PreparerUserId} không tồn tại.");
        }

        // Default the preparer to the caller when the client didn't pick one.
        // Sales users don't have users.view so the FE picker isn't visible
        // to them — auto-assignment keeps the tender owned by a real person.
        var effectivePreparerId = request.PreparerUserId ?? callerUserId;

        var codePrefix = $"TD-{DateTime.UtcNow.Year}-";
        var entity = new Tender
        {
            Code = await SequentialCodes.NextAsync(db.Tenders.Select(t => t.Code), codePrefix, 4, ct),
            Name = name,
            CustomerId = customer.Id,
            OpeningDate = request.OpeningDate,
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

        if (request.PreparerUserId.HasValue &&
            !await db.Users.AnyAsync(u => u.Id == request.PreparerUserId.Value, ct))
        {
            throw new TenderOperationException($"Người phụ trách #{request.PreparerUserId} không tồn tại.");
        }

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
            entity.SubmissionDeadline = request.SubmissionDeadline;
            entity.PreparerUserId = request.PreparerUserId;
            entity.InfoSource = TrimOrNull(request.InfoSource);
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

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<TenderChecklistItemStatus>(request.Status, ignoreCase: true, out var next))
            {
                throw new TenderOperationException($"Trạng thái checklist '{request.Status}' không hợp lệ.");
            }
            item.Status = next;
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

    public async Task<TenderResponse?> AttachChecklistFileAsync(int tenderId, int itemId,
        string filePath, string originalFileName, int callerUserId, CancellationToken ct = default)
    {
        var tender = await db.Tenders
            .Include(t => t.Customer)
            .Include(t => t.Preparer)
            .Include(t => t.ChecklistItems).ThenInclude(i => i.Owner)
            .FirstOrDefaultAsync(t => t.Id == tenderId, ct);
        var item = tender?.ChecklistItems.FirstOrDefault(i => i.Id == itemId);
        if (tender is null || item is null) return null;
        GuardChecklistMutable(tender);

        item.FilePath = filePath;
        item.OriginalFileName = originalFileName;
        item.CapabilityDocumentId = null;
        AutoAdvanceIfUnfinished(item);
        var now = DateTime.UtcNow;
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
            AutoAdvanceIfUnfinished(item);
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

        // The opportunity, its project and the Won result land together or not at all.
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
            "Tender {TenderId} won; opportunity {OpportunityId} and project {ProjectCode} opened by user {UserId}",
            tenderId, opportunity.Id, opportunity.OperationalProject?.Code, callerUserId);
        return result;
    }

    /// <summary>
    /// A won tender skips prospecting and quoting: the bid was the proposal, so
    /// the opportunity opens in Negotiation, valued at the approved bid estimate,
    /// with a Planning project so the contract can be raised straight away.
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

        await OperationalProjectCodeAllocator.AcquireLockAsync(db, now.Year, ct);
        var projectName = $"Dự án {tender.Name}";
        var project = new OperationalProject
        {
            Code = await OperationalProjectCodeAllocator.NextCodeAsync(db, now.Year, ct),
            Name = projectName.Length > 300 ? projectName[..300] : projectName,
            CustomerId = tender.CustomerId,
            Status = OperationalProjectStatus.Planning,
            CreatedAt = now,
            CreatedByUserId = callerUserId,
            UpdatedAt = now,
            UpdatedByUserId = callerUserId,
        };
        var opportunity = new Opportunity
        {
            Name = tender.Name,
            CustomerId = tender.CustomerId,
            OperationalProject = project,
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
        db.OperationalProjects.Add(project);
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
        var allowed = tender.Status switch
        {
            TenderStatus.Preparing => nextStatus is TenderStatus.Submitted or TenderStatus.Cancelled,
            TenderStatus.Submitted => nextStatus is TenderStatus.Won or TenderStatus.Lost or TenderStatus.Cancelled,
            _ => false,
        };
        if (!allowed)
        {
            throw new TenderOperationException($"Không thể chuyển gói thầu từ {tender.Status} sang {nextStatus}.");
        }

        if (nextStatus == TenderStatus.Submitted)
        {
            var allChecklistComplete = await db.TenderChecklistItems
                .Where(item => item.TenderId == tenderId)
                .AllAsync(item => item.Status == TenderChecklistItemStatus.Done ||
                    item.Status == TenderChecklistItemStatus.Submitted, ct);
            var hasChecklist = await db.TenderChecklistItems.AnyAsync(item => item.TenderId == tenderId, ct);
            if (!hasChecklist || !allChecklistComplete)
            {
                throw new TenderOperationException("Phải hoàn tất tất cả mục checklist trước khi nộp gói thầu.");
            }
            if (!await db.TenderEstimateRevisions.AnyAsync(revision =>
                    revision.TenderId == tenderId && revision.Status == TenderEstimateRevisionStatus.Approved, ct))
            {
                throw new TenderOperationException("Phải có ít nhất một dự toán đã được phê duyệt trước khi nộp gói thầu.");
            }
        }
        else if (nextStatus == TenderStatus.Won)
        {
            if (!request.OpportunityId.HasValue || !await db.Opportunities.AnyAsync(
                    opportunity => opportunity.Id == request.OpportunityId.Value &&
                        opportunity.CustomerId == tender.CustomerId,
                    ct))
            {
                throw new TenderOperationException(
                    $"Cơ hội #{request.OpportunityId} không tồn tại hoặc không thuộc khách hàng của gói thầu.");
            }
            tender.WonOpportunityId = request.OpportunityId;
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

    private static void AutoAdvanceIfUnfinished(TenderChecklistItem item)
    {
        // Attaching a file also completes the row when it's still in an
        // unfinished state — matches the AC "upload file → % checklist tăng".
        if (item.Status is TenderChecklistItemStatus.NotStarted or TenderChecklistItemStatus.Preparing)
        {
            item.Status = TenderChecklistItemStatus.Done;
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
                "Gói thầu đã ở trạng thái kết thúc, không thể chỉnh sửa checklist.");
        }
    }

    // ------------------------------ Helpers ---------------------------------

    private static string? TrimOrNull(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

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
        var doneCount = t.ChecklistItems.Count(i =>
            i.Status == TenderChecklistItemStatus.Done || i.Status == TenderChecklistItemStatus.Submitted);
        var totalCount = t.ChecklistItems.Count;
        var percent = ComputePercent(doneCount, totalCount);

        return new TenderResponse
        {
            Id = t.Id,
            Code = t.Code,
            Name = t.Name,
            CustomerId = t.CustomerId,
            CustomerName = t.Customer?.Name ?? string.Empty,
            OpeningDate = t.OpeningDate,
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
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt,
            ChecklistItems = t.ChecklistItems
                .OrderBy(i => i.SortOrder)
                .Select(i => new TenderChecklistItemResponse
                {
                    Id = i.Id,
                    TemplateCode = i.TemplateCode,
                    Title = i.Title,
                    Status = i.Status.ToString(),
                    OwnerUserId = i.OwnerUserId,
                    OwnerName = i.Owner?.FullName,
                    InternalDeadline = i.InternalDeadline,
                    FilePath = i.FilePath,
                    OriginalFileName = i.OriginalFileName,
                    SortOrder = i.SortOrder,
                }).ToList(),
            ChecklistCompletionPercent = percent,
            IsDeadlineImminent = IsDeadlineImminent(t.Status, t.SubmissionDeadline, now) && percent < 100,
        };
    }
}
