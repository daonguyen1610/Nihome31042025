using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>
/// Implementation of the M4 partial acceptance workflow (NIH-143).
/// Owns state machine, code allocation and roll-ups; approval is
/// permission-guarded at the controller so the service exposes two
/// entry points (<see cref="TransitionAsync"/> for non-approval
/// transitions and <see cref="ApproveAsync"/> for the approve gate).
/// </summary>
public class AcceptanceRecordService(
    AppDbContext db,
    ILogger<AcceptanceRecordService> logger) : IAcceptanceRecordService
{
    private const int MaxPageSize = 200;
    private const int MaxBulkDelete = 100;

    // --------------------------------------------------------------------
    //  Read paths
    // --------------------------------------------------------------------

    public async Task<AcceptanceRecordListResponse> ListAsync(AcceptanceRecordListParams p, CancellationToken ct = default)
    {
        var page = p.Page < 1 ? 1 : p.Page;
        var pageSize = Math.Clamp(p.PageSize <= 0 ? 20 : p.PageSize, 1, MaxPageSize);

        var q = db.AcceptanceRecords
            .AsNoTracking()
            .Include(a => a.DesignProject)
            .Include(a => a.ConstructionTask)
            .Include(a => a.SubmittedBy)
            .Include(a => a.ApprovedBy)
            .Include(a => a.RejectedBy)
            .AsQueryable();

        if (p.DesignProjectId.HasValue) q = q.Where(a => a.DesignProjectId == p.DesignProjectId.Value);
        if (p.ConstructionTaskId.HasValue) q = q.Where(a => a.ConstructionTaskId == p.ConstructionTaskId.Value);

        if (!string.IsNullOrWhiteSpace(p.Status))
        {
            var statuses = p.Status.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => Enum.TryParse<AcceptanceStatus>(s, true, out var v) ? (AcceptanceStatus?)v : null)
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();
            if (statuses.Count > 0) q = q.Where(a => statuses.Contains(a.Status));
        }

        if (!string.IsNullOrWhiteSpace(p.Search))
        {
            var term = p.Search.Trim();
            q = q.Where(a => EF.Functions.Like(a.Title, $"%{term}%")
                          || EF.Functions.Like(a.AcceptanceCode, $"%{term}%")
                          || (a.Location != null && EF.Functions.Like(a.Location, $"%{term}%")));
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (p.OpenOnly)
        {
            q = q.Where(a => a.Status == AcceptanceStatus.Draft
                          || a.Status == AcceptanceStatus.Submitted
                          || a.Status == AcceptanceStatus.Rejected);
        }
        if (p.OverdueOnly)
        {
            q = q.Where(a => a.AcceptanceDate < today
                          && (a.Status == AcceptanceStatus.Draft || a.Status == AcceptanceStatus.Submitted));
        }

        var total = await q.CountAsync(ct);

        var rows = await q
            .OrderByDescending(a => a.AcceptanceDate)
            .ThenBy(a => a.AcceptanceCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // Per-status + overdue roll-up in the same project scope so the
        // stat tiles stay aligned with the current filter.
        var scope = db.AcceptanceRecords.AsNoTracking();
        if (p.DesignProjectId.HasValue) scope = scope.Where(a => a.DesignProjectId == p.DesignProjectId.Value);
        if (p.ConstructionTaskId.HasValue) scope = scope.Where(a => a.ConstructionTaskId == p.ConstructionTaskId.Value);

        var statusCounts = await scope
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status.ToString(), x => x.Count, ct);

        var overdueCount = await scope.CountAsync(a => a.AcceptanceDate < today
            && (a.Status == AcceptanceStatus.Draft || a.Status == AcceptanceStatus.Submitted), ct);

        return new AcceptanceRecordListResponse
        {
            Items = rows.Select(r => Map(r, today)).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize,
            StatusCounts = statusCounts,
            OverdueCount = overdueCount,
        };
    }

    public async Task<AcceptanceRecordResponse?> GetAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.AcceptanceRecords
            .AsNoTracking()
            .Include(a => a.DesignProject)
            .Include(a => a.ConstructionTask)
            .Include(a => a.SubmittedBy)
            .Include(a => a.ApprovedBy)
            .Include(a => a.RejectedBy)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
        return entity is null ? null : Map(entity, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    // --------------------------------------------------------------------
    //  Write paths
    // --------------------------------------------------------------------

    public async Task<AcceptanceRecordResponse> CreateAsync(CreateAcceptanceRecordRequest request, int callerUserId, CancellationToken ct = default)
    {
        var title = (request.Title ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(title))
        {
            throw new AcceptanceRecordOperationException("Tiêu đề biên bản nghiệm thu là bắt buộc.");
        }

        var project = await db.DesignProjects.FirstOrDefaultAsync(dp => dp.Id == request.DesignProjectId, ct);
        if (project is null)
        {
            throw new AcceptanceRecordOperationException($"Dự án #{request.DesignProjectId} không tồn tại.");
        }

        if (request.ConstructionTaskId.HasValue)
        {
            var taskProject = await db.ConstructionTasks
                .Where(t => t.Id == request.ConstructionTaskId.Value)
                .Select(t => (int?)t.DesignProjectId)
                .FirstOrDefaultAsync(ct);
            if (taskProject is null)
            {
                throw new AcceptanceRecordOperationException($"Hạng mục #{request.ConstructionTaskId} không tồn tại.");
            }
            if (taskProject.Value != request.DesignProjectId)
            {
                throw new AcceptanceRecordOperationException("Hạng mục không thuộc dự án đã chọn.");
            }
        }

        var code = await AllocateCodeAsync(request.DesignProjectId, ct);

        var entity = new AcceptanceRecord
        {
            DesignProjectId = request.DesignProjectId,
            AcceptanceCode = code,
            Title = title,
            Description = TrimOrNull(request.Description),
            ConstructionTaskId = request.ConstructionTaskId,
            AcceptanceDate = request.AcceptanceDate,
            Location = TrimOrNull(request.Location),
            Participants = TrimOrNull(request.Participants),
            Findings = TrimOrNull(request.Findings),
            Documents = TrimOrNull(request.Documents),
            Status = AcceptanceStatus.Draft,
            CreatedByUserId = callerUserId,
            UpdatedByUserId = callerUserId,
        };
        db.AcceptanceRecords.Add(entity);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("AcceptanceRecord {Id} ({Code}) created on project {ProjectId}",
            entity.Id, entity.AcceptanceCode, entity.DesignProjectId);
        return (await GetAsync(entity.Id, ct))!;
    }

    public async Task<AcceptanceRecordResponse?> UpdateAsync(int id, UpdateAcceptanceRecordRequest request, int callerUserId, CancellationToken ct = default)
    {
        var entity = await db.AcceptanceRecords.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (entity is null) return null;

        if (entity.Status is AcceptanceStatus.Approved or AcceptanceStatus.Cancelled)
        {
            throw new AcceptanceRecordOperationException(
                $"Không thể chỉnh sửa biên bản đã ở trạng thái '{entity.Status}'.");
        }

        var title = (request.Title ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(title))
        {
            throw new AcceptanceRecordOperationException("Tiêu đề biên bản nghiệm thu là bắt buộc.");
        }

        if (request.ConstructionTaskId.HasValue)
        {
            var taskProject = await db.ConstructionTasks
                .Where(t => t.Id == request.ConstructionTaskId.Value)
                .Select(t => (int?)t.DesignProjectId)
                .FirstOrDefaultAsync(ct);
            if (taskProject is null)
            {
                throw new AcceptanceRecordOperationException($"Hạng mục #{request.ConstructionTaskId} không tồn tại.");
            }
            if (taskProject.Value != entity.DesignProjectId)
            {
                throw new AcceptanceRecordOperationException("Hạng mục không thuộc dự án của biên bản.");
            }
        }

        entity.Title = title;
        entity.Description = TrimOrNull(request.Description);
        entity.ConstructionTaskId = request.ConstructionTaskId;
        entity.AcceptanceDate = request.AcceptanceDate;
        entity.Location = TrimOrNull(request.Location);
        entity.Participants = TrimOrNull(request.Participants);
        entity.Findings = TrimOrNull(request.Findings);
        entity.Documents = TrimOrNull(request.Documents);
        entity.UpdatedByUserId = callerUserId;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<AcceptanceRecordResponse?> TransitionAsync(int id, TransitionAcceptanceStatusRequest request, int callerUserId, CancellationToken ct = default)
    {
        var entity = await db.AcceptanceRecords.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (entity is null) return null;

        if (!Enum.TryParse<AcceptanceStatus>(request.Status, true, out var next))
        {
            throw new AcceptanceRecordOperationException($"Trạng thái '{request.Status}' không hợp lệ.");
        }
        if (next == AcceptanceStatus.Approved)
        {
            throw new AcceptanceRecordOperationException(
                "Sử dụng endpoint /approve — thao tác duyệt cần quyền construction.acceptance.approve.");
        }

        EnsureTransitionAllowed(entity.Status, next);

        ApplyTransition(entity, next, callerUserId, request.ResolutionNote);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("AcceptanceRecord {Id} transitioned {From} -> {To}",
            id, entity.Status, next);
        return await GetAsync(id, ct);
    }

    public async Task<AcceptanceRecordResponse?> ApproveAsync(int id, TransitionAcceptanceStatusRequest request, int callerUserId, CancellationToken ct = default)
    {
        var entity = await db.AcceptanceRecords.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (entity is null) return null;

        EnsureTransitionAllowed(entity.Status, AcceptanceStatus.Approved);
        ApplyTransition(entity, AcceptanceStatus.Approved, callerUserId, request.ResolutionNote);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("AcceptanceRecord {Id} approved by user {UserId}", id, callerUserId);
        return await GetAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.AcceptanceRecords.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (entity is null) return false;
        if (entity.Status == AcceptanceStatus.Approved)
        {
            throw new AcceptanceRecordOperationException(
                "Không thể xoá biên bản đã được duyệt. Hãy huỷ (Cancel) trước khi xoá.");
        }
        db.AcceptanceRecords.Remove(entity);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<AcceptanceRecordBulkDeleteResponse> BulkDeleteAsync(BulkDeleteAcceptanceRecordsRequest request, CancellationToken ct = default)
    {
        var ids = (request.Ids ?? new List<int>()).Distinct().ToList();
        if (ids.Count == 0)
        {
            throw new AcceptanceRecordOperationException("Danh sách biên bản cần xoá là bắt buộc.");
        }
        if (ids.Count > MaxBulkDelete)
        {
            throw new AcceptanceRecordOperationException(
                $"Chỉ xoá tối đa {MaxBulkDelete} biên bản mỗi lần.");
        }

        var rows = await db.AcceptanceRecords.Where(a => ids.Contains(a.Id)).ToListAsync(ct);
        var response = new AcceptanceRecordBulkDeleteResponse();
        foreach (var row in rows)
        {
            if (row.Status == AcceptanceStatus.Approved)
            {
                response.SkippedIds.Add(row.Id);
            }
            else
            {
                response.DeletedIds.Add(row.Id);
                db.AcceptanceRecords.Remove(row);
            }
        }
        // Missing ids also skipped (surfaced to the caller so their toast
        // can distinguish 'blocked' vs 'gone').
        response.SkippedIds.AddRange(ids.Except(rows.Select(r => r.Id)));
        if (response.DeletedIds.Count > 0) await db.SaveChangesAsync(ct);
        return response;
    }

    // --------------------------------------------------------------------
    //  Helpers
    // --------------------------------------------------------------------

    private async Task<string> AllocateCodeAsync(int projectId, CancellationToken ct)
    {
        var codes = await db.AcceptanceRecords
            .Where(a => a.DesignProjectId == projectId)
            .Select(a => a.AcceptanceCode)
            .ToListAsync(ct);
        var maxSeq = codes
            .Select(c =>
            {
                var idx = c.LastIndexOf('-');
                if (idx < 0 || idx == c.Length - 1) return 0;
                return int.TryParse(c[(idx + 1)..], out var n) ? n : 0;
            })
            .DefaultIfEmpty(0)
            .Max();
        return $"A-{maxSeq + 1:D3}";
    }

    /// <summary>
    /// State machine table. Anything not listed here is rejected as an
    /// invalid transition.
    ///
    ///   Draft      -> Submitted, Cancelled
    ///   Submitted  -> Approved, Rejected, Cancelled, Draft (recall)
    ///   Rejected   -> Draft (revision), Cancelled
    ///   Approved   -> Cancelled  (only path out — reopens as a new record)
    ///   Cancelled  -> (terminal)
    /// </summary>
    private static void EnsureTransitionAllowed(AcceptanceStatus from, AcceptanceStatus to)
    {
        if (from == to)
        {
            throw new AcceptanceRecordOperationException(
                $"Trạng thái đã là '{from}'.");
        }

        var allowed = from switch
        {
            AcceptanceStatus.Draft => to is AcceptanceStatus.Submitted or AcceptanceStatus.Cancelled,
            AcceptanceStatus.Submitted => to is AcceptanceStatus.Approved or AcceptanceStatus.Rejected
                                              or AcceptanceStatus.Cancelled or AcceptanceStatus.Draft,
            AcceptanceStatus.Rejected => to is AcceptanceStatus.Draft or AcceptanceStatus.Cancelled,
            AcceptanceStatus.Approved => to is AcceptanceStatus.Cancelled,
            AcceptanceStatus.Cancelled => false,
            _ => false,
        };
        if (!allowed)
        {
            throw new AcceptanceRecordOperationException(
                $"Không thể chuyển '{from}' sang '{to}'.");
        }
    }

    private static void ApplyTransition(AcceptanceRecord entity, AcceptanceStatus next, int userId, string? note)
    {
        var now = DateTime.UtcNow;

        // Reset per-transition metadata so a re-submit doesn't carry over
        // the old approve/reject signatures.
        switch (next)
        {
            case AcceptanceStatus.Submitted:
                entity.SubmittedAt = now;
                entity.SubmittedByUserId = userId;
                entity.RejectedAt = null;
                entity.RejectedByUserId = null;
                break;
            case AcceptanceStatus.Approved:
                entity.ApprovedAt = now;
                entity.ApprovedByUserId = userId;
                break;
            case AcceptanceStatus.Rejected:
                entity.RejectedAt = now;
                entity.RejectedByUserId = userId;
                break;
            case AcceptanceStatus.Draft when entity.Status == AcceptanceStatus.Rejected:
                // Rejected -> Draft is the 'revise' branch.
                entity.RevisionCount += 1;
                break;
        }

        if (!string.IsNullOrWhiteSpace(note))
        {
            entity.ResolutionNote = note.Trim();
        }
        entity.Status = next;
        entity.UpdatedByUserId = userId;
        entity.UpdatedAt = now;
    }

    private static AcceptanceRecordResponse Map(AcceptanceRecord entity, DateOnly today)
    {
        var isOverdue = entity.AcceptanceDate < today
            && (entity.Status == AcceptanceStatus.Draft || entity.Status == AcceptanceStatus.Submitted);
        return new AcceptanceRecordResponse
        {
            Id = entity.Id,
            DesignProjectId = entity.DesignProjectId,
            DesignProjectName = entity.DesignProject?.Name ?? string.Empty,
            AcceptanceCode = entity.AcceptanceCode,
            Title = entity.Title,
            Description = entity.Description,
            ConstructionTaskId = entity.ConstructionTaskId,
            ConstructionTaskName = entity.ConstructionTask?.Name,
            AcceptanceDate = entity.AcceptanceDate,
            Location = entity.Location,
            Participants = entity.Participants,
            Findings = entity.Findings,
            ResolutionNote = entity.ResolutionNote,
            Documents = entity.Documents,
            Status = entity.Status.ToString(),
            IsOverdue = isOverdue,
            RevisionCount = entity.RevisionCount,
            SubmittedAt = entity.SubmittedAt,
            SubmittedByUserId = entity.SubmittedByUserId,
            SubmittedByName = entity.SubmittedBy?.FullName,
            ApprovedAt = entity.ApprovedAt,
            ApprovedByUserId = entity.ApprovedByUserId,
            ApprovedByName = entity.ApprovedBy?.FullName,
            RejectedAt = entity.RejectedAt,
            RejectedByUserId = entity.RejectedByUserId,
            RejectedByName = entity.RejectedBy?.FullName,
            CreatedAt = entity.CreatedAt,
            CreatedByUserId = entity.CreatedByUserId,
            UpdatedAt = entity.UpdatedAt,
            UpdatedByUserId = entity.UpdatedByUserId,
        };
    }

    private static string? TrimOrNull(string? value)
    {
        if (value is null) return null;
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
