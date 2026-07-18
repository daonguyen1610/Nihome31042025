using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>
/// M2 Shop Drawing service (NIH-116 slice 1) — see
/// <see cref="IShopDrawingService"/>.
/// </summary>
public class ShopDrawingService(
    AppDbContext db,
    ILogger<ShopDrawingService> logger) : IShopDrawingService
{
    private const int MaxPageSize = 200;
    private const int MaxBulkDelete = 100;
    private const string DisciplineCategory = "design_discipline";

    /// <summary>Human-friendly prefixes used when auto-generating <c>DrawingCode</c>.</summary>
    private static readonly Dictionary<string, string> CodePrefix = new(StringComparer.OrdinalIgnoreCase)
    {
        ["architecture"] = "KT-SD",
        ["structure"] = "KC-SD",
        ["mep"] = "MEP-SD",
        ["interior"] = "NT-SD",
    };

    public async Task<ShopDrawingListResponse> ListAsync(ShopDrawingListParams p, CancellationToken ct = default)
    {
        var page = p.Page < 1 ? 1 : p.Page;
        var pageSize = Math.Clamp(p.PageSize <= 0 ? 50 : p.PageSize, 1, MaxPageSize);

        var q = db.ShopDrawings
            .AsNoTracking()
            .Include(d => d.DesignProject)
            .Include(d => d.Owner)
            .AsQueryable();

        if (p.DesignProjectId.HasValue) q = q.Where(d => d.DesignProjectId == p.DesignProjectId.Value);
        if (!string.IsNullOrWhiteSpace(p.DisciplineCode))
        {
            var code = p.DisciplineCode.Trim();
            q = q.Where(d => d.DisciplineCode == code);
        }
        if (!string.IsNullOrWhiteSpace(p.ConstructionItem))
        {
            var term = p.ConstructionItem.Trim();
            q = q.Where(d => EF.Functions.Like(d.ConstructionItem, $"%{term}%"));
        }
        if (!string.IsNullOrWhiteSpace(p.Status))
        {
            var statuses = p.Status.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => Enum.TryParse<ShopDrawingStatus>(s, true, out var v) ? (ShopDrawingStatus?)v : null)
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();
            if (statuses.Count > 0) q = q.Where(d => statuses.Contains(d.Status));
        }
        if (!string.IsNullOrWhiteSpace(p.Search))
        {
            var term = p.Search.Trim();
            q = q.Where(d => EF.Functions.Like(d.Title, $"%{term}%")
                          || EF.Functions.Like(d.DrawingCode, $"%{term}%")
                          || EF.Functions.Like(d.ConstructionItem, $"%{term}%"));
        }

        var total = await q.CountAsync(ct);
        var rows = await q
            .OrderBy(d => d.DisciplineCode)
            .ThenBy(d => d.ConstructionItem)
            .ThenBy(d => d.DrawingCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var disciplineCodes = rows.Select(r => r.DisciplineCode).Distinct().ToList();
        var labelByCode = disciplineCodes.Count == 0
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : await db.MasterDataOptions.AsNoTracking()
                .Where(m => m.Category == DisciplineCategory && disciplineCodes.Contains(m.Code))
                .ToDictionaryAsync(m => m.Code, m => m.Name, StringComparer.OrdinalIgnoreCase, ct);

        // Per-status roll-up computed against the same scope (project +
        // discipline filter) so the header pills line up with the visible
        // list even when pagination is in play.
        var statusScope = db.ShopDrawings.AsNoTracking();
        if (p.DesignProjectId.HasValue) statusScope = statusScope.Where(d => d.DesignProjectId == p.DesignProjectId.Value);
        if (!string.IsNullOrWhiteSpace(p.DisciplineCode))
        {
            var code = p.DisciplineCode.Trim();
            statusScope = statusScope.Where(d => d.DisciplineCode == code);
        }
        var statusCounts = await statusScope
            .GroupBy(d => d.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status.ToString(), x => x.Count, ct);

        return new ShopDrawingListResponse
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Items = rows.Select(r => Map(r, labelByCode)).ToList(),
            StatusCounts = statusCounts,
        };
    }

    public async Task<ShopDrawingResponse?> GetAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.ShopDrawings
            .AsNoTracking()
            .Include(d => d.DesignProject)
            .Include(d => d.Owner)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
        if (entity is null) return null;

        var label = await db.MasterDataOptions.AsNoTracking()
            .Where(m => m.Category == DisciplineCategory && m.Code == entity.DisciplineCode)
            .Select(m => m.Name)
            .FirstOrDefaultAsync(ct);
        var lookup = label is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [entity.DisciplineCode] = label };
        return Map(entity, lookup);
    }

    public async Task<ShopDrawingResponse> CreateAsync(CreateShopDrawingRequest request, int callerUserId, CancellationToken ct = default)
    {
        var title = (request.Title ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ShopDrawingOperationException("Tên bản vẽ là bắt buộc.");
        }

        var discipline = (request.DisciplineCode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(discipline))
        {
            throw new ShopDrawingOperationException("Bộ môn là bắt buộc.");
        }

        var constructionItem = (request.ConstructionItem ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(constructionItem))
        {
            throw new ShopDrawingOperationException("Hạng mục thi công là bắt buộc.");
        }

        var project = await db.DesignProjects.FirstOrDefaultAsync(dp => dp.Id == request.DesignProjectId, ct);
        if (project is null)
        {
            throw new ShopDrawingOperationException($"Dự án #{request.DesignProjectId} không tồn tại.");
        }

        // Guard: only create Shop Drawings while the parent project is
        // actually at the Shop Drawing stage. Otherwise the drawing list
        // would grow retroactively while the workflow is closed.
        if (project.CurrentStage != DesignProjectStage.ShopDrawing)
        {
            throw new ShopDrawingOperationException(
                "Chỉ tạo được bản vẽ Shop Drawing khi dự án đang ở giai đoạn Shop Drawing.");
        }

        var disciplineExists = await db.MasterDataOptions
            .AnyAsync(m => m.Category == DisciplineCategory && m.Code == discipline && m.IsActive, ct);
        if (!disciplineExists)
        {
            throw new ShopDrawingOperationException($"Bộ môn '{discipline}' không hợp lệ.");
        }

        if (request.OwnerUserId.HasValue &&
            !await db.Users.AnyAsync(u => u.Id == request.OwnerUserId.Value, ct))
        {
            throw new ShopDrawingOperationException($"Người phụ trách #{request.OwnerUserId} không tồn tại.");
        }

        var code = await AllocateDrawingCodeAsync(request.DesignProjectId, discipline, ct);

        var entity = new ShopDrawing
        {
            DesignProjectId = request.DesignProjectId,
            DisciplineCode = discipline,
            ConstructionItem = constructionItem,
            DrawingCode = code,
            Title = title,
            Description = TrimOrNull(request.Description),
            OwnerUserId = request.OwnerUserId,
            Note = TrimOrNull(request.Note),
            Status = ShopDrawingStatus.Drafting,
            CreatedByUserId = callerUserId,
            UpdatedByUserId = callerUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.ShopDrawings.Add(entity);
        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "ShopDrawing {Id} ({Code}) created for project {ProjectId} by user {UserId}",
            entity.Id, entity.DrawingCode, request.DesignProjectId, callerUserId);
        return (await GetAsync(entity.Id, ct))!;
    }

    public async Task<ShopDrawingResponse?> UpdateAsync(int id, UpdateShopDrawingRequest request,
        int callerUserId, CancellationToken ct = default)
    {
        var entity = await db.ShopDrawings.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (entity is null) return null;

        var title = (request.Title ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ShopDrawingOperationException("Tên bản vẽ là bắt buộc.");
        }
        var discipline = (request.DisciplineCode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(discipline))
        {
            throw new ShopDrawingOperationException("Bộ môn là bắt buộc.");
        }
        var constructionItem = (request.ConstructionItem ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(constructionItem))
        {
            throw new ShopDrawingOperationException("Hạng mục thi công là bắt buộc.");
        }

        // Locked once the drawing has hit released / rejected. FE hides
        // the edit button; the server re-checks defensively.
        if (entity.Status is ShopDrawingStatus.Released or ShopDrawingStatus.Rejected)
        {
            throw new ShopDrawingOperationException(
                "Bản vẽ đã ở trạng thái kết thúc không thể chỉnh sửa.");
        }

        // Metadata like discipline / construction item change the grouping
        // and (for discipline) invalidate the drawing code. Only allow
        // while the drawing is still a draft to avoid rewriting history
        // rows that reviewers already looked at.
        if (!string.Equals(discipline, entity.DisciplineCode, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(constructionItem, entity.ConstructionItem, StringComparison.Ordinal))
        {
            if (entity.Status != ShopDrawingStatus.Drafting)
            {
                throw new ShopDrawingOperationException(
                    "Không thể đổi bộ môn/hạng mục khi bản vẽ đã qua bước Đang vẽ.");
            }
            if (!string.Equals(discipline, entity.DisciplineCode, StringComparison.OrdinalIgnoreCase))
            {
                var newDisciplineExists = await db.MasterDataOptions
                    .AnyAsync(m => m.Category == DisciplineCategory && m.Code == discipline && m.IsActive, ct);
                if (!newDisciplineExists)
                {
                    throw new ShopDrawingOperationException($"Bộ môn '{discipline}' không hợp lệ.");
                }
                entity.DisciplineCode = discipline;
                entity.DrawingCode = await AllocateDrawingCodeAsync(entity.DesignProjectId, discipline, ct);
            }
            entity.ConstructionItem = constructionItem;
        }

        if (request.OwnerUserId.HasValue &&
            !await db.Users.AnyAsync(u => u.Id == request.OwnerUserId.Value, ct))
        {
            throw new ShopDrawingOperationException($"Người phụ trách #{request.OwnerUserId} không tồn tại.");
        }

        entity.Title = title;
        entity.Description = TrimOrNull(request.Description);
        entity.OwnerUserId = request.OwnerUserId;
        entity.Note = TrimOrNull(request.Note);
        entity.UpdatedByUserId = callerUserId;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.ShopDrawings.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (entity is null) return false;

        if (entity.Status != ShopDrawingStatus.Drafting)
        {
            throw new ShopDrawingOperationException(
                "Chỉ xoá được bản vẽ khi còn ở trạng thái Đang vẽ.");
        }

        db.ShopDrawings.Remove(entity);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("ShopDrawing {Id} deleted", id);
        return true;
    }

    public async Task<ShopDrawingBulkDeleteResponse> BulkDeleteAsync(IReadOnlyList<int> ids, CancellationToken ct = default)
    {
        if (ids is null || ids.Count == 0)
        {
            throw new ShopDrawingOperationException("Danh sách bản vẽ cần xoá là bắt buộc.");
        }
        if (ids.Count > MaxBulkDelete)
        {
            throw new ShopDrawingOperationException(
                $"Chỉ xoá tối đa {MaxBulkDelete} bản vẽ mỗi lần.");
        }

        var distinctIds = ids.Distinct().ToList();
        var rows = await db.ShopDrawings
            .Where(d => distinctIds.Contains(d.Id))
            .ToListAsync(ct);

        var response = new ShopDrawingBulkDeleteResponse { Requested = distinctIds.Count };
        var found = rows.Select(r => r.Id).ToHashSet();
        foreach (var missing in distinctIds.Where(id => !found.Contains(id)))
        {
            response.Failures.Add(new ShopDrawingBulkDeleteFailure
            {
                Id = missing,
                Message = $"Bản vẽ #{missing} không tồn tại.",
            });
        }

        var toDelete = new List<ShopDrawing>();
        foreach (var row in rows)
        {
            if (row.Status != ShopDrawingStatus.Drafting)
            {
                response.Failures.Add(new ShopDrawingBulkDeleteFailure
                {
                    Id = row.Id,
                    Message = "Chỉ xoá được bản vẽ ở trạng thái Đang vẽ.",
                });
                continue;
            }
            toDelete.Add(row);
        }

        if (toDelete.Count > 0)
        {
            db.ShopDrawings.RemoveRange(toDelete);
            await db.SaveChangesAsync(ct);
            response.Deleted = toDelete.Count;
            logger.LogInformation("ShopDrawing bulk-deleted {Count} rows ({Ids})",
                toDelete.Count, string.Join(",", toDelete.Select(r => r.Id)));
        }
        return response;
    }

    public async Task<ShopDrawingResponse?> TransitionStatusAsync(int id,
        TransitionShopDrawingStatusRequest request, int callerUserId, CancellationToken ct = default)
    {
        if (!Enum.TryParse<ShopDrawingStatus>(request.Status, true, out var next))
        {
            throw new ShopDrawingOperationException($"Trạng thái '{request.Status}' không hợp lệ.");
        }

        var entity = await db.ShopDrawings.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (entity is null) return null;

        EnsureTransitionAllowed(entity.Status, next);

        entity.Status = next;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedByUserId = callerUserId;
        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "ShopDrawing {Id} transitioned to {Status} by user {UserId}", id, next, callerUserId);
        return await GetAsync(id, ct);
    }

    // ------------------------------ Helpers ---------------------------------

    private async Task<string> AllocateDrawingCodeAsync(int designProjectId, string discipline, CancellationToken ct)
    {
        var prefix = CodePrefix.TryGetValue(discipline, out var p) ? p : $"{discipline.ToUpperInvariant()}-SD";
        var used = await db.ShopDrawings
            .Where(d => d.DesignProjectId == designProjectId
                     && d.DisciplineCode == discipline)
            .Select(d => d.DrawingCode)
            .ToListAsync(ct);
        var maxSeq = used
            .Select(c =>
            {
                var idx = c.LastIndexOf('-');
                if (idx < 0 || idx == c.Length - 1) return 0;
                return int.TryParse(c[(idx + 1)..], out var n) ? n : 0;
            })
            .DefaultIfEmpty(0)
            .Max();
        return $"{prefix}-{maxSeq + 1:D3}";
    }

    /// <summary>
    /// Slice-1 state machine for a Shop Drawing:
    /// <list type="bullet">
    ///   <item>Drafting → InReview | Rejected</item>
    ///   <item>InReview → Approved | Drafting | Rejected</item>
    ///   <item>Approved → PendingIfc | Drafting | Rejected</item>
    ///   <item>PendingIfc → Approved | Rejected</item>
    ///   <item>Released is set by the NIH-118 IFC release flow (not this endpoint)</item>
    ///   <item>Rejected is terminal</item>
    /// </list>
    /// </summary>
    private static void EnsureTransitionAllowed(ShopDrawingStatus from, ShopDrawingStatus to)
    {
        if (from == to) return; // idempotent no-op
        bool ok = (from, to) switch
        {
            (ShopDrawingStatus.Drafting, ShopDrawingStatus.InReview) => true,
            (ShopDrawingStatus.Drafting, ShopDrawingStatus.Rejected) => true,

            (ShopDrawingStatus.InReview, ShopDrawingStatus.Approved) => true,
            (ShopDrawingStatus.InReview, ShopDrawingStatus.Drafting) => true,
            (ShopDrawingStatus.InReview, ShopDrawingStatus.Rejected) => true,

            (ShopDrawingStatus.Approved, ShopDrawingStatus.PendingIfc) => true,
            (ShopDrawingStatus.Approved, ShopDrawingStatus.Drafting) => true,
            (ShopDrawingStatus.Approved, ShopDrawingStatus.Rejected) => true,

            (ShopDrawingStatus.PendingIfc, ShopDrawingStatus.Approved) => true,
            (ShopDrawingStatus.PendingIfc, ShopDrawingStatus.Rejected) => true,

            // Released is intentionally NOT reachable here — the NIH-118
            // IFC release flow is the only writer for that state so the
            // dated stamp / watermark side-effect can never be bypassed.
            _ => false,
        };
        if (!ok)
        {
            throw new ShopDrawingOperationException(
                $"Không thể chuyển từ '{from}' sang '{to}'.");
        }
    }

    private static string? TrimOrNull(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static ShopDrawingResponse Map(ShopDrawing d, IReadOnlyDictionary<string, string> labelByCode) => new()
    {
        Id = d.Id,
        DesignProjectId = d.DesignProjectId,
        DesignProjectCode = d.DesignProject?.ProjectCode,
        DisciplineCode = d.DisciplineCode,
        DisciplineLabel = labelByCode.TryGetValue(d.DisciplineCode, out var label) ? label : null,
        ConstructionItem = d.ConstructionItem,
        DrawingCode = d.DrawingCode,
        Title = d.Title,
        Description = d.Description,
        OwnerUserId = d.OwnerUserId,
        OwnerName = d.Owner?.FullName,
        Status = d.Status.ToString(),
        Note = d.Note,
        CreatedAt = d.CreatedAt,
        UpdatedAt = d.UpdatedAt,
    };
}
