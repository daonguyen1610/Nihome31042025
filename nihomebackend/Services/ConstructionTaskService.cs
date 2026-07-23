using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>
/// Slice-1 implementation of <see cref="IConstructionTaskService"/> for
/// the M4 Gantt (NIH-141) feature. Handles validation, cycle detection
/// in the predecessor graph, planned-vs-actual date rules and the
/// overdue roll-up used by the Gantt list header.
/// </summary>
public class ConstructionTaskService(
    AppDbContext db,
    ILogger<ConstructionTaskService> logger) : IConstructionTaskService
{
    private const int MaxPageSize = 200;
    private const int MaxBulkDelete = 100;

    public async Task<ConstructionTaskListResponse> ListAsync(ConstructionTaskListParams p, CancellationToken ct = default)
    {
        var page = p.Page < 1 ? 1 : p.Page;
        var pageSize = Math.Clamp(p.PageSize <= 0 ? 50 : p.PageSize, 1, MaxPageSize);

        var q = db.ConstructionTasks
            .AsNoTracking()
            .Include(t => t.DesignProject)
            .Include(t => t.Owner)
            .Include(t => t.Predecessors)
                .ThenInclude(pd => pd.PredecessorTask)
            .AsQueryable();

        if (p.DesignProjectId.HasValue) q = q.Where(t => t.DesignProjectId == p.DesignProjectId.Value);
        if (p.OwnerUserId.HasValue) q = q.Where(t => t.OwnerUserId == p.OwnerUserId.Value);
        if (!string.IsNullOrWhiteSpace(p.Status))
        {
            var statuses = p.Status.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => Enum.TryParse<ConstructionTaskStatus>(s, true, out var v) ? (ConstructionTaskStatus?)v : null)
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();
            if (statuses.Count > 0) q = q.Where(t => statuses.Contains(t.Status));
        }
        if (!string.IsNullOrWhiteSpace(p.Search))
        {
            var term = p.Search.Trim();
            q = q.Where(t => EF.Functions.Like(t.Name, $"%{term}%")
                          || EF.Functions.Like(t.TaskCode, $"%{term}%")
                          || (t.Wbs != null && EF.Functions.Like(t.Wbs, $"%{term}%")));
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (p.OverdueOnly == true)
        {
            q = q.Where(t => t.PlannedEnd < today
                          && t.Status != ConstructionTaskStatus.Completed
                          && t.Status != ConstructionTaskStatus.Cancelled);
        }

        var total = await q.CountAsync(ct);
        var rows = await q
            .OrderBy(t => t.PlannedStart)
            .ThenBy(t => t.TaskCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // Per-status + overdue roll-up on the same filter scope (minus the
        // OverdueOnly flag) so the header pills line up with the visible
        // filter set even when pagination is in play.
        var scope = db.ConstructionTasks.AsNoTracking();
        if (p.DesignProjectId.HasValue) scope = scope.Where(t => t.DesignProjectId == p.DesignProjectId.Value);
        if (p.OwnerUserId.HasValue) scope = scope.Where(t => t.OwnerUserId == p.OwnerUserId.Value);
        var statusCounts = await scope
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status.ToString(), x => x.Count, ct);
        var overdueCount = await scope.CountAsync(t => t.PlannedEnd < today
            && t.Status != ConstructionTaskStatus.Completed
            && t.Status != ConstructionTaskStatus.Cancelled, ct);

        return new ConstructionTaskListResponse
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Items = rows.Select(r => Map(r, today)).ToList(),
            StatusCounts = statusCounts,
            OverdueCount = overdueCount,
        };
    }

    public async Task<ConstructionTaskResponse?> GetAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.ConstructionTasks
            .AsNoTracking()
            .Include(t => t.DesignProject)
            .Include(t => t.Owner)
            .Include(t => t.Predecessors)
                .ThenInclude(pd => pd.PredecessorTask)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
        return entity is null ? null : Map(entity, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    public async Task<ConstructionTaskResponse> CreateAsync(CreateConstructionTaskRequest request, int callerUserId, CancellationToken ct = default)
    {
        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ConstructionTaskOperationException("Tên công việc là bắt buộc.");
        }
        if (request.PlannedEnd < request.PlannedStart)
        {
            throw new ConstructionTaskOperationException("Ngày kết thúc dự kiến phải sau hoặc bằng ngày bắt đầu.");
        }

        var project = await db.DesignProjects.FirstOrDefaultAsync(dp => dp.Id == request.DesignProjectId, ct);
        if (project is null)
        {
            throw new ConstructionTaskOperationException($"Dự án #{request.DesignProjectId} không tồn tại.");
        }

        if (request.OwnerUserId.HasValue &&
            !await db.Users.AnyAsync(u => u.Id == request.OwnerUserId.Value, ct))
        {
            throw new ConstructionTaskOperationException($"Người phụ trách #{request.OwnerUserId} không tồn tại.");
        }

        var code = (request.TaskCode ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(code))
        {
            code = await AllocateTaskCodeAsync(request.DesignProjectId, ct);
        }
        else if (await db.ConstructionTasks.AnyAsync(t => t.DesignProjectId == request.DesignProjectId && t.TaskCode == code, ct))
        {
            throw new ConstructionTaskOperationException($"Mã công việc '{code}' đã được dùng trong dự án này.");
        }

        var entity = new ConstructionTask
        {
            DesignProjectId = request.DesignProjectId,
            TaskCode = code,
            Wbs = TrimOrNull(request.Wbs),
            Name = name,
            Description = TrimOrNull(request.Description),
            PlannedStart = request.PlannedStart,
            PlannedEnd = request.PlannedEnd,
            ProgressPercent = 0,
            OwnerUserId = request.OwnerUserId,
            Status = ConstructionTaskStatus.Planned,
            CreatedByUserId = callerUserId,
            UpdatedByUserId = callerUserId,
        };
        db.ConstructionTasks.Add(entity);
        await db.SaveChangesAsync(ct);

        if (request.PredecessorTaskIds is { Count: > 0 })
        {
            await ReplacePredecessorsAsync(entity, request.PredecessorTaskIds, ct);
        }

        logger.LogInformation("ConstructionTask {Id} ({Code}) created on project {ProjectId}",
            entity.Id, entity.TaskCode, entity.DesignProjectId);
        return (await GetAsync(entity.Id, ct))!;
    }

    public async Task<ConstructionTaskResponse?> UpdateAsync(int id, UpdateConstructionTaskRequest request, int callerUserId, CancellationToken ct = default)
    {
        var entity = await db.ConstructionTasks.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (entity is null) return null;

        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ConstructionTaskOperationException("Tên công việc là bắt buộc.");
        }
        if (request.PlannedEnd < request.PlannedStart)
        {
            throw new ConstructionTaskOperationException("Ngày kết thúc dự kiến phải sau hoặc bằng ngày bắt đầu.");
        }
        if (request.ActualStart.HasValue && request.ActualEnd.HasValue
            && request.ActualEnd.Value < request.ActualStart.Value)
        {
            throw new ConstructionTaskOperationException("Ngày kết thúc thực tế phải sau hoặc bằng ngày bắt đầu thực tế.");
        }
        if (request.ProgressPercent < 0 || request.ProgressPercent > 100)
        {
            throw new ConstructionTaskOperationException("Tiến độ hoàn thành phải từ 0 đến 100.");
        }
        if (!Enum.TryParse<ConstructionTaskStatus>(request.Status, true, out var nextStatus))
        {
            throw new ConstructionTaskOperationException($"Trạng thái '{request.Status}' không hợp lệ.");
        }
        if (request.OwnerUserId.HasValue &&
            !await db.Users.AnyAsync(u => u.Id == request.OwnerUserId.Value, ct))
        {
            throw new ConstructionTaskOperationException($"Người phụ trách #{request.OwnerUserId} không tồn tại.");
        }

        entity.Wbs = TrimOrNull(request.Wbs);
        entity.Name = name;
        entity.Description = TrimOrNull(request.Description);
        entity.PlannedStart = request.PlannedStart;
        entity.PlannedEnd = request.PlannedEnd;
        entity.ActualStart = request.ActualStart;
        entity.ProgressPercent = request.ProgressPercent;
        entity.OwnerUserId = request.OwnerUserId;
        var (resolvedStatus, resolvedActualEnd) = ApplyStatusRules(
            nextStatus, request.ActualStart, request.ActualEnd, request.ProgressPercent,
            DateOnly.FromDateTime(DateTime.UtcNow));
        entity.Status = resolvedStatus;
        entity.ActualEnd = resolvedActualEnd;
        entity.UpdatedByUserId = callerUserId;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("ConstructionTask {Id} updated (status={Status}, progress={Progress}%)",
            id, entity.Status, entity.ProgressPercent);
        return await GetAsync(id, ct);
    }

    public async Task<ConstructionTaskResponse?> UpdateProgressAsync(int id, UpdateConstructionTaskProgressRequest request, int callerUserId, CancellationToken ct = default)
    {
        if (request.ProgressPercent < 0 || request.ProgressPercent > 100)
        {
            throw new ConstructionTaskOperationException("Tiến độ hoàn thành phải từ 0 đến 100.");
        }
        var entity = await db.ConstructionTasks.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (entity is null) return null;

        entity.ProgressPercent = request.ProgressPercent;
        if (request.ActualStart.HasValue) entity.ActualStart = request.ActualStart;
        if (request.ActualEnd.HasValue) entity.ActualEnd = request.ActualEnd;

        var requestedStatus = entity.Status;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<ConstructionTaskStatus>(request.Status, true, out var st))
            {
                throw new ConstructionTaskOperationException($"Trạng thái '{request.Status}' không hợp lệ.");
            }
            requestedStatus = st;
        }
        var (resolvedStatus, resolvedActualEnd) = ApplyStatusRules(
            requestedStatus, entity.ActualStart, entity.ActualEnd, entity.ProgressPercent,
            DateOnly.FromDateTime(DateTime.UtcNow));
        entity.Status = resolvedStatus;
        entity.ActualEnd = resolvedActualEnd;

        if (entity.ActualStart.HasValue && entity.ActualEnd.HasValue
            && entity.ActualEnd.Value < entity.ActualStart.Value)
        {
            throw new ConstructionTaskOperationException("Ngày kết thúc thực tế phải sau hoặc bằng ngày bắt đầu thực tế.");
        }

        entity.UpdatedByUserId = callerUserId;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<ConstructionTaskResponse> SetPredecessorsAsync(int id, SetConstructionTaskPredecessorsRequest request, int callerUserId, CancellationToken ct = default)
    {
        var entity = await db.ConstructionTasks
            .Include(t => t.Predecessors)
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new ConstructionTaskOperationException($"Công việc #{id} không tồn tại.");

        await ReplacePredecessorsAsync(entity, request.PredecessorTaskIds ?? new List<int>(), ct);
        entity.UpdatedByUserId = callerUserId;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return (await GetAsync(id, ct))!;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.ConstructionTasks.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (entity is null) return false;

        // Block delete when this task is a predecessor for another —
        // the caller must first re-wire the dependent tasks.
        var dependents = await db.ConstructionTaskDependencies
            .CountAsync(d => d.PredecessorTaskId == id, ct);
        if (dependents > 0)
        {
            throw new ConstructionTaskOperationException(
                $"Không thể xoá — công việc này đang là tiền nhiệm của {dependents} công việc khác.");
        }

        // Explicitly clear this task's own predecessor edges before
        // dropping the task itself — SQL Server cascades handle this via
        // the mapping, but the in-memory provider used in tests does not.
        var ownEdges = await db.ConstructionTaskDependencies
            .Where(d => d.TaskId == id)
            .ToListAsync(ct);
        if (ownEdges.Count > 0)
        {
            db.ConstructionTaskDependencies.RemoveRange(ownEdges);
        }

        db.ConstructionTasks.Remove(entity);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("ConstructionTask {Id} deleted", id);
        return true;
    }

    public async Task<ConstructionTaskBulkDeleteResponse> BulkDeleteAsync(IReadOnlyList<int> ids, CancellationToken ct = default)
    {
        if (ids is null || ids.Count == 0)
        {
            throw new ConstructionTaskOperationException("Danh sách công việc cần xoá là bắt buộc.");
        }
        if (ids.Count > MaxBulkDelete)
        {
            throw new ConstructionTaskOperationException(
                $"Chỉ xoá tối đa {MaxBulkDelete} công việc mỗi lần.");
        }

        var distinctIds = ids.Distinct().ToList();
        var rows = await db.ConstructionTasks
            .Where(t => distinctIds.Contains(t.Id))
            .ToListAsync(ct);
        // Count only *external* dependents — edges pointing from a task
        // that is itself in the delete set will be cleaned up before the
        // row is removed, so they must not block the operation. Without
        // this exclusion, bulk-deleting a whole dependency chain would
        // always fail on the earlier links.
        var dependencyCounts = await db.ConstructionTaskDependencies
            .Where(d => distinctIds.Contains(d.PredecessorTaskId)
                     && !distinctIds.Contains(d.TaskId))
            .GroupBy(d => d.PredecessorTaskId)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, ct);

        var response = new ConstructionTaskBulkDeleteResponse { Requested = distinctIds.Count };
        var found = rows.Select(r => r.Id).ToHashSet();
        foreach (var missing in distinctIds.Where(id => !found.Contains(id)))
        {
            response.Failures.Add(new ConstructionTaskBulkDeleteFailure
            {
                Id = missing,
                Message = $"Công việc #{missing} không tồn tại.",
            });
        }

        var toDelete = new List<ConstructionTask>();
        foreach (var row in rows)
        {
            if (dependencyCounts.TryGetValue(row.Id, out var depCount) && depCount > 0)
            {
                response.Failures.Add(new ConstructionTaskBulkDeleteFailure
                {
                    Id = row.Id,
                    Message = $"Đang là tiền nhiệm của {depCount} công việc khác.",
                });
                continue;
            }
            toDelete.Add(row);
        }

        if (toDelete.Count > 0)
        {
            var toDeleteIds = toDelete.Select(t => t.Id).ToList();
            // Same reasoning as DeleteAsync — explicit dependency cleanup
            // for providers that don't honour cascade delete.
            var ownEdges = await db.ConstructionTaskDependencies
                .Where(d => toDeleteIds.Contains(d.TaskId))
                .ToListAsync(ct);
            if (ownEdges.Count > 0)
            {
                db.ConstructionTaskDependencies.RemoveRange(ownEdges);
            }
            db.ConstructionTasks.RemoveRange(toDelete);
            await db.SaveChangesAsync(ct);
            response.Deleted = toDelete.Count;
            logger.LogInformation("ConstructionTask bulk-deleted {Count} rows ({Ids})",
                toDelete.Count, string.Join(",", toDelete.Select(r => r.Id)));
        }
        return response;
    }

    // ------------------------------ Helpers ---------------------------------

    private async Task<string> AllocateTaskCodeAsync(int projectId, CancellationToken ct)
    {
        var used = await db.ConstructionTasks
            .Where(t => t.DesignProjectId == projectId)
            .Select(t => t.TaskCode)
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
        return $"T-{maxSeq + 1:D3}";
    }

    /// <summary>
    /// Slice-1 status/date rules — kept intentionally permissive so the
    /// owner can correct a mistake, but auto-derives obvious edges from
    /// the numeric fields so the Gantt chart never contradicts them.
    /// Returns the resolved <c>(status, actualEnd)</c> pair so the
    /// caller can persist both without duplicating the branching.
    /// </summary>
    private static (ConstructionTaskStatus Status, DateOnly? ActualEnd) ApplyStatusRules(
        ConstructionTaskStatus requested,
        DateOnly? actualStart,
        DateOnly? actualEnd,
        int progress,
        DateOnly today)
    {
        // 100% progress => Completed, and auto-fill actualEnd to today
        // if the user didn't supply one. Without this, we'd be left with
        // an ambiguous "100% but still InProgress" state that the Gantt
        // header would silently misclassify.
        if (progress >= 100)
        {
            return (ConstructionTaskStatus.Completed, actualEnd ?? today);
        }
        // Actual start set + not yet Completed/Cancelled => at least
        // InProgress. Preserve an already-InProgress request.
        if (actualStart.HasValue && requested == ConstructionTaskStatus.Planned)
        {
            return (ConstructionTaskStatus.InProgress, actualEnd);
        }
        return (requested, actualEnd);
    }

    private async Task ReplacePredecessorsAsync(ConstructionTask task, List<int> predecessorIds, CancellationToken ct)
    {
        var cleanIds = predecessorIds
            .Where(id => id > 0 && id != task.Id)
            .Distinct()
            .ToList();

        if (cleanIds.Count > 0)
        {
            var candidates = await db.ConstructionTasks
                .AsNoTracking()
                .Where(t => cleanIds.Contains(t.Id))
                .Select(t => new { t.Id, t.DesignProjectId })
                .ToListAsync(ct);
            var missing = cleanIds.Except(candidates.Select(c => c.Id)).ToList();
            if (missing.Count > 0)
            {
                throw new ConstructionTaskOperationException(
                    $"Công việc tiền nhiệm không tồn tại: {string.Join(", ", missing)}.");
            }
            var crossProject = candidates.Where(c => c.DesignProjectId != task.DesignProjectId).ToList();
            if (crossProject.Count > 0)
            {
                throw new ConstructionTaskOperationException(
                    "Tiền nhiệm phải cùng dự án với công việc hiện tại.");
            }
            // Cycle check: pretend to add each edge, then DFS from `task`
            // and verify we don't loop back to it through the existing graph.
            await EnsureNoCycleAsync(task.Id, cleanIds, ct);
        }

        // Diff against the existing edges instead of blindly delete+insert.
        // Keeps the audit trail readable ("no-op saves" no longer flip
        // dependency rows) and cuts a round-trip when nothing changed.
        var existing = await db.ConstructionTaskDependencies
            .Where(d => d.TaskId == task.Id)
            .ToListAsync(ct);
        var wantedSet = cleanIds.ToHashSet();
        var existingSet = existing.Select(e => e.PredecessorTaskId).ToHashSet();

        var toRemove = existing.Where(e => !wantedSet.Contains(e.PredecessorTaskId)).ToList();
        var toAdd = cleanIds.Where(id => !existingSet.Contains(id)).ToList();

        if (toRemove.Count > 0)
        {
            db.ConstructionTaskDependencies.RemoveRange(toRemove);
        }
        foreach (var pid in toAdd)
        {
            db.ConstructionTaskDependencies.Add(new ConstructionTaskDependency
            {
                TaskId = task.Id,
                PredecessorTaskId = pid,
            });
        }
        if (toRemove.Count > 0 || toAdd.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task EnsureNoCycleAsync(int taskId, List<int> newPredecessorIds, CancellationToken ct)
    {
        // Build the adjacency map (successor -> predecessors) for the same
        // project so DFS stays small even on large graphs.
        var project = await db.ConstructionTasks
            .Where(t => t.Id == taskId)
            .Select(t => t.DesignProjectId)
            .FirstAsync(ct);
        var edges = await db.ConstructionTaskDependencies
            .AsNoTracking()
            .Where(d => d.Task.DesignProjectId == project)
            .Select(d => new { d.TaskId, d.PredecessorTaskId })
            .ToListAsync(ct);

        var adj = edges
            .GroupBy(e => e.TaskId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.PredecessorTaskId).ToHashSet());
        adj[taskId] = new HashSet<int>(newPredecessorIds); // simulate replacement

        // DFS: starting from taskId, follow predecessors — if we come back
        // to taskId we've formed a cycle.
        var visited = new HashSet<int>();
        var stack = new Stack<int>();
        foreach (var start in newPredecessorIds) stack.Push(start);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node == taskId)
            {
                throw new ConstructionTaskOperationException(
                    "Cấu hình tiền nhiệm tạo vòng lặp phụ thuộc.");
            }
            if (!visited.Add(node)) continue;
            if (adj.TryGetValue(node, out var preds))
            {
                foreach (var p in preds) stack.Push(p);
            }
        }
    }

    private static string? TrimOrNull(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static ConstructionTaskResponse Map(ConstructionTask t, DateOnly today) => new()
    {
        Id = t.Id,
        DesignProjectId = t.DesignProjectId,
        DesignProjectCode = t.DesignProject?.ProjectCode,
        DesignProjectName = t.DesignProject?.Name,
        TaskCode = t.TaskCode,
        Wbs = t.Wbs,
        Name = t.Name,
        Description = t.Description,
        PlannedStart = t.PlannedStart,
        PlannedEnd = t.PlannedEnd,
        ActualStart = t.ActualStart,
        ActualEnd = t.ActualEnd,
        ProgressPercent = t.ProgressPercent,
        OwnerUserId = t.OwnerUserId,
        OwnerName = t.Owner?.FullName,
        Status = t.Status.ToString(),
        IsOverdue = t.PlannedEnd < today
                    && t.Status != ConstructionTaskStatus.Completed
                    && t.Status != ConstructionTaskStatus.Cancelled,
        Predecessors = t.Predecessors
            .OrderBy(pd => pd.PredecessorTask?.TaskCode)
            .Select(pd => new ConstructionTaskDependencyResponse
            {
                Id = pd.Id,
                PredecessorTaskId = pd.PredecessorTaskId,
                PredecessorTaskCode = pd.PredecessorTask?.TaskCode,
                PredecessorTaskName = pd.PredecessorTask?.Name,
                PredecessorStatus = pd.PredecessorTask?.Status.ToString(),
            }).ToList(),
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt,
    };
}
