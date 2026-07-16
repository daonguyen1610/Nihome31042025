using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>
/// Thrown for expected business-rule violations in the Quote workflow —
/// invalid state transitions, missing FK references, RBAC gaps. Controller
/// converts these to HTTP 400/403.
/// </summary>
public class QuoteOperationException : Exception
{
    public QuoteOperationException(string message) : base(message) { }
}

/// <summary>
/// Quote (báo giá) service — CRUD, totals computation, versioning and
/// workflow state machine. Owner scoping mirrors <see cref="OpportunityService"/>:
/// <c>crm.quotes.view.all</c> unlocks cross-owner view/edit, otherwise callers
/// see + mutate only quotes they own.
///
/// State machine:
///   Draft ──Submit──▶ PendingApproval ──Approve──▶ Approved ──Send──▶ SentToCustomer
///     ▲                    │                                              │
///     └── RejectInternal ──┘                                              │
///                                                                        ├─▶ CustomerApproved (terminal)
///                                                                        ├─▶ Rejected         (terminal)
///                                                                        └─▶ Expired          (auto when ValidUntil &lt; now)
///
/// Any non-terminal → Cancelled via Cancel action.
/// </summary>
public class QuoteService(
    AppDbContext db,
    INotificationService notifications,
    ILogger<QuoteService> logger) : IQuoteService
{
    private const int MaxPageSize = 100;
    private const int DefaultValidityDays = 30;
    private const int ExpiringSoonDays = 3;

    // ------------------------------ List / Get ------------------------------

    public async Task<QuoteListResponse> ListAsync(
        int callerUserId,
        bool canSeeAll,
        QuoteStatus? status = null,
        int? opportunityId = null,
        int? customerId = null,
        int? ownerUserId = null,
        DateTime? createdFrom = null,
        DateTime? createdTo = null,
        decimal? minValue = null,
        decimal? maxValue = null,
        string? search = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > MaxPageSize) pageSize = MaxPageSize;

        var q = BuildFiltered(callerUserId, canSeeAll, status, opportunityId, customerId, ownerUserId,
            createdFrom, createdTo, minValue, maxValue, search);

        var total = await q.CountAsync(ct);

        // Spec NIH-92: default sort ValidUntil ASC so soon-to-expire quotes surface first.
        var rows = await q
            .OrderBy(x => x.ValidUntil)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                Quote = x,
                OpportunityName = x.Opportunity.Name,
                CustomerName = x.Opportunity.Customer.Name,
                OwnerName = x.Owner != null ? x.Owner.FullName : null,
            })
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        return new QuoteListResponse
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Items = rows.Select(r => new QuoteListItemResponse
            {
                Id = r.Quote.Id,
                Code = r.Quote.Code,
                OpportunityId = r.Quote.OpportunityId,
                OpportunityName = r.OpportunityName,
                CustomerName = r.CustomerName,
                OwnerUserId = r.Quote.OwnerUserId,
                OwnerName = r.OwnerName,
                Version = r.Quote.Version,
                Method = r.Quote.Method.ToString(),
                GrandTotal = r.Quote.GrandTotal,
                Status = r.Quote.Status.ToString(),
                ValidUntil = r.Quote.ValidUntil,
                IsExpiringSoon = IsNonTerminal(r.Quote.Status)
                    && (r.Quote.ValidUntil - now).TotalDays <= ExpiringSoonDays,
                UpdatedAt = r.Quote.UpdatedAt,
            }).ToList(),
        };
    }

    public async Task<QuoteResponse?> GetAsync(int id, int callerUserId, bool canSeeAll, CancellationToken ct = default)
    {
        var quote = await db.Quotes
            .AsNoTracking()
            .Include(q => q.Opportunity).ThenInclude(o => o.Customer)
            .Include(q => q.Owner)
            .Include(q => q.Items)
            .Include(q => q.ApprovalLogs).ThenInclude(l => l.By)
            .FirstOrDefaultAsync(q => q.Id == id, ct);
        if (quote is null) return null;
        if (!canSeeAll && quote.OwnerUserId != callerUserId) return null;
        return Map(quote);
    }

    // ------------------------------ Create / Update ------------------------------

    public async Task<QuoteResponse> CreateAsync(
        CreateQuoteRequest request,
        int callerUserId,
        bool canManage,
        CancellationToken ct = default)
    {
        if (!canManage) throw new QuoteOperationException("Không có quyền tạo báo giá.");

        var opportunity = await db.Opportunities.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == request.OpportunityId, ct)
            ?? throw new QuoteOperationException($"Không tìm thấy cơ hội #{request.OpportunityId}.");

        ValidateMethodPayload(request.Method, request.AreaSqm, request.UnitPricePerSqm, request.Items);

        var now = DateTime.UtcNow;
        var ownerId = request.OwnerUserId ?? opportunity.OwnerUserId ?? callerUserId;
        var validUntil = request.ValidUntil ?? now.AddDays(DefaultValidityDays);
        if (validUntil < now.Date)
        {
            throw new QuoteOperationException("Hạn hiệu lực không được nhỏ hơn hôm nay.");
        }

        var quote = new Quote
        {
            Code = await GenerateCodeAsync(now, ct),
            OpportunityId = request.OpportunityId,
            OwnerUserId = ownerId,
            Method = request.Method,
            Version = 1,
            AreaSqm = request.Method == QuoteMethod.UnitCost ? request.AreaSqm : null,
            UnitPricePerSqm = request.Method == QuoteMethod.UnitCost ? request.UnitPricePerSqm : null,
            PackageDescription = request.Method == QuoteMethod.UnitCost
                ? request.PackageDescription?.Trim()
                : null,
            DiscountPercent = request.DiscountPercent,
            VatPercent = request.VatPercent,
            ValidUntil = validUntil,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            Status = QuoteStatus.Draft,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedByUserId = callerUserId,
            UpdatedByUserId = callerUserId,
        };

        ApplyItems(quote, request.Method, request.Items);
        RecomputeTotals(quote);

        db.Quotes.Add(quote);
        db.QuoteApprovalLogs.Add(new QuoteApprovalLog
        {
            Quote = quote,
            Action = QuoteWorkflowAction.Create,
            FromStatus = null,
            ToStatus = QuoteStatus.Draft,
            ByUserId = callerUserId,
            CreatedAt = now,
        });
        await db.SaveChangesAsync(ct);

        return (await GetAsync(quote.Id, callerUserId, canSeeAll: true, ct))!;
    }

    public async Task<QuoteResponse?> UpdateAsync(
        int id,
        UpdateQuoteRequest request,
        int callerUserId,
        bool canManage,
        bool canSeeAll,
        CancellationToken ct = default)
    {
        if (!canManage) throw new QuoteOperationException("Không có quyền chỉnh sửa báo giá.");

        var quote = await db.Quotes.Include(q => q.Items).FirstOrDefaultAsync(q => q.Id == id, ct);
        if (quote is null) return null;
        if (!canSeeAll && quote.OwnerUserId != callerUserId) return null;

        if (quote.Status is QuoteStatus.Cancelled
            or QuoteStatus.Rejected
            or QuoteStatus.CustomerApproved)
        {
            throw new QuoteOperationException($"Báo giá đang ở trạng thái {quote.Status} — không thể chỉnh sửa.");
        }

        if (!canSeeAll && request.OwnerUserId.HasValue && request.OwnerUserId.Value != callerUserId)
        {
            throw new QuoteOperationException("Bạn không có quyền gán báo giá cho người khác.");
        }

        ValidateMethodPayload(quote.Method, request.AreaSqm, request.UnitPricePerSqm, request.Items);

        var now = DateTime.UtcNow;

        // Spec NIH-84 & NIH-93: editing after Approved/Sent/... spawns a new version.
        // Snapshot the current row before mutating, then bump Version.
        var isPostApproval = quote.Status is QuoteStatus.Approved
            or QuoteStatus.SentToCustomer
            or QuoteStatus.Expired;
        if (isPostApproval)
        {
            db.QuoteVersionSnapshots.Add(SnapshotOf(quote, now, callerUserId));
            quote.Version += 1;
            quote.Status = QuoteStatus.Draft;
            quote.SubmittedAt = null;
            quote.SubmittedByUserId = null;
            quote.ApprovedAt = null;
            quote.ApprovedByUserId = null;
            quote.SentAt = null;
            quote.SentByUserId = null;
            quote.ClosedAt = null;
            db.QuoteApprovalLogs.Add(new QuoteApprovalLog
            {
                QuoteId = quote.Id,
                Action = QuoteWorkflowAction.NewVersion,
                FromStatus = QuoteStatus.Approved,
                ToStatus = QuoteStatus.Draft,
                ByUserId = callerUserId,
                Note = $"Bumped to V{quote.Version} on edit-after-approval.",
                CreatedAt = now,
            });
        }

        quote.OwnerUserId = request.OwnerUserId ?? quote.OwnerUserId;
        quote.AreaSqm = quote.Method == QuoteMethod.UnitCost ? request.AreaSqm : null;
        quote.UnitPricePerSqm = quote.Method == QuoteMethod.UnitCost ? request.UnitPricePerSqm : null;
        quote.PackageDescription = quote.Method == QuoteMethod.UnitCost
            ? request.PackageDescription?.Trim()
            : null;
        quote.DiscountPercent = request.DiscountPercent;
        quote.VatPercent = request.VatPercent;
        if (request.ValidUntil.HasValue) quote.ValidUntil = request.ValidUntil.Value;
        quote.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        quote.UpdatedAt = now;
        quote.UpdatedByUserId = callerUserId;

        // Rebuild items only for BOQ. UnitCost mode never has line items.
        db.QuoteItems.RemoveRange(quote.Items);
        quote.Items = new List<QuoteItem>();
        ApplyItems(quote, quote.Method, request.Items);
        RecomputeTotals(quote);

        db.QuoteApprovalLogs.Add(new QuoteApprovalLog
        {
            QuoteId = quote.Id,
            Action = QuoteWorkflowAction.Update,
            FromStatus = quote.Status,
            ToStatus = quote.Status,
            ByUserId = callerUserId,
            CreatedAt = now,
        });
        await db.SaveChangesAsync(ct);

        return await GetAsync(quote.Id, callerUserId, canSeeAll: true, ct);
    }

    // ------------------------------ Workflow transitions ------------------------------

    public Task<QuoteResponse?> SubmitAsync(int id, QuoteWorkflowRequest req, int caller, bool canManage, bool canSeeAll, CancellationToken ct = default) =>
        TransitionAsync(id, caller, canSeeAll,
            allowedFrom: [QuoteStatus.Draft],
            to: QuoteStatus.PendingApproval,
            action: QuoteWorkflowAction.Submit,
            permitted: canManage,
            note: req.Note,
            beforeSave: q =>
            {
                if (q.Items.Count == 0 && q.Method == QuoteMethod.Boq)
                    throw new QuoteOperationException("Báo giá BOQ phải có ít nhất 1 dòng hạng mục.");
                if (q.GrandTotal <= 0)
                    throw new QuoteOperationException("Báo giá phải có tổng > 0.");
                if (q.ValidUntil < DateTime.UtcNow.Date)
                    throw new QuoteOperationException("Hạn hiệu lực đã qua — cập nhật trước khi submit.");
                q.SubmittedAt = DateTime.UtcNow;
                q.SubmittedByUserId = caller;
            }, ct);

    public Task<QuoteResponse?> ApproveAsync(int id, QuoteWorkflowRequest req, int caller, bool canApprove, CancellationToken ct = default) =>
        TransitionAsync(id, caller, canSeeAll: true,
            allowedFrom: [QuoteStatus.PendingApproval],
            to: QuoteStatus.Approved,
            action: QuoteWorkflowAction.Approve,
            permitted: canApprove,
            note: req.Note,
            beforeSave: q => { q.ApprovedAt = DateTime.UtcNow; q.ApprovedByUserId = caller; },
            ct: ct);

    public Task<QuoteResponse?> RejectInternalAsync(int id, QuoteWorkflowRequest req, int caller, bool canApprove, CancellationToken ct = default) =>
        TransitionAsync(id, caller, canSeeAll: true,
            allowedFrom: [QuoteStatus.PendingApproval],
            to: QuoteStatus.Draft,
            action: QuoteWorkflowAction.RejectInternal,
            permitted: canApprove,
            note: req.Note,
            beforeSave: q =>
            {
                if (string.IsNullOrWhiteSpace(req.Note))
                    throw new QuoteOperationException("Từ chối phải kèm ghi chú.");
                q.SubmittedAt = null;
                q.SubmittedByUserId = null;
            }, ct);

    public Task<QuoteResponse?> SendToCustomerAsync(int id, QuoteWorkflowRequest req, int caller, bool canSend, bool canSeeAll, CancellationToken ct = default) =>
        TransitionAsync(id, caller, canSeeAll,
            allowedFrom: [QuoteStatus.Approved],
            to: QuoteStatus.SentToCustomer,
            action: QuoteWorkflowAction.Send,
            permitted: canSend,
            note: req.Note,
            beforeSave: q => { q.SentAt = DateTime.UtcNow; q.SentByUserId = caller; }, ct);

    public Task<QuoteResponse?> MarkCustomerApprovedAsync(int id, QuoteWorkflowRequest req, int caller, bool canManage, bool canSeeAll, CancellationToken ct = default) =>
        TransitionAsync(id, caller, canSeeAll,
            allowedFrom: [QuoteStatus.SentToCustomer],
            to: QuoteStatus.CustomerApproved,
            action: QuoteWorkflowAction.CustomerApprove,
            permitted: canManage,
            note: req.Note,
            beforeSave: q => { q.ClosedAt = DateTime.UtcNow; }, ct);

    public Task<QuoteResponse?> MarkCustomerRejectedAsync(int id, QuoteWorkflowRequest req, int caller, bool canManage, bool canSeeAll, CancellationToken ct = default) =>
        TransitionAsync(id, caller, canSeeAll,
            allowedFrom: [QuoteStatus.SentToCustomer],
            to: QuoteStatus.Rejected,
            action: QuoteWorkflowAction.CustomerReject,
            permitted: canManage,
            note: req.Note,
            beforeSave: q =>
            {
                if (string.IsNullOrWhiteSpace(req.Note))
                    throw new QuoteOperationException("Từ chối bởi khách phải kèm ghi chú lý do.");
                q.ClosedAt = DateTime.UtcNow;
            }, ct);

    public Task<QuoteResponse?> CancelAsync(int id, QuoteWorkflowRequest req, int caller, bool canManage, bool canSeeAll, CancellationToken ct = default) =>
        TransitionAsync(id, caller, canSeeAll,
            allowedFrom: [
                QuoteStatus.Draft, QuoteStatus.PendingApproval,
                QuoteStatus.Approved, QuoteStatus.SentToCustomer,
                QuoteStatus.Expired,
            ],
            to: QuoteStatus.Cancelled,
            action: QuoteWorkflowAction.Cancel,
            permitted: canManage,
            note: req.Note,
            beforeSave: q => { q.ClosedAt = DateTime.UtcNow; }, ct);

    public async Task<QuoteResponse?> ExtendValidityAsync(
        int id, ExtendQuoteValidityRequest request, int callerUserId, bool canApprove, CancellationToken ct = default)
    {
        if (!canApprove) throw new QuoteOperationException("Không có quyền gia hạn báo giá.");

        var quote = await db.Quotes.FirstOrDefaultAsync(q => q.Id == id, ct);
        if (quote is null) return null;

        if (quote.Status is not QuoteStatus.Expired and not QuoteStatus.Approved and not QuoteStatus.SentToCustomer)
        {
            throw new QuoteOperationException("Chỉ báo giá đã duyệt / đã gửi / hết hạn mới có thể gia hạn.");
        }
        if (request.NewValidUntil <= DateTime.UtcNow.Date)
        {
            throw new QuoteOperationException("Hạn mới phải sau ngày hôm nay.");
        }

        var now = DateTime.UtcNow;
        var previousStatus = quote.Status;
        quote.ValidUntil = request.NewValidUntil;
        if (quote.Status == QuoteStatus.Expired) quote.Status = QuoteStatus.Approved;
        quote.UpdatedAt = now;
        quote.UpdatedByUserId = callerUserId;

        db.QuoteApprovalLogs.Add(new QuoteApprovalLog
        {
            QuoteId = quote.Id,
            Action = QuoteWorkflowAction.ExtendValidity,
            FromStatus = previousStatus,
            ToStatus = quote.Status,
            ByUserId = callerUserId,
            Note = request.Note,
            CreatedAt = now,
        });
        await db.SaveChangesAsync(ct);
        return await GetAsync(quote.Id, callerUserId, canSeeAll: true, ct);
    }

    // ------------------------------ Delete ------------------------------

    public async Task<bool> DeleteAsync(int id, int callerUserId, bool canManage, bool canSeeAll, CancellationToken ct = default)
    {
        if (!canManage) return false;

        var quote = await db.Quotes.FirstOrDefaultAsync(q => q.Id == id, ct);
        if (quote is null) return false;
        if (!canSeeAll && quote.OwnerUserId != callerUserId) return false;

        // Hard delete only allowed on Draft. Any other state should use Cancel.
        if (quote.Status != QuoteStatus.Draft)
        {
            throw new QuoteOperationException("Chỉ báo giá ở trạng thái Draft mới được xoá. Trạng thái khác dùng Cancel.");
        }

        db.Quotes.Remove(quote);
        await db.SaveChangesAsync(ct);
        return true;
    }

    // ------------------------------ Versions ------------------------------

    public async Task<QuoteVersionsResponse?> GetVersionsAsync(int id, int callerUserId, bool canSeeAll, CancellationToken ct = default)
    {
        var quote = await db.Quotes.AsNoTracking()
            .Include(q => q.Items)
            .Include(q => q.VersionSnapshots)
            .FirstOrDefaultAsync(q => q.Id == id, ct);
        if (quote is null) return null;
        if (!canSeeAll && quote.OwnerUserId != callerUserId) return null;

        var versions = quote.VersionSnapshots
            .OrderBy(s => s.VersionNumber)
            .Select(s => new QuoteVersionResponse
            {
                Version = s.VersionNumber,
                Method = s.Method.ToString(),
                AreaSqm = s.AreaSqm,
                UnitPricePerSqm = s.UnitPricePerSqm,
                PackageDescription = s.PackageDescription,
                Subtotal = s.Subtotal,
                DiscountPercent = s.DiscountPercent,
                VatPercent = s.VatPercent,
                GrandTotal = s.GrandTotal,
                Items = DeserializeItems(s.ItemsJson),
                CapturedAt = s.CreatedAt,
                IsCurrent = false,
            }).ToList();

        versions.Add(new QuoteVersionResponse
        {
            Version = quote.Version,
            Method = quote.Method.ToString(),
            AreaSqm = quote.AreaSqm,
            UnitPricePerSqm = quote.UnitPricePerSqm,
            PackageDescription = quote.PackageDescription,
            Subtotal = quote.Subtotal,
            DiscountPercent = quote.DiscountPercent,
            VatPercent = quote.VatPercent,
            GrandTotal = quote.GrandTotal,
            Items = quote.Items.OrderBy(i => i.SortOrder).Select(MapItem).ToList(),
            CapturedAt = quote.UpdatedAt,
            IsCurrent = true,
        });

        return new QuoteVersionsResponse { QuoteId = quote.Id, Versions = versions };
    }

    // ============================== Internals ==============================

    private async Task<QuoteResponse?> TransitionAsync(
        int id,
        int callerUserId,
        bool canSeeAll,
        QuoteStatus[] allowedFrom,
        QuoteStatus to,
        QuoteWorkflowAction action,
        bool permitted,
        string? note,
        Action<Quote> beforeSave,
        CancellationToken ct)
    {
        if (!permitted)
        {
            throw new QuoteOperationException($"Không có quyền thực hiện thao tác {action}.");
        }

        // Include Items so guards like Submit's "BOQ needs ≥ 1 item" can inspect
        // the true row count instead of the default-empty navigation collection.
        var quote = await db.Quotes
            .Include(q => q.Items)
            .FirstOrDefaultAsync(q => q.Id == id, ct);
        if (quote is null) return null;
        if (!canSeeAll && quote.OwnerUserId != callerUserId) return null;

        // Auto-expire before evaluating allowed-from.
        AutoExpireIfNeeded(quote);

        if (!allowedFrom.Contains(quote.Status))
        {
            throw new QuoteOperationException(
                $"Không thể chuyển báo giá từ {quote.Status} sang {to}.");
        }

        var from = quote.Status;
        quote.Status = to;
        beforeSave(quote);

        var now = DateTime.UtcNow;
        quote.UpdatedAt = now;
        quote.UpdatedByUserId = callerUserId;

        db.QuoteApprovalLogs.Add(new QuoteApprovalLog
        {
            QuoteId = quote.Id,
            Action = action,
            FromStatus = from,
            ToStatus = to,
            ByUserId = callerUserId,
            Note = note,
            CreatedAt = now,
        });
        await db.SaveChangesAsync(ct);

        // Best-effort notification hooks. Failures are logged, never bubble up.
        try
        {
            await FireStatusNotificationsAsync(quote, from, to, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Quote {QuoteId} notification for {Action} failed.", quote.Id, action);
        }

        return await GetAsync(quote.Id, callerUserId, canSeeAll: true, ct);
    }

    private async Task FireStatusNotificationsAsync(Quote quote, QuoteStatus from, QuoteStatus to, CancellationToken ct)
    {
        // Slice 1: emit an in-app notification for the Sales owner on approve /
        // reject / customer-approve / customer-reject / cancel. Recipient
        // lookup for Sales Manager on Submit is deferred to a follow-up slice
        // (needs "users with permission" resolver — parked in NIH-84).
        _ = ct;
        if (quote.OwnerUserId is not int ownerId) return;
        if (to is not (QuoteStatus.Approved or QuoteStatus.SentToCustomer
            or QuoteStatus.CustomerApproved or QuoteStatus.Rejected or QuoteStatus.Cancelled))
        {
            return;
        }

        var title = $"Báo giá {quote.Code} · {StatusVi(to)}";
        var body = $"Báo giá {quote.Code} chuyển từ {StatusVi(from)} sang {StatusVi(to)}.";
        await notifications.CreateAsync(ownerId, "crm.quote.status-changed", title, body);
    }

    private static string StatusVi(QuoteStatus s) => s switch
    {
        QuoteStatus.Draft => "Nháp",
        QuoteStatus.PendingApproval => "Chờ duyệt nội bộ",
        QuoteStatus.Approved => "Đã duyệt",
        QuoteStatus.SentToCustomer => "Đã gửi khách",
        QuoteStatus.CustomerApproved => "Khách duyệt",
        QuoteStatus.Rejected => "Từ chối",
        QuoteStatus.Expired => "Hết hạn",
        QuoteStatus.Cancelled => "Đã huỷ",
        _ => s.ToString(),
    };

    private static void AutoExpireIfNeeded(Quote quote)
    {
        if (IsNonTerminal(quote.Status)
            && quote.Status != QuoteStatus.Draft
            && quote.Status != QuoteStatus.PendingApproval
            && quote.ValidUntil < DateTime.UtcNow)
        {
            quote.Status = QuoteStatus.Expired;
        }
    }

    private static bool IsNonTerminal(QuoteStatus s) =>
        s is not QuoteStatus.CustomerApproved
          and not QuoteStatus.Rejected
          and not QuoteStatus.Cancelled;

    private IQueryable<Quote> BuildFiltered(
        int callerUserId, bool canSeeAll,
        QuoteStatus? status, int? opportunityId, int? customerId, int? ownerUserId,
        DateTime? createdFrom, DateTime? createdTo,
        decimal? minValue, decimal? maxValue, string? search)
    {
        var q = db.Quotes.AsNoTracking().AsQueryable();
        if (!canSeeAll) q = q.Where(x => x.OwnerUserId == callerUserId);
        else if (ownerUserId.HasValue) q = q.Where(x => x.OwnerUserId == ownerUserId.Value);

        if (status.HasValue) q = q.Where(x => x.Status == status.Value);
        if (opportunityId.HasValue) q = q.Where(x => x.OpportunityId == opportunityId.Value);
        if (customerId.HasValue) q = q.Where(x => x.Opportunity.CustomerId == customerId.Value);
        if (createdFrom.HasValue) q = q.Where(x => x.CreatedAt >= createdFrom.Value);
        if (createdTo.HasValue) q = q.Where(x => x.CreatedAt <= createdTo.Value);
        if (minValue.HasValue) q = q.Where(x => x.GrandTotal >= minValue.Value);
        if (maxValue.HasValue) q = q.Where(x => x.GrandTotal <= maxValue.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var like = $"%{search.Trim()}%";
            q = q.Where(x =>
                EF.Functions.Like(x.Code, like)
                || EF.Functions.Like(x.Opportunity.Name, like)
                || EF.Functions.Like(x.Opportunity.Customer.Name, like));
        }
        return q;
    }

    private async Task<string> GenerateCodeAsync(DateTime now, CancellationToken ct)
    {
        var year = now.Year;
        var prefix = $"QT-{year}-";
        var lastNumber = await db.Quotes.AsNoTracking()
            .Where(q => q.Code.StartsWith(prefix))
            .OrderByDescending(q => q.Code)
            .Select(q => q.Code)
            .FirstOrDefaultAsync(ct);
        int next = 1;
        if (lastNumber is not null && int.TryParse(lastNumber[prefix.Length..], out var parsed))
        {
            next = parsed + 1;
        }
        return $"{prefix}{next:D4}";
    }

    private static void ApplyItems(Quote quote, QuoteMethod method, List<QuoteItemInput> inputs)
    {
        if (method != QuoteMethod.Boq) return;
        int sort = 0;
        foreach (var i in inputs)
        {
            var amount = decimal.Round(i.Quantity * i.UnitPrice, 2, MidpointRounding.AwayFromZero);
            quote.Items.Add(new QuoteItem
            {
                ItemCode = string.IsNullOrWhiteSpace(i.ItemCode) ? null : i.ItemCode.Trim(),
                Name = i.Name.Trim(),
                Unit = i.Unit.Trim(),
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                Amount = amount,
                SortOrder = i.SortOrder == 0 ? ++sort : i.SortOrder,
            });
        }
    }

    private static void RecomputeTotals(Quote quote)
    {
        decimal subtotal = quote.Method == QuoteMethod.Boq
            ? quote.Items.Sum(i => i.Amount)
            : decimal.Round((quote.AreaSqm ?? 0m) * (quote.UnitPricePerSqm ?? 0m), 2, MidpointRounding.AwayFromZero);

        var afterDiscount = subtotal * (1 - quote.DiscountPercent / 100m);
        var vat = afterDiscount * (quote.VatPercent / 100m);
        var grand = decimal.Round(afterDiscount + vat, 2, MidpointRounding.AwayFromZero);

        quote.Subtotal = decimal.Round(subtotal, 2, MidpointRounding.AwayFromZero);
        quote.GrandTotal = grand;
    }

    private static void ValidateMethodPayload(QuoteMethod method, decimal? area, decimal? unitPrice, List<QuoteItemInput> items)
    {
        if (method == QuoteMethod.UnitCost)
        {
            if (area is null || area <= 0)
                throw new QuoteOperationException("Suất đầu tư: Diện tích m² phải > 0.");
            if (unitPrice is null || unitPrice <= 0)
                throw new QuoteOperationException("Suất đầu tư: Đơn giá/m² phải > 0.");
        }
        else
        {
            if (items.Count == 0)
                throw new QuoteOperationException("BOQ: phải có ít nhất 1 dòng hạng mục.");
            if (items.Any(i => i.Quantity < 0 || i.UnitPrice < 0))
                throw new QuoteOperationException("BOQ: khối lượng và đơn giá không được âm.");
        }
    }

    private static QuoteVersionSnapshot SnapshotOf(Quote q, DateTime now, int by) => new()
    {
        QuoteId = q.Id,
        VersionNumber = q.Version,
        Method = q.Method,
        AreaSqm = q.AreaSqm,
        UnitPricePerSqm = q.UnitPricePerSqm,
        PackageDescription = q.PackageDescription,
        Subtotal = q.Subtotal,
        DiscountPercent = q.DiscountPercent,
        VatPercent = q.VatPercent,
        GrandTotal = q.GrandTotal,
        ItemsJson = JsonSerializer.Serialize(q.Items.OrderBy(i => i.SortOrder).Select(MapItem)),
        CreatedAt = now,
        CreatedByUserId = by,
    };

    private static List<QuoteItemResponse> DeserializeItems(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<QuoteItemResponse>>(json) ?? new();
        }
        catch (JsonException)
        {
            return new List<QuoteItemResponse>();
        }
    }

    private static QuoteItemResponse MapItem(QuoteItem i) => new()
    {
        Id = i.Id,
        ItemCode = i.ItemCode,
        Name = i.Name,
        Unit = i.Unit,
        Quantity = i.Quantity,
        UnitPrice = i.UnitPrice,
        Amount = i.Amount,
        SortOrder = i.SortOrder,
    };

    private static QuoteResponse Map(Quote q) => new()
    {
        Id = q.Id,
        Code = q.Code,
        OpportunityId = q.OpportunityId,
        OpportunityName = q.Opportunity?.Name,
        CustomerId = q.Opportunity?.CustomerId,
        CustomerName = q.Opportunity?.Customer?.Name,
        OwnerUserId = q.OwnerUserId,
        OwnerName = q.Owner?.FullName,
        Method = q.Method.ToString(),
        Version = q.Version,
        AreaSqm = q.AreaSqm,
        UnitPricePerSqm = q.UnitPricePerSqm,
        PackageDescription = q.PackageDescription,
        Subtotal = q.Subtotal,
        DiscountPercent = q.DiscountPercent,
        VatPercent = q.VatPercent,
        GrandTotal = q.GrandTotal,
        GrandTotalInWords = VietnameseNumberFormatter.ToWords(q.GrandTotal),
        Status = q.Status.ToString(),
        ValidUntil = q.ValidUntil,
        IsExpired = IsNonTerminal(q.Status) && q.ValidUntil < DateTime.UtcNow,
        Note = q.Note,
        SubmittedAt = q.SubmittedAt,
        ApprovedAt = q.ApprovedAt,
        SentAt = q.SentAt,
        ClosedAt = q.ClosedAt,
        CreatedAt = q.CreatedAt,
        UpdatedAt = q.UpdatedAt,
        Items = q.Items?.OrderBy(i => i.SortOrder).Select(MapItem).ToList() ?? new(),
        ApprovalLogs = q.ApprovalLogs?
            .OrderBy(l => l.CreatedAt)
            .Select(l => new QuoteApprovalLogResponse
            {
                Id = l.Id,
                Action = l.Action.ToString(),
                FromStatus = l.FromStatus?.ToString(),
                ToStatus = l.ToStatus.ToString(),
                ByUserId = l.ByUserId,
                ByUserName = l.By?.FullName,
                Note = l.Note,
                CreatedAt = l.CreatedAt,
            }).ToList() ?? new(),
    };
}
