using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>
/// M2 Drawing Revision service (NIH-117 slice 1) — see
/// <see cref="IDrawingRevisionService"/>.
/// </summary>
public class DrawingRevisionService(
    AppDbContext db,
    ILogger<DrawingRevisionService> logger) : IDrawingRevisionService
{
    private const int MaxPageSize = 200;
    private const string ReasonCategory = "drawing_revision_reason";

    public async Task<DrawingRevisionListResponse> ListAsync(DrawingRevisionListParams p, CancellationToken ct = default)
    {
        var page = p.Page < 1 ? 1 : p.Page;
        var pageSize = Math.Clamp(p.PageSize <= 0 ? 100 : p.PageSize, 1, MaxPageSize);

        var q = db.DrawingRevisions
            .AsNoTracking()
            .Include(r => r.CreatedBy)
            .AsQueryable();

        DrawingRevisionTargetType? targetType = null;
        if (!string.IsNullOrWhiteSpace(p.TargetType))
        {
            if (!Enum.TryParse<DrawingRevisionTargetType>(p.TargetType, true, out var tt))
            {
                throw new DrawingRevisionOperationException($"Loại đối tượng '{p.TargetType}' không hợp lệ.");
            }
            targetType = tt;
            q = q.Where(r => r.TargetType == tt);
        }
        if (p.TargetId.HasValue) q = q.Where(r => r.TargetId == p.TargetId.Value);

        var total = await q.CountAsync(ct);
        var rows = await q
            .OrderByDescending(r => r.RevisionNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var reasonCodes = rows.Select(r => r.ReasonCode).Distinct().ToList();
        var labelByReason = reasonCodes.Count == 0
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : await db.MasterDataOptions.AsNoTracking()
                .Where(m => m.Category == ReasonCategory && reasonCodes.Contains(m.Code))
                .ToDictionaryAsync(m => m.Code, m => m.Name, StringComparer.OrdinalIgnoreCase, ct);

        var targetIds = rows.Select(r => r.TargetId).Distinct().ToList();
        var basicTargets = targetType is null or DrawingRevisionTargetType.BasicDesignDoc
            ? await db.BasicDesignDocs.AsNoTracking()
                .Where(d => targetIds.Contains(d.Id))
                .Select(d => new { d.Id, d.DocumentCode, d.Title })
                .ToDictionaryAsync(d => d.Id, d => (d.DocumentCode, d.Title), ct)
            : new Dictionary<int, (string DocumentCode, string Title)>();
        var shopTargets = targetType is null or DrawingRevisionTargetType.ShopDrawing
            ? await db.ShopDrawings.AsNoTracking()
                .Where(d => targetIds.Contains(d.Id))
                .Select(d => new { d.Id, d.DrawingCode, d.Title })
                .ToDictionaryAsync(d => d.Id, d => (d.DrawingCode, d.Title), ct)
            : new Dictionary<int, (string DrawingCode, string Title)>();

        return new DrawingRevisionListResponse
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Items = rows.Select(r => Map(r, labelByReason, basicTargets, shopTargets)).ToList(),
        };
    }

    public async Task<DrawingRevisionResponse?> GetAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.DrawingRevisions
            .AsNoTracking()
            .Include(r => r.CreatedBy)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity is null) return null;

        var reasonLabel = await db.MasterDataOptions.AsNoTracking()
            .Where(m => m.Category == ReasonCategory && m.Code == entity.ReasonCode)
            .Select(m => m.Name)
            .FirstOrDefaultAsync(ct);
        var reasonLookup = reasonLabel is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [entity.ReasonCode] = reasonLabel };

        var (targetCode, targetTitle) = await ResolveTargetAsync(entity.TargetType, entity.TargetId, ct);
        var basicMap = entity.TargetType == DrawingRevisionTargetType.BasicDesignDoc && targetCode is not null
            ? new Dictionary<int, (string DocumentCode, string Title)> { [entity.TargetId] = (targetCode, targetTitle ?? string.Empty) }
            : new Dictionary<int, (string DocumentCode, string Title)>();
        var shopMap = entity.TargetType == DrawingRevisionTargetType.ShopDrawing && targetCode is not null
            ? new Dictionary<int, (string DrawingCode, string Title)> { [entity.TargetId] = (targetCode, targetTitle ?? string.Empty) }
            : new Dictionary<int, (string DrawingCode, string Title)>();

        return Map(entity, reasonLookup, basicMap, shopMap);
    }

    public async Task<DrawingRevisionResponse> CreateAsync(CreateDrawingRevisionRequest request, int callerUserId, CancellationToken ct = default)
    {
        if (!Enum.TryParse<DrawingRevisionTargetType>(request.TargetType, true, out var targetType))
        {
            throw new DrawingRevisionOperationException($"Loại đối tượng '{request.TargetType}' không hợp lệ.");
        }

        var note = (request.Note ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(note))
        {
            throw new DrawingRevisionOperationException("Ghi chú thay đổi là bắt buộc.");
        }

        var reason = (request.ReasonCode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DrawingRevisionOperationException("Lý do thay đổi là bắt buộc.");
        }

        // Verify the master-data reason exists + is active.
        var reasonExists = await db.MasterDataOptions
            .AnyAsync(m => m.Category == ReasonCategory && m.Code == reason && m.IsActive, ct);
        if (!reasonExists)
        {
            throw new DrawingRevisionOperationException($"Lý do '{reason}' không hợp lệ.");
        }

        // Verify the target drawing exists.
        var targetExists = targetType switch
        {
            DrawingRevisionTargetType.BasicDesignDoc =>
                await db.BasicDesignDocs.AnyAsync(d => d.Id == request.TargetId, ct),
            DrawingRevisionTargetType.ShopDrawing =>
                await db.ShopDrawings.AnyAsync(d => d.Id == request.TargetId, ct),
            _ => false,
        };
        if (!targetExists)
        {
            throw new DrawingRevisionOperationException($"Bản vẽ #{request.TargetId} không tồn tại.");
        }

        // Flip the current previous revision to superseded.
        var previous = await db.DrawingRevisions
            .Where(r => r.TargetType == targetType && r.TargetId == request.TargetId && r.IsCurrent)
            .ToListAsync(ct);
        foreach (var p in previous)
        {
            p.IsCurrent = false;
        }

        // Auto-allocate the next revision number per-target.
        var lastNumber = await db.DrawingRevisions
            .Where(r => r.TargetType == targetType && r.TargetId == request.TargetId)
            .Select(r => (int?)r.RevisionNumber)
            .MaxAsync(ct) ?? 0;

        var entity = new DrawingRevision
        {
            TargetType = targetType,
            TargetId = request.TargetId,
            RevisionNumber = lastNumber + 1,
            ReasonCode = reason,
            Note = note,
            IsCurrent = true,
            CreatedByUserId = callerUserId,
            CreatedAt = DateTime.UtcNow,
        };
        db.DrawingRevisions.Add(entity);
        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "DrawingRevision R{Number} created for {TargetType} #{TargetId} by user {UserId}",
            entity.RevisionNumber, targetType, request.TargetId, callerUserId);
        return (await GetAsync(entity.Id, ct))!;
    }

    public async Task<DrawingRevisionDiffResponse?> DiffAsync(int fromId, int toId, CancellationToken ct = default)
    {
        var from = await db.DrawingRevisions
            .AsNoTracking()
            .Include(r => r.CreatedBy)
            .FirstOrDefaultAsync(r => r.Id == fromId, ct);
        var to = await db.DrawingRevisions
            .AsNoTracking()
            .Include(r => r.CreatedBy)
            .FirstOrDefaultAsync(r => r.Id == toId, ct);
        if (from is null || to is null) return null;
        if (from.TargetType != to.TargetType || from.TargetId != to.TargetId)
        {
            throw new DrawingRevisionOperationException(
                "Chỉ so sánh được các revision của cùng một bản vẽ.");
        }

        var codes = new[] { from.ReasonCode, to.ReasonCode }.Distinct().ToList();
        var labelByReason = await db.MasterDataOptions.AsNoTracking()
            .Where(m => m.Category == ReasonCategory && codes.Contains(m.Code))
            .ToDictionaryAsync(m => m.Code, m => m.Name, StringComparer.OrdinalIgnoreCase, ct);

        var (targetCode, targetTitle) = await ResolveTargetAsync(from.TargetType, from.TargetId, ct);
        var basicMap = from.TargetType == DrawingRevisionTargetType.BasicDesignDoc && targetCode is not null
            ? new Dictionary<int, (string DocumentCode, string Title)> { [from.TargetId] = (targetCode, targetTitle ?? string.Empty) }
            : new Dictionary<int, (string DocumentCode, string Title)>();
        var shopMap = from.TargetType == DrawingRevisionTargetType.ShopDrawing && targetCode is not null
            ? new Dictionary<int, (string DrawingCode, string Title)> { [from.TargetId] = (targetCode, targetTitle ?? string.Empty) }
            : new Dictionary<int, (string DrawingCode, string Title)>();

        // Metadata-only diff for slice 1 — file diff arrives with the
        // NIH-117 slice 2 upload endpoint.
        var changes = new List<string>();
        if (!string.Equals(from.ReasonCode, to.ReasonCode, StringComparison.OrdinalIgnoreCase))
        {
            changes.Add($"Lý do: {LabelOr(labelByReason, from.ReasonCode)} → {LabelOr(labelByReason, to.ReasonCode)}");
        }
        if (!string.Equals(from.Note, to.Note, StringComparison.Ordinal))
        {
            changes.Add("Ghi chú thay đổi.");
        }
        if (from.CreatedByUserId != to.CreatedByUserId)
        {
            changes.Add($"Người tạo: {from.CreatedBy?.FullName ?? from.CreatedByUserId.ToString()} → {to.CreatedBy?.FullName ?? to.CreatedByUserId.ToString()}");
        }
        if (changes.Count == 0)
        {
            changes.Add("Không có thay đổi metadata giữa hai revision.");
        }

        return new DrawingRevisionDiffResponse
        {
            From = Map(from, labelByReason, basicMap, shopMap),
            To = Map(to, labelByReason, basicMap, shopMap),
            Changes = changes,
        };
    }

    // ------------------------------ Helpers ---------------------------------

    private async Task<(string? Code, string? Title)> ResolveTargetAsync(
        DrawingRevisionTargetType targetType, int targetId, CancellationToken ct)
    {
        return targetType switch
        {
            DrawingRevisionTargetType.BasicDesignDoc => await db.BasicDesignDocs.AsNoTracking()
                .Where(d => d.Id == targetId)
                .Select(d => new ValueTuple<string?, string?>(d.DocumentCode, d.Title))
                .FirstOrDefaultAsync(ct),
            DrawingRevisionTargetType.ShopDrawing => await db.ShopDrawings.AsNoTracking()
                .Where(d => d.Id == targetId)
                .Select(d => new ValueTuple<string?, string?>(d.DrawingCode, d.Title))
                .FirstOrDefaultAsync(ct),
            _ => (null, null),
        };
    }

    private static string LabelOr(IReadOnlyDictionary<string, string> map, string code) =>
        map.TryGetValue(code, out var label) ? label : code;

    private static DrawingRevisionResponse Map(
        DrawingRevision r,
        IReadOnlyDictionary<string, string> reasonLabelByCode,
        IReadOnlyDictionary<int, (string DocumentCode, string Title)> basicTargets,
        IReadOnlyDictionary<int, (string DrawingCode, string Title)> shopTargets)
    {
        string? targetCode = null;
        string? targetTitle = null;
        if (r.TargetType == DrawingRevisionTargetType.BasicDesignDoc &&
            basicTargets.TryGetValue(r.TargetId, out var b))
        {
            targetCode = b.DocumentCode;
            targetTitle = b.Title;
        }
        else if (r.TargetType == DrawingRevisionTargetType.ShopDrawing &&
                 shopTargets.TryGetValue(r.TargetId, out var s))
        {
            targetCode = s.DrawingCode;
            targetTitle = s.Title;
        }
        return new DrawingRevisionResponse
        {
            Id = r.Id,
            TargetType = r.TargetType.ToString(),
            TargetId = r.TargetId,
            TargetCode = targetCode,
            TargetTitle = targetTitle,
            RevisionNumber = r.RevisionNumber,
            ReasonCode = r.ReasonCode,
            ReasonLabel = reasonLabelByCode.TryGetValue(r.ReasonCode, out var label) ? label : null,
            Note = r.Note,
            IsCurrent = r.IsCurrent,
            CreatedAt = r.CreatedAt,
            CreatedByUserId = r.CreatedByUserId,
            CreatedByName = r.CreatedBy?.FullName,
        };
    }
}
