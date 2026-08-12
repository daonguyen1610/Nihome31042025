using System.Text.Json;
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
    private const int MaxDocuments = 20;

    // --------------------------------------------------------------------
    //  Read paths
    // --------------------------------------------------------------------

    public async Task<AcceptanceRecordListResponse> ListAsync(
        AcceptanceRecordListParams p, int callerUserId, bool canSeeAll, CancellationToken ct = default)
    {
        var page = p.Page < 1 ? 1 : p.Page;
        var pageSize = Math.Clamp(p.PageSize <= 0 ? 20 : p.PageSize, 1, MaxPageSize);

        var q = ApplyScope(db.AcceptanceRecords.AsNoTracking(), callerUserId, canSeeAll)
            .Include(a => a.DesignProject)
            .Include(a => a.ConstructionTask)
            .Include(a => a.SubmittedBy)
            .Include(a => a.ApprovedBy)
            .Include(a => a.RejectedBy)
            .AsQueryable();

        if (p.DesignProjectId.HasValue) q = q.Where(a => a.DesignProjectId == p.DesignProjectId.Value);
        if (p.ConstructionTaskId.HasValue) q = q.Where(a => a.ConstructionTaskId == p.ConstructionTaskId.Value);
        if (p.ResponsibleUserId.HasValue)
        {
            var responsibleUserId = p.ResponsibleUserId.Value;
            q = q.Where(a => a.CreatedByUserId == responsibleUserId
                          || a.DesignProject.ProjectManagerUserId == responsibleUserId
                          || a.DesignProject.DesignLeadUserId == responsibleUserId
                          || (a.ConstructionTask != null && a.ConstructionTask.OwnerUserId == responsibleUserId));
        }
        if (p.AcceptanceFrom.HasValue) q = q.Where(a => a.AcceptanceDate >= p.AcceptanceFrom.Value);
        if (p.AcceptanceTo.HasValue) q = q.Where(a => a.AcceptanceDate <= p.AcceptanceTo.Value);

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

        var ordered = ApplySort(q, p.SortBy, p.SortDirection);
        var rows = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // Per-status + overdue roll-up in the same project scope so the
        // stat tiles stay aligned with the current filter.
        var scope = ApplyScope(db.AcceptanceRecords.AsNoTracking(), callerUserId, canSeeAll);
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

    public async Task<AcceptanceRecordResponse?> GetAsync(
        int id, int callerUserId, bool canSeeAll, CancellationToken ct = default)
    {
        var entity = await ApplyScope(db.AcceptanceRecords.AsNoTracking(), callerUserId, canSeeAll)
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

    public async Task<AcceptanceRecordResponse> CreateAsync(
        CreateAcceptanceRecordRequest request, int callerUserId, bool canSeeAll = true, CancellationToken ct = default)
    {
        ValidateRequest(request.Title, request.AcceptanceDate, request.Description, request.Location,
            request.Participants, request.Findings, request.Documents);
        var title = request.Title.Trim();

        var project = await db.DesignProjects.FirstOrDefaultAsync(dp => dp.Id == request.DesignProjectId, ct);
        if (project is null)
        {
            throw new AcceptanceRecordOperationException($"Dự án #{request.DesignProjectId} không tồn tại.");
        }
        int? taskOwnerUserId = null;
        if (request.ConstructionTaskId.HasValue)
        {
            var task = await db.ConstructionTasks
                .Where(t => t.Id == request.ConstructionTaskId.Value)
                .Select(t => new { t.DesignProjectId, t.OwnerUserId })
                .FirstOrDefaultAsync(ct);
            if (task is null)
            {
                throw new AcceptanceRecordOperationException($"Hạng mục #{request.ConstructionTaskId} không tồn tại.");
            }
            if (task.DesignProjectId != request.DesignProjectId)
            {
                throw new AcceptanceRecordOperationException("Hạng mục không thuộc dự án đã chọn.");
            }
            taskOwnerUserId = task.OwnerUserId;
        }
        if (!canSeeAll && project.ProjectManagerUserId != callerUserId
            && project.DesignLeadUserId != callerUserId && taskOwnerUserId != callerUserId)
        {
            throw new AcceptanceRecordOperationException("Bạn không thuộc phạm vi dự án hoặc hạng mục đã chọn.");
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
        return (await GetAsync(entity.Id, callerUserId, true, ct))!;
    }

    public async Task<AcceptanceRecordResponse?> UpdateAsync(
        int id, UpdateAcceptanceRecordRequest request, int callerUserId, bool canSeeAll = true, CancellationToken ct = default)
    {
        var entity = await ApplyScope(db.AcceptanceRecords, callerUserId, canSeeAll)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
        if (entity is null) return null;

        if (entity.Status is AcceptanceStatus.Approved or AcceptanceStatus.Cancelled)
        {
            throw new AcceptanceRecordOperationException(
                $"Không thể chỉnh sửa biên bản đã ở trạng thái '{entity.Status}'.");
        }

        ValidateRequest(request.Title, request.AcceptanceDate, request.Description, request.Location,
            request.Participants, request.Findings, request.Documents);
        var title = request.Title.Trim();

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
        if (request.Documents is not null) entity.Documents = TrimOrNull(request.Documents);
        entity.UpdatedByUserId = callerUserId;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, callerUserId, true, ct);
    }

    public async Task<AcceptanceRecordResponse?> TransitionAsync(
        int id, TransitionAcceptanceStatusRequest request, int callerUserId, bool canSeeAll = true, CancellationToken ct = default)
    {
        var entity = await ApplyScope(db.AcceptanceRecords, callerUserId, canSeeAll)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
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

        var previous = entity.Status;
        ApplyTransition(entity, next, callerUserId, request.ResolutionNote);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("AcceptanceRecord {Id} transitioned {From} -> {To}",
            id, previous, next);
        return await GetAsync(id, callerUserId, true, ct);
    }

    public async Task<AcceptanceRecordResponse?> ApproveAsync(
        int id, TransitionAcceptanceStatusRequest request, int callerUserId, bool canSeeAll = true, CancellationToken ct = default)
    {
        var entity = await ApplyScope(db.AcceptanceRecords, callerUserId, canSeeAll)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
        if (entity is null) return null;

        EnsureTransitionAllowed(entity.Status, AcceptanceStatus.Approved);
        ApplyTransition(entity, AcceptanceStatus.Approved, callerUserId, request.ResolutionNote);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("AcceptanceRecord {Id} approved by user {UserId}", id, callerUserId);
        return await GetAsync(id, callerUserId, true, ct);
    }

    public async Task<bool> DeleteAsync(int id, int callerUserId = 0, bool canSeeAll = true, CancellationToken ct = default)
    {
        var entity = await ApplyScope(db.AcceptanceRecords, callerUserId, canSeeAll)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
        if (entity is null) return false;
        db.AcceptanceRecords.Remove(entity);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<AcceptanceRecordBulkDeleteResponse> BulkDeleteAsync(
        BulkDeleteAcceptanceRecordsRequest request, int callerUserId = 0, bool canSeeAll = true, CancellationToken ct = default)
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

        var rows = await ApplyScope(db.AcceptanceRecords, callerUserId, canSeeAll)
            .Where(a => ids.Contains(a.Id)).ToListAsync(ct);
        var response = new AcceptanceRecordBulkDeleteResponse();
        foreach (var row in rows)
        {
            response.DeletedIds.Add(row.Id);
            db.AcceptanceRecords.Remove(row);
        }
        response.SkippedIds.AddRange(ids.Except(rows.Select(r => r.Id)));
        if (response.DeletedIds.Count > 0) await db.SaveChangesAsync(ct);
        return response;
    }

    // --------------------------------------------------------------------
    //  Helpers
    // --------------------------------------------------------------------

    private static IQueryable<AcceptanceRecord> ApplyScope(
        IQueryable<AcceptanceRecord> query, int callerUserId, bool canSeeAll)
    {
        if (canSeeAll) return query;
        return query.Where(a => a.CreatedByUserId == callerUserId
            || a.SubmittedByUserId == callerUserId
            || a.ApprovedByUserId == callerUserId
            || a.RejectedByUserId == callerUserId
            || a.DesignProject.ProjectManagerUserId == callerUserId
            || a.DesignProject.DesignLeadUserId == callerUserId
            || (a.ConstructionTask != null && a.ConstructionTask.OwnerUserId == callerUserId));
    }

    private static IOrderedQueryable<AcceptanceRecord> ApplySort(
        IQueryable<AcceptanceRecord> query, string? sortBy, string? sortDirection)
    {
        var descending = !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        return (sortBy?.Trim().ToLowerInvariant(), descending) switch
        {
            ("code", false) => query.OrderBy(a => a.AcceptanceCode),
            ("code", true) => query.OrderByDescending(a => a.AcceptanceCode),
            ("title", false) => query.OrderBy(a => a.Title),
            ("title", true) => query.OrderByDescending(a => a.Title),
            ("project", false) => query.OrderBy(a => a.DesignProject.Name),
            ("project", true) => query.OrderByDescending(a => a.DesignProject.Name),
            ("status", false) => query.OrderBy(a => a.Status),
            ("status", true) => query.OrderByDescending(a => a.Status),
            ("updatedat", false) => query.OrderBy(a => a.UpdatedAt),
            ("updatedat", true) => query.OrderByDescending(a => a.UpdatedAt),
            (_, false) => query.OrderBy(a => a.AcceptanceDate).ThenBy(a => a.AcceptanceCode),
            _ => query.OrderByDescending(a => a.AcceptanceDate).ThenBy(a => a.AcceptanceCode),
        };
    }

    private static void ValidateRequest(
        string? title, DateOnly acceptanceDate, string? description, string? location,
        string? participants, string? findings, string? documents)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new AcceptanceRecordOperationException("Tiêu đề biên bản nghiệm thu là bắt buộc.");
        if (title.Trim().Length > 300)
            throw new AcceptanceRecordOperationException("Tiêu đề không được vượt quá 300 ký tự.");
        if (acceptanceDate == default)
            throw new AcceptanceRecordOperationException("Ngày nghiệm thu là bắt buộc.");
        EnsureMaxLength(description, 4000, "Mô tả");
        EnsureMaxLength(location, 200, "Địa điểm");
        EnsureMaxLength(participants, 1000, "Thành phần tham gia");
        EnsureMaxLength(findings, 4000, "Ghi nhận");
        ValidateDocuments(documents);
    }

    private static void EnsureMaxLength(string? value, int maxLength, string field)
    {
        if (value?.Trim().Length > maxLength)
            throw new AcceptanceRecordOperationException($"{field} không được vượt quá {maxLength} ký tự.");
    }

    private static void ValidateDocuments(string? documents)
    {
        if (string.IsNullOrWhiteSpace(documents)) return;
        if (documents.Length > 4000)
            throw new AcceptanceRecordOperationException("Danh sách tài liệu không được vượt quá 4000 ký tự.");
        try
        {
            var paths = JsonSerializer.Deserialize<List<string>>(documents);
            if (paths is null || paths.Count > MaxDocuments || paths.Any(string.IsNullOrWhiteSpace))
                throw new JsonException();
            if (paths.Any(path => path.Trim().Length > 500))
                throw new AcceptanceRecordOperationException("Mỗi đường dẫn tài liệu không được vượt quá 500 ký tự.");
            if (paths.Any(path => !IsSafeDocumentUrl(path)))
                throw new AcceptanceRecordOperationException(
                    "Đường dẫn tài liệu phải bắt đầu bằng / hoặc là URL HTTP(S) tuyệt đối.");
        }
        catch (AcceptanceRecordOperationException)
        {
            throw;
        }
        catch (JsonException)
        {
            throw new AcceptanceRecordOperationException(
                $"Tài liệu phải là mảng JSON gồm tối đa {MaxDocuments} đường dẫn hợp lệ.");
        }
    }

    private static bool IsSafeDocumentUrl(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith("/", StringComparison.Ordinal))
            return !trimmed.StartsWith("//", StringComparison.Ordinal)
                && !trimmed.StartsWith("/\\", StringComparison.Ordinal);

        return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && !string.IsNullOrWhiteSpace(uri.Host);
    }

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
