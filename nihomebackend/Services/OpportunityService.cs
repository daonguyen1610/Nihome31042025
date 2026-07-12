using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class OpportunityService(
    AppDbContext db,
    INotificationService notifications,
    ILogger<OpportunityService> logger) : IOpportunityService
{
    private const int MaxPageSize = 100;
    private const string LostReasonMasterDataCategory = "opportunity_lost_reason";
    private const string OpportunityAssignedTemplate = "opportunity.assigned";
    private const string OpportunityStageChangedTemplate = "opportunity.stage-changed";

    // -------- List / Pipeline / Get --------

    public async Task<OpportunityListResponse> ListAsync(
        int callerUserId,
        bool canSeeAll,
        OpportunityStage? stage = null,
        int? customerId = null,
        int? ownerUserId = null,
        DateTime? expectedCloseFrom = null,
        DateTime? expectedCloseTo = null,
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

        var query = BuildFilteredQuery(
            callerUserId, canSeeAll, stage, customerId, ownerUserId,
            expectedCloseFrom, expectedCloseTo, minValue, maxValue, search);

        var total = await query.CountAsync(ct);

        var rows = await query
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new
            {
                Opportunity = o,
                CustomerName = o.Customer.Name,
                OwnerName = o.Owner != null ? o.Owner.FullName : null,
            })
            .ToListAsync(ct);

        return new OpportunityListResponse
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Items = rows
                .Select(r => Map(r.Opportunity, r.CustomerName, r.OwnerName, activities: null))
                .ToList(),
        };
    }

    public async Task<OpportunityPipelineResponse> GetPipelineAsync(
        int callerUserId,
        bool canSeeAll,
        int? ownerUserId = null,
        int? customerId = null,
        DateTime? expectedCloseFrom = null,
        DateTime? expectedCloseTo = null,
        decimal? minValue = null,
        decimal? maxValue = null,
        CancellationToken ct = default)
    {
        var query = BuildFilteredQuery(
            callerUserId, canSeeAll,
            stage: null, customerId, ownerUserId,
            expectedCloseFrom, expectedCloseTo, minValue, maxValue, search: null);

        var rows = await query
            .OrderBy(o => o.ExpectedCloseDate ?? DateTime.MaxValue)
            .ThenByDescending(o => o.EstimatedValue)
            .Select(o => new
            {
                Opportunity = o,
                CustomerName = o.Customer.Name,
                OwnerName = o.Owner != null ? o.Owner.FullName : null,
            })
            .ToListAsync(ct);

        var byStage = rows
            .GroupBy(r => r.Opportunity.Stage)
            .ToDictionary(g => g.Key, g => g.ToList());

        var columns = new List<OpportunityPipelineColumn>();
        foreach (var stage in Enum.GetValues<OpportunityStage>())
        {
            var bucket = byStage.TryGetValue(stage, out var b) ? b : new();
            columns.Add(new OpportunityPipelineColumn
            {
                Stage = stage,
                Count = bucket.Count,
                TotalValue = bucket.Sum(r => r.Opportunity.EstimatedValue),
                Items = bucket
                    .Select(r => Map(r.Opportunity, r.CustomerName, r.OwnerName, activities: null))
                    .ToList(),
            });
        }
        return new OpportunityPipelineResponse { Columns = columns };
    }

    public async Task<OpportunityResponse?> GetAsync(
        int id,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default)
    {
        var op = await db.Opportunities
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Owner)
            .Include(o => o.Activities)
                .ThenInclude(a => a.CreatedBy)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        if (op is null) return null;
        if (!canSeeAll && op.OwnerUserId != callerUserId) return null;

        return Map(op, op.Customer.Name, op.Owner?.FullName, op.Activities);
    }

    // -------- Create / Update --------

    public async Task<OpportunityResponse> CreateAsync(
        CreateOpportunityRequest request,
        int callerUserId,
        bool canManage,
        CancellationToken ct = default)
    {
        if (!canManage)
        {
            throw new OpportunityOperationException("Không có quyền tạo cơ hội.");
        }

        if (request.Stage is OpportunityStage.Won or OpportunityStage.Lost)
        {
            throw new OpportunityOperationException(
                "Cơ hội mới phải khởi tạo ở giai đoạn Prospecting/Qualification/Proposal/Negotiation. Dùng đổi giai đoạn để chuyển sang Won/Lost.");
        }

        var customer = await db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId, ct)
            ?? throw new OpportunityOperationException($"Không tìm thấy khách hàng #{request.CustomerId}.");

        var now = DateTime.UtcNow;
        var ownerId = request.OwnerUserId ?? callerUserId;

        var op = new Opportunity
        {
            Name = request.Name.Trim(),
            CustomerId = request.CustomerId,
            OwnerUserId = ownerId,
            EstimatedValue = request.EstimatedValue,
            WinProbability = request.WinProbability,
            ExpectedCloseDate = request.ExpectedCloseDate,
            Stage = request.Stage,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
            CreatedByUserId = callerUserId,
            UpdatedByUserId = callerUserId,
        };

        db.Opportunities.Add(op);
        await db.SaveChangesAsync(ct);

        if (ownerId != callerUserId)
        {
            await FireAssignedAsync(op, customer.Name, ownerId);
        }

        return await GetAsync(op.Id, callerUserId, canSeeAll: true, ct)
            ?? throw new InvalidOperationException("Newly created opportunity missing.");
    }

    public async Task<OpportunityResponse?> UpdateAsync(
        int id,
        UpdateOpportunityRequest request,
        int callerUserId,
        bool canManage,
        bool canSeeAll,
        CancellationToken ct = default)
    {
        if (!canManage) throw new OpportunityOperationException("Không có quyền chỉnh sửa cơ hội.");

        var op = await db.Opportunities.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (op is null) return null;
        if (!canSeeAll && op.OwnerUserId != callerUserId) return null;

        var customer = await db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId, ct)
            ?? throw new OpportunityOperationException($"Không tìm thấy khách hàng #{request.CustomerId}.");

        var previousOwnerId = op.OwnerUserId;

        // Sales without view.all cannot reassign the opportunity to another user.
        if (!canSeeAll && request.OwnerUserId.HasValue && request.OwnerUserId.Value != callerUserId)
        {
            throw new OpportunityOperationException("Bạn không có quyền gán cơ hội cho người khác.");
        }

        op.Name = request.Name.Trim();
        op.CustomerId = request.CustomerId;
        op.OwnerUserId = request.OwnerUserId;
        op.EstimatedValue = request.EstimatedValue;
        op.WinProbability = request.WinProbability;
        op.ExpectedCloseDate = request.ExpectedCloseDate;
        op.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        op.UpdatedAt = DateTime.UtcNow;
        op.UpdatedByUserId = callerUserId;

        await db.SaveChangesAsync(ct);

        if (op.OwnerUserId.HasValue
            && op.OwnerUserId.Value != previousOwnerId
            && op.OwnerUserId.Value != callerUserId)
        {
            await FireAssignedAsync(op, customer.Name, op.OwnerUserId.Value);
        }

        return await GetAsync(op.Id, callerUserId, canSeeAll: true, ct);
    }

    // -------- Stage change --------

    public async Task<OpportunityResponse?> ChangeStageAsync(
        int id,
        ChangeOpportunityStageRequest request,
        int callerUserId,
        bool canManage,
        bool canSeeAll,
        CancellationToken ct = default)
    {
        if (!canManage) throw new OpportunityOperationException("Không có quyền đổi giai đoạn cơ hội.");

        var op = await db.Opportunities.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (op is null) return null;
        if (!canSeeAll && op.OwnerUserId != callerUserId) return null;

        var from = op.Stage;
        var to = request.TargetStage;
        if (from == to)
        {
            return await GetAsync(op.Id, callerUserId, canSeeAll: true, ct);
        }

        // Terminal stages (Won/Lost) cannot revert to earlier stages nor swap sideways.
        if (from is OpportunityStage.Won or OpportunityStage.Lost)
        {
            throw new OpportunityOperationException(
                $"Cơ hội đã ở giai đoạn {from}. Không thể chuyển sang giai đoạn khác.");
        }

        if (to is OpportunityStage.Won)
        {
            // Spec: Won requires linking a Quote or a Tender. Quote/Tender modules
            // ship in NIH-84/NIH-85 — until they exist we accept but do not enforce
            // one of the ids. Once those modules ship, uncomment the guard below.
            //
            // if (!request.WonQuoteId.HasValue && !request.WonTenderId.HasValue)
            //     throw new OpportunityOperationException("Won cần chọn Báo giá hoặc Gói thầu trúng.");

            op.WonQuoteId = request.WonQuoteId;
            op.WonTenderId = request.WonTenderId;
            op.LostReasonCode = null;
            op.LostNote = null;
            op.WinProbability = 100;
        }
        else if (to is OpportunityStage.Lost)
        {
            if (string.IsNullOrWhiteSpace(request.LostReasonCode))
            {
                throw new OpportunityOperationException("Cơ hội chuyển sang Lost phải chọn lý do thua.");
            }
            var reasonExists = await db.MasterDataOptions
                .AsNoTracking()
                .AnyAsync(m =>
                    m.Category == LostReasonMasterDataCategory
                    && m.Code == request.LostReasonCode
                    && m.IsActive, ct);
            if (!reasonExists)
            {
                throw new OpportunityOperationException(
                    $"Lý do thua '{request.LostReasonCode}' không hợp lệ.");
            }
            if (string.IsNullOrWhiteSpace(request.LostNote))
            {
                throw new OpportunityOperationException("Cơ hội chuyển sang Lost phải kèm ghi chú.");
            }

            op.LostReasonCode = request.LostReasonCode.Trim();
            op.LostNote = request.LostNote.Trim();
            op.WonQuoteId = null;
            op.WonTenderId = null;
            op.WinProbability = 0;
        }
        else
        {
            // Non-terminal target: clear any residual terminal metadata that
            // may exist from a previous state (defensive; shouldn't happen).
            op.LostReasonCode = null;
            op.LostNote = null;
            op.WonQuoteId = null;
            op.WonTenderId = null;
        }

        op.Stage = to;
        var now = DateTime.UtcNow;
        op.ClosedAt = to is OpportunityStage.Won or OpportunityStage.Lost ? now : null;
        op.UpdatedAt = now;
        op.UpdatedByUserId = callerUserId;

        db.OpportunityActivities.Add(new OpportunityActivity
        {
            OpportunityId = op.Id,
            Type = OpportunityActivityType.StageChange,
            OccurredAt = now,
            Content = BuildStageChangeContent(from, to, request),
            CreatedByUserId = callerUserId,
            CreatedAt = now,
        });

        await db.SaveChangesAsync(ct);

        if (to is OpportunityStage.Won or OpportunityStage.Lost && op.OwnerUserId.HasValue)
        {
            var customerName = await db.Customers.AsNoTracking()
                .Where(c => c.Id == op.CustomerId)
                .Select(c => c.Name)
                .FirstOrDefaultAsync(ct) ?? string.Empty;
            await FireStageChangedAsync(op, customerName, op.OwnerUserId.Value, to);
        }

        return await GetAsync(op.Id, callerUserId, canSeeAll: true, ct);
    }

    // -------- Delete --------

    public async Task<bool> DeleteAsync(
        int id,
        int callerUserId,
        bool canManage,
        bool canSeeAll,
        CancellationToken ct = default)
    {
        if (!canManage) return false;

        var op = await db.Opportunities.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (op is null) return false;
        if (!canSeeAll && op.OwnerUserId != callerUserId) return false;

        // Spec: block delete if linked Quote/Contract exists. Those modules
        // ship in NIH-84/NIH-87 — placeholder guard until they land.
        // TODO(NIH-84/NIH-87): re-enable once linked entities exist.

        db.Opportunities.Remove(op);
        await db.SaveChangesAsync(ct);
        return true;
    }

    // -------- Activities --------

    public async Task<OpportunityActivityResponse?> AddActivityAsync(
        int opportunityId,
        AddOpportunityActivityRequest request,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default)
    {
        var op = await db.Opportunities.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == opportunityId, ct);
        if (op is null) return null;
        if (!canSeeAll && op.OwnerUserId != callerUserId) return null;

        var now = DateTime.UtcNow;
        var entity = new OpportunityActivity
        {
            OpportunityId = opportunityId,
            Type = request.Type,
            OccurredAt = request.OccurredAt ?? now,
            Content = request.Content.Trim(),
            CreatedByUserId = callerUserId,
            CreatedAt = now,
        };
        db.OpportunityActivities.Add(entity);
        await db.SaveChangesAsync(ct);

        // Bump the opportunity's UpdatedAt so timeline entries surface in listings.
        var track = await db.Opportunities.FirstAsync(o => o.Id == opportunityId, ct);
        track.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        var name = await db.Users.AsNoTracking()
            .Where(u => u.Id == callerUserId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(ct);

        return new OpportunityActivityResponse
        {
            Id = entity.Id,
            Type = entity.Type,
            OccurredAt = entity.OccurredAt,
            Content = entity.Content,
            CreatedByUserId = entity.CreatedByUserId,
            CreatedByName = name,
            CreatedAt = entity.CreatedAt,
        };
    }

    // -------- Helpers --------

    private IQueryable<Opportunity> BuildFilteredQuery(
        int callerUserId,
        bool canSeeAll,
        OpportunityStage? stage,
        int? customerId,
        int? ownerUserId,
        DateTime? expectedCloseFrom,
        DateTime? expectedCloseTo,
        decimal? minValue,
        decimal? maxValue,
        string? search)
    {
        var query = db.Opportunities.AsNoTracking().AsQueryable();

        if (!canSeeAll)
        {
            query = query.Where(o => o.OwnerUserId == callerUserId);
        }
        else if (ownerUserId.HasValue)
        {
            query = query.Where(o => o.OwnerUserId == ownerUserId.Value);
        }

        if (stage.HasValue)
        {
            query = query.Where(o => o.Stage == stage.Value);
        }
        if (customerId.HasValue)
        {
            query = query.Where(o => o.CustomerId == customerId.Value);
        }
        if (expectedCloseFrom.HasValue)
        {
            query = query.Where(o => o.ExpectedCloseDate >= expectedCloseFrom.Value);
        }
        if (expectedCloseTo.HasValue)
        {
            query = query.Where(o => o.ExpectedCloseDate <= expectedCloseTo.Value);
        }
        if (minValue.HasValue)
        {
            query = query.Where(o => o.EstimatedValue >= minValue.Value);
        }
        if (maxValue.HasValue)
        {
            query = query.Where(o => o.EstimatedValue <= maxValue.Value);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var like = $"%{search.Trim()}%";
            query = query.Where(o =>
                EF.Functions.Like(o.Name, like)
                || EF.Functions.Like(o.Customer.Name, like));
        }

        return query;
    }

    private static string BuildStageChangeContent(
        OpportunityStage from,
        OpportunityStage to,
        ChangeOpportunityStageRequest request)
    {
        var extra = to switch
        {
            OpportunityStage.Won => request.WonQuoteId.HasValue
                ? $" · quote={request.WonQuoteId.Value}"
                : request.WonTenderId.HasValue
                    ? $" · tender={request.WonTenderId.Value}"
                    : string.Empty,
            OpportunityStage.Lost => $" · reason={request.LostReasonCode}",
            _ => string.Empty,
        };
        return $"Stage {from} → {to}{extra}";
    }

    private async Task FireAssignedAsync(Opportunity op, string customerName, int ownerUserId)
    {
        try
        {
            await notifications.NotifyFromTemplateAsync(
                ownerUserId,
                OpportunityAssignedTemplate,
                new Dictionary<string, string>
                {
                    ["opportunityName"] = op.Name,
                    ["customerName"] = customerName,
                    ["estimatedValue"] = op.EstimatedValue.ToString("N0"),
                },
                refEntityType: EntityTypes.Opportunity,
                refEntityId: op.Id,
                linkUrl: $"/admin/opportunities/{op.Id}");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Opportunity {OpportunityId} assigned to user {OwnerId} but notification dispatch failed.",
                op.Id, ownerUserId);
        }
    }

    private async Task FireStageChangedAsync(
        Opportunity op,
        string customerName,
        int ownerUserId,
        OpportunityStage stage)
    {
        try
        {
            await notifications.NotifyFromTemplateAsync(
                ownerUserId,
                OpportunityStageChangedTemplate,
                new Dictionary<string, string>
                {
                    ["opportunityName"] = op.Name,
                    ["customerName"] = customerName,
                    ["stage"] = stage.ToString(),
                },
                refEntityType: EntityTypes.Opportunity,
                refEntityId: op.Id,
                linkUrl: $"/admin/opportunities/{op.Id}");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Opportunity {OpportunityId} stage change notification failed.", op.Id);
        }
    }

    private static OpportunityResponse Map(
        Opportunity op,
        string? customerName,
        string? ownerName,
        IEnumerable<OpportunityActivity>? activities)
    {
        return new OpportunityResponse
        {
            Id = op.Id,
            Name = op.Name,
            CustomerId = op.CustomerId,
            CustomerName = customerName,
            OwnerUserId = op.OwnerUserId,
            OwnerName = ownerName,
            EstimatedValue = op.EstimatedValue,
            WinProbability = op.WinProbability,
            ExpectedCloseDate = op.ExpectedCloseDate,
            Stage = op.Stage,
            LostReasonCode = op.LostReasonCode,
            LostNote = op.LostNote,
            ClosedAt = op.ClosedAt,
            WonQuoteId = op.WonQuoteId,
            WonTenderId = op.WonTenderId,
            Note = op.Note,
            CreatedAt = op.CreatedAt,
            UpdatedAt = op.UpdatedAt,
            Activities = activities is null
                ? new()
                : activities
                    .OrderByDescending(a => a.OccurredAt)
                    .ThenByDescending(a => a.Id)
                    .Select(a => new OpportunityActivityResponse
                    {
                        Id = a.Id,
                        Type = a.Type,
                        OccurredAt = a.OccurredAt,
                        Content = a.Content,
                        CreatedByUserId = a.CreatedByUserId,
                        CreatedByName = a.CreatedBy?.FullName,
                        CreatedAt = a.CreatedAt,
                    })
                    .ToList(),
        };
    }
}
