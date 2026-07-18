using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

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
    ILogger<TenderService> logger) : ITenderService
{
    private const int MaxPageSize = 100;
    private const int DeadlineImminentDays = 3;
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

        var year = DateTime.UtcNow.Year;
        var nextSeq = 1 + await db.Tenders
            .Where(t => t.Code.StartsWith($"TD-{year}-"))
            .CountAsync(ct);
        var code = $"TD-{year}-{nextSeq:D4}";

        var entity = new Tender
        {
            Code = code,
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
        await db.SaveChangesAsync(ct);

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
            entity.Name = request.Name.Trim();
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

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.Tenders.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (entity is null) return false;

        // Guard: once submitted (or terminal) we keep the audit trail so
        // outcomes cannot silently disappear.
        if (entity.Status != TenderStatus.Preparing)
        {
            throw new TenderOperationException("Chỉ có thể xoá gói thầu đang Chuẩn bị.");
        }

        db.Tenders.Remove(entity);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Tender {Id} deleted", id);
        return true;
    }

    // ------------------------------ NIH-97 Detail-page workflow ------------------------------

    public async Task<TenderResponse?> UpdateChecklistItemAsync(int tenderId, int itemId,
        UpdateTenderChecklistItemRequest request, int callerUserId, CancellationToken ct = default)
    {
        var item = await db.TenderChecklistItems
            .FirstOrDefaultAsync(i => i.Id == itemId && i.TenderId == tenderId, ct);
        if (item is null) return null;

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<TenderChecklistItemStatus>(request.Status, ignoreCase: true, out var next))
            {
                throw new TenderOperationException($"Trạng thái checklist '{request.Status}' không hợp lệ.");
            }
            item.Status = next;
        }

        if (request.ClearOwner)
        {
            item.OwnerUserId = null;
        }
        else if (request.OwnerUserId.HasValue)
        {
            if (!await db.Users.AnyAsync(u => u.Id == request.OwnerUserId.Value, ct))
            {
                throw new TenderOperationException($"Người phụ trách #{request.OwnerUserId} không tồn tại.");
            }
            item.OwnerUserId = request.OwnerUserId.Value;
        }

        if (request.ClearInternalDeadline)
        {
            item.InternalDeadline = null;
        }
        else if (request.InternalDeadline.HasValue)
        {
            item.InternalDeadline = request.InternalDeadline.Value;
        }

        var now = DateTime.UtcNow;
        item.UpdatedAt = now;

        // Bump parent tender's UpdatedAt so list views + optimistic-UI
        // detect the change without a targeted reload.
        var tender = await db.Tenders.FirstAsync(t => t.Id == tenderId, ct);
        tender.UpdatedAt = now;
        tender.UpdatedByUserId = callerUserId;

        await db.SaveChangesAsync(ct);

        logger.LogInformation("Tender {TenderId} checklist item {ItemId} updated by user {UserId}",
            tenderId, itemId, callerUserId);
        return await GetAsync(tenderId, ct);
    }

    public async Task<TenderResponse?> AttachChecklistFileAsync(int tenderId, int itemId,
        string filePath, string originalFileName, int callerUserId, CancellationToken ct = default)
    {
        var item = await db.TenderChecklistItems
            .FirstOrDefaultAsync(i => i.Id == itemId && i.TenderId == tenderId, ct);
        if (item is null) return null;

        item.FilePath = filePath;
        item.OriginalFileName = originalFileName;
        AutoAdvanceIfUnfinished(item);
        var now = DateTime.UtcNow;
        item.UpdatedAt = now;

        var tender = await db.Tenders.FirstAsync(t => t.Id == tenderId, ct);
        tender.UpdatedAt = now;
        tender.UpdatedByUserId = callerUserId;

        await db.SaveChangesAsync(ct);
        return await GetAsync(tenderId, ct);
    }

    public async Task<TenderResponse?> AttachChecklistFromLibraryAsync(int tenderId,
        AttachTenderChecklistFromLibraryRequest request, int callerUserId, CancellationToken ct = default)
    {
        var tender = await db.Tenders.FirstOrDefaultAsync(t => t.Id == tenderId, ct);
        if (tender is null) return null;

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
        var tender = await db.Tenders.FirstOrDefaultAsync(t => t.Id == tenderId, ct);
        if (tender is null) return null;

        GuardNotTerminal(tender);

        if (!await db.Opportunities.AnyAsync(o => o.Id == request.OpportunityId, ct))
        {
            throw new TenderOperationException($"Cơ hội #{request.OpportunityId} không tồn tại.");
        }

        tender.Status = TenderStatus.Won;
        tender.WonOpportunityId = request.OpportunityId;
        tender.LostReasonCode = null;
        tender.LostNote = null;
        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            tender.Note = request.Note.Trim();
        }
        var now = DateTime.UtcNow;
        tender.ClosedAt = now;
        tender.UpdatedAt = now;
        tender.UpdatedByUserId = callerUserId;

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Tender {TenderId} marked WON by user {UserId} (opportunity {OppId})",
            tenderId, callerUserId, request.OpportunityId);
        return await GetAsync(tenderId, ct);
    }

    public async Task<TenderResponse?> MarkLostAsync(int tenderId, MarkTenderLostRequest request,
        int callerUserId, CancellationToken ct = default)
    {
        var tender = await db.Tenders.FirstOrDefaultAsync(t => t.Id == tenderId, ct);
        if (tender is null) return null;

        GuardNotTerminal(tender);

        var reasonCode = (request.ReasonCode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(reasonCode))
        {
            throw new TenderOperationException("Vui lòng chọn lý do trượt thầu.");
        }

        // Reasons come from the shared opportunity_lost_reason master data
        // category so wording stays consistent with the CRM funnel.
        var reasonExists = await db.MasterDataOptions.AnyAsync(m =>
            m.Category == "opportunity_lost_reason" && m.Code == reasonCode && m.IsActive, ct);
        if (!reasonExists)
        {
            throw new TenderOperationException($"Lý do '{reasonCode}' không hợp lệ.");
        }

        tender.Status = TenderStatus.Lost;
        tender.LostReasonCode = reasonCode;
        tender.LostNote = TrimOrNull(request.Note);
        tender.WonOpportunityId = null;
        var now = DateTime.UtcNow;
        tender.ClosedAt = now;
        tender.UpdatedAt = now;
        tender.UpdatedByUserId = callerUserId;

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Tender {TenderId} marked LOST by user {UserId} (reason={Reason})",
            tenderId, callerUserId, reasonCode);
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

    private static void GuardNotTerminal(Tender tender)
    {
        if (tender.Status is TenderStatus.Won or TenderStatus.Lost or TenderStatus.Cancelled)
        {
            throw new TenderOperationException("Gói thầu đã ở trạng thái kết thúc, không thể đánh dấu lại.");
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
