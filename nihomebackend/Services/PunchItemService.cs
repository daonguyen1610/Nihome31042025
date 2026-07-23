using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>
/// Slice-1 implementation of <see cref="IPunchItemService"/> for the
/// NIH-146 punch list. Owns the Open → InProgress → Fixed → Verified
/// state machine, the reopen counter, and the overdue roll-up used by
/// the list header.
/// </summary>
public class PunchItemService(
    AppDbContext db,
    ILogger<PunchItemService> logger) : IPunchItemService
{
    private const int MaxPageSize = 200;
    private const int MaxBulkDelete = 100;

    public async Task<PunchItemListResponse> ListAsync(PunchItemListParams p, CancellationToken ct = default)
    {
        var page = p.Page < 1 ? 1 : p.Page;
        var pageSize = Math.Clamp(p.PageSize <= 0 ? 50 : p.PageSize, 1, MaxPageSize);

        var q = db.PunchItems
            .AsNoTracking()
            .Include(pi => pi.DesignProject)
            .Include(pi => pi.Assignee)
            .Include(pi => pi.VerifiedBy)
            .AsQueryable();

        if (p.DesignProjectId.HasValue) q = q.Where(pi => pi.DesignProjectId == p.DesignProjectId.Value);
        if (p.AssigneeUserId.HasValue) q = q.Where(pi => pi.AssigneeUserId == p.AssigneeUserId.Value);

        if (!string.IsNullOrWhiteSpace(p.Status))
        {
            var statuses = p.Status.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => Enum.TryParse<PunchStatus>(s, true, out var v) ? (PunchStatus?)v : null)
                .Where(v => v.HasValue).Select(v => v!.Value).ToList();
            if (statuses.Count > 0) q = q.Where(pi => statuses.Contains(pi.Status));
        }
        if (!string.IsNullOrWhiteSpace(p.Severity))
        {
            var severities = p.Severity.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => Enum.TryParse<PunchSeverity>(s, true, out var v) ? (PunchSeverity?)v : null)
                .Where(v => v.HasValue).Select(v => v!.Value).ToList();
            if (severities.Count > 0) q = q.Where(pi => severities.Contains(pi.Severity));
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (p.OpenOnly == true)
        {
            q = q.Where(pi => pi.Status != PunchStatus.Verified && pi.Status != PunchStatus.Cancelled);
        }
        if (p.OverdueOnly == true)
        {
            q = q.Where(pi => pi.Deadline != null && pi.Deadline < today
                           && pi.Status != PunchStatus.Verified
                           && pi.Status != PunchStatus.Cancelled);
        }

        if (!string.IsNullOrWhiteSpace(p.Search))
        {
            var term = p.Search.Trim();
            q = q.Where(pi => EF.Functions.Like(pi.Title, $"%{term}%")
                           || EF.Functions.Like(pi.PunchCode, $"%{term}%")
                           || (pi.Location != null && EF.Functions.Like(pi.Location, $"%{term}%"))
                           || (pi.Description != null && EF.Functions.Like(pi.Description, $"%{term}%")));
        }

        var total = await q.CountAsync(ct);
        var rows = await q
            // Critical + Overdue first — brings attention to what matters.
            .OrderByDescending(pi => pi.Severity)
            .ThenBy(pi => pi.Deadline ?? DateOnly.MaxValue)
            .ThenBy(pi => pi.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // Roll-up on the project-scoped set (drop status/severity/search
        // filters) so the header pills track the whole project workload.
        var scope = db.PunchItems.AsNoTracking();
        if (p.DesignProjectId.HasValue) scope = scope.Where(pi => pi.DesignProjectId == p.DesignProjectId.Value);
        if (p.AssigneeUserId.HasValue) scope = scope.Where(pi => pi.AssigneeUserId == p.AssigneeUserId.Value);
        var statusCounts = await scope
            .GroupBy(pi => pi.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status.ToString(), x => x.Count, ct);
        var overdueCount = await scope.CountAsync(pi => pi.Deadline != null && pi.Deadline < today
            && pi.Status != PunchStatus.Verified
            && pi.Status != PunchStatus.Cancelled, ct);

        return new PunchItemListResponse
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Items = rows.Select(r => Map(r, today)).ToList(),
            StatusCounts = statusCounts,
            OverdueCount = overdueCount,
        };
    }

    public async Task<PunchItemResponse?> GetAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.PunchItems
            .AsNoTracking()
            .Include(pi => pi.DesignProject)
            .Include(pi => pi.Assignee)
            .Include(pi => pi.VerifiedBy)
            .FirstOrDefaultAsync(pi => pi.Id == id, ct);
        return entity is null ? null : Map(entity, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    public async Task<PunchItemResponse> CreateAsync(CreatePunchItemRequest request, int callerUserId, CancellationToken ct = default)
    {
        var title = (request.Title ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new PunchItemOperationException("Tiêu đề là bắt buộc.");
        }
        if (!Enum.TryParse<PunchSeverity>(request.Severity, true, out var severity))
        {
            throw new PunchItemOperationException($"Mức độ '{request.Severity}' không hợp lệ.");
        }
        var project = await db.DesignProjects.FirstOrDefaultAsync(dp => dp.Id == request.DesignProjectId, ct);
        if (project is null)
        {
            throw new PunchItemOperationException($"Dự án #{request.DesignProjectId} không tồn tại.");
        }
        if (request.AssigneeUserId.HasValue &&
            !await db.Users.AnyAsync(u => u.Id == request.AssigneeUserId.Value, ct))
        {
            throw new PunchItemOperationException($"Người xử lý #{request.AssigneeUserId} không tồn tại.");
        }

        var code = (request.PunchCode ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(code))
        {
            code = await AllocatePunchCodeAsync(request.DesignProjectId, ct);
        }
        else if (await db.PunchItems.AnyAsync(pi => pi.DesignProjectId == request.DesignProjectId && pi.PunchCode == code, ct))
        {
            throw new PunchItemOperationException($"Mã lỗi '{code}' đã được dùng trong dự án này.");
        }

        var entity = new PunchItem
        {
            DesignProjectId = request.DesignProjectId,
            PunchCode = code,
            Title = title,
            Description = TrimOrNull(request.Description),
            Location = TrimOrNull(request.Location),
            Severity = severity,
            AssigneeUserId = request.AssigneeUserId,
            Deadline = request.Deadline,
            Note = TrimOrNull(request.Note),
            Status = PunchStatus.Open,
            CreatedByUserId = callerUserId,
            UpdatedByUserId = callerUserId,
        };
        db.PunchItems.Add(entity);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("PunchItem {Id} ({Code}) raised on project {ProjectId}",
            entity.Id, entity.PunchCode, entity.DesignProjectId);
        return (await GetAsync(entity.Id, ct))!;
    }

    public async Task<PunchItemResponse?> UpdateAsync(int id, UpdatePunchItemRequest request, int callerUserId, CancellationToken ct = default)
    {
        var entity = await db.PunchItems.FirstOrDefaultAsync(pi => pi.Id == id, ct);
        if (entity is null) return null;
        // Locked once Verified — the fix has been signed off, no more
        // metadata drift. Cancelled rows are also frozen.
        if (entity.Status == PunchStatus.Verified || entity.Status == PunchStatus.Cancelled)
        {
            throw new PunchItemOperationException(
                "Không chỉnh sửa được khi lỗi đã ở trạng thái Đã xác nhận hoặc Đã huỷ.");
        }

        var title = (request.Title ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new PunchItemOperationException("Tiêu đề là bắt buộc.");
        }
        if (!Enum.TryParse<PunchSeverity>(request.Severity, true, out var severity))
        {
            throw new PunchItemOperationException($"Mức độ '{request.Severity}' không hợp lệ.");
        }
        if (request.AssigneeUserId.HasValue &&
            !await db.Users.AnyAsync(u => u.Id == request.AssigneeUserId.Value, ct))
        {
            throw new PunchItemOperationException($"Người xử lý #{request.AssigneeUserId} không tồn tại.");
        }

        entity.Title = title;
        entity.Description = TrimOrNull(request.Description);
        entity.Location = TrimOrNull(request.Location);
        entity.Severity = severity;
        entity.AssigneeUserId = request.AssigneeUserId;
        entity.Deadline = request.Deadline;
        entity.ResolutionNote = TrimOrNull(request.ResolutionNote);
        entity.Note = TrimOrNull(request.Note);
        entity.UpdatedByUserId = callerUserId;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<PunchItemResponse?> TransitionStatusAsync(int id, TransitionPunchStatusRequest request, int callerUserId, CancellationToken ct = default)
    {
        if (!Enum.TryParse<PunchStatus>(request.Status, true, out var next))
        {
            throw new PunchItemOperationException($"Trạng thái '{request.Status}' không hợp lệ.");
        }
        var entity = await db.PunchItems.FirstOrDefaultAsync(pi => pi.Id == id, ct);
        if (entity is null) return null;

        EnsureTransitionAllowed(entity.Status, next);

        // Reopen bookkeeping — bumping the counter and clearing the
        // verification stamp so a subsequent verify records a fresh one.
        if (next == PunchStatus.Open && entity.Status != PunchStatus.Open)
        {
            entity.ReopenCount += 1;
            entity.VerifiedAt = null;
            entity.VerifiedByUserId = null;
        }
        if (next == PunchStatus.Verified)
        {
            entity.VerifiedAt = DateTime.UtcNow;
            entity.VerifiedByUserId = callerUserId;
        }

        if (!string.IsNullOrWhiteSpace(request.ResolutionNote))
        {
            entity.ResolutionNote = request.ResolutionNote.Trim();
        }
        entity.Status = next;
        entity.UpdatedByUserId = callerUserId;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("PunchItem {Id} transitioned to {Status} by user {UserId}",
            id, next, callerUserId);
        return await GetAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.PunchItems.FirstOrDefaultAsync(pi => pi.Id == id, ct);
        if (entity is null) return false;
        if (entity.Status != PunchStatus.Open)
        {
            throw new PunchItemOperationException(
                "Chỉ xoá được lỗi ở trạng thái Mở. Chuyển sang Đã huỷ để đóng dòng.");
        }
        db.PunchItems.Remove(entity);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("PunchItem {Id} deleted", id);
        return true;
    }

    public async Task<PunchItemBulkDeleteResponse> BulkDeleteAsync(IReadOnlyList<int> ids, CancellationToken ct = default)
    {
        if (ids is null || ids.Count == 0)
        {
            throw new PunchItemOperationException("Danh sách lỗi cần xoá là bắt buộc.");
        }
        if (ids.Count > MaxBulkDelete)
        {
            throw new PunchItemOperationException(
                $"Chỉ xoá tối đa {MaxBulkDelete} lỗi mỗi lần.");
        }
        var distinctIds = ids.Distinct().ToList();
        var rows = await db.PunchItems.Where(pi => distinctIds.Contains(pi.Id)).ToListAsync(ct);
        var response = new PunchItemBulkDeleteResponse { Requested = distinctIds.Count };
        var found = rows.Select(r => r.Id).ToHashSet();
        foreach (var missing in distinctIds.Where(id => !found.Contains(id)))
        {
            response.Failures.Add(new PunchItemBulkDeleteFailure
            {
                Id = missing,
                Message = $"Lỗi #{missing} không tồn tại.",
            });
        }
        var toDelete = new List<PunchItem>();
        foreach (var row in rows)
        {
            if (row.Status != PunchStatus.Open)
            {
                response.Failures.Add(new PunchItemBulkDeleteFailure
                {
                    Id = row.Id,
                    Message = "Chỉ xoá được lỗi ở trạng thái Mở.",
                });
                continue;
            }
            toDelete.Add(row);
        }
        if (toDelete.Count > 0)
        {
            db.PunchItems.RemoveRange(toDelete);
            await db.SaveChangesAsync(ct);
            response.Deleted = toDelete.Count;
        }
        return response;
    }

    // ------------------------------ Helpers ---------------------------------

    private async Task<string> AllocatePunchCodeAsync(int projectId, CancellationToken ct)
    {
        var used = await db.PunchItems
            .Where(pi => pi.DesignProjectId == projectId)
            .Select(pi => pi.PunchCode)
            .ToListAsync(ct);
        var maxSeq = used
            .Select(c =>
            {
                var idx = c.LastIndexOf('-');
                if (idx < 0 || idx == c.Length - 1) return 0;
                return int.TryParse(c[(idx + 1)..], out var n) ? n : 0;
            })
            .DefaultIfEmpty(0).Max();
        return $"P-{maxSeq + 1:D3}";
    }

    /// <summary>
    /// Slice-1 state machine for a punch item:
    /// <list type="bullet">
    ///   <item>Open → InProgress | Cancelled</item>
    ///   <item>InProgress → Fixed | Open (reopen while still triaging) | Cancelled</item>
    ///   <item>Fixed → Verified (accept) | Open (rejection — fix wasn't right) | Cancelled</item>
    ///   <item>Verified → Open (post-acceptance defect appears)</item>
    ///   <item>Cancelled is terminal — reopen requires creating a new item</item>
    /// </list>
    /// </summary>
    private static void EnsureTransitionAllowed(PunchStatus from, PunchStatus to)
    {
        if (from == to) return; // idempotent
        bool ok = (from, to) switch
        {
            (PunchStatus.Open, PunchStatus.InProgress) => true,
            (PunchStatus.Open, PunchStatus.Cancelled) => true,

            (PunchStatus.InProgress, PunchStatus.Fixed) => true,
            (PunchStatus.InProgress, PunchStatus.Open) => true,
            (PunchStatus.InProgress, PunchStatus.Cancelled) => true,

            (PunchStatus.Fixed, PunchStatus.Verified) => true,
            (PunchStatus.Fixed, PunchStatus.Open) => true,
            (PunchStatus.Fixed, PunchStatus.Cancelled) => true,

            (PunchStatus.Verified, PunchStatus.Open) => true,

            _ => false,
        };
        if (!ok)
        {
            throw new PunchItemOperationException($"Không thể chuyển từ '{from}' sang '{to}'.");
        }
    }

    private static string? TrimOrNull(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static PunchItemResponse Map(PunchItem pi, DateOnly today) => new()
    {
        Id = pi.Id,
        DesignProjectId = pi.DesignProjectId,
        DesignProjectCode = pi.DesignProject?.ProjectCode,
        DesignProjectName = pi.DesignProject?.Name,
        PunchCode = pi.PunchCode,
        Title = pi.Title,
        Description = pi.Description,
        Location = pi.Location,
        Severity = pi.Severity.ToString(),
        Status = pi.Status.ToString(),
        AssigneeUserId = pi.AssigneeUserId,
        AssigneeName = pi.Assignee?.FullName,
        Deadline = pi.Deadline,
        ResolutionNote = pi.ResolutionNote,
        ReopenCount = pi.ReopenCount,
        VerifiedAt = pi.VerifiedAt,
        VerifiedByUserId = pi.VerifiedByUserId,
        VerifiedByName = pi.VerifiedBy?.FullName,
        Note = pi.Note,
        IsOverdue = pi.Deadline.HasValue && pi.Deadline < today
                    && pi.Status != PunchStatus.Verified
                    && pi.Status != PunchStatus.Cancelled,
        CreatedAt = pi.CreatedAt,
        UpdatedAt = pi.UpdatedAt,
    };
}
