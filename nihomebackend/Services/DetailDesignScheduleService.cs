using System.Text.Json;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public sealed class DesignScheduleOperationException(string message) : Exception(message);

public static class DesignScheduleRules
{
    private static readonly IReadOnlyDictionary<DesignScheduleStatus, DesignScheduleStatus[]> Transitions =
        new Dictionary<DesignScheduleStatus, DesignScheduleStatus[]>
        {
            [DesignScheduleStatus.NotStarted] =
                [DesignScheduleStatus.InProgress, DesignScheduleStatus.OnHold, DesignScheduleStatus.WaitingForDepartment],
            [DesignScheduleStatus.InProgress] =
                [DesignScheduleStatus.Completed, DesignScheduleStatus.OnHold, DesignScheduleStatus.WaitingForDepartment],
            [DesignScheduleStatus.OnHold] =
                [DesignScheduleStatus.InProgress, DesignScheduleStatus.WaitingForDepartment],
            [DesignScheduleStatus.WaitingForDepartment] =
                [DesignScheduleStatus.InProgress, DesignScheduleStatus.OnHold],
            [DesignScheduleStatus.Completed] = [],
        };

    public static DesignScheduleStatus ParseStatus(string value)
    {
        if (!Enum.TryParse<DesignScheduleStatus>(value?.Trim(), true, out var status) ||
            !Enum.IsDefined(status))
            throw new DesignScheduleOperationException(
                "Trạng thái không hợp lệ. Giá trị hợp lệ: NotStarted, InProgress, Completed, OnHold, WaitingForDepartment.");
        return status;
    }

    public static void ValidateDatesAndStatus(
        DateOnly plannedStart,
        DateOnly plannedEnd,
        DateOnly? actualStart,
        DateOnly? actualEnd,
        DesignScheduleStatus status,
        int progressPercent,
        int weight,
        bool isMilestone = false)
    {
        if (plannedEnd < plannedStart)
            throw new DesignScheduleOperationException("Ngày kết thúc kế hoạch không được trước ngày bắt đầu kế hoạch.");
        if (isMilestone && plannedStart != plannedEnd)
            throw new DesignScheduleOperationException("Mốc thiết kế phải có ngày bắt đầu và kết thúc kế hoạch giống nhau.");
        if (actualEnd.HasValue && !actualStart.HasValue)
            throw new DesignScheduleOperationException("Ngày kết thúc thực tế yêu cầu ngày bắt đầu thực tế.");
        if (actualEnd < actualStart)
            throw new DesignScheduleOperationException("Ngày kết thúc thực tế không được trước ngày bắt đầu thực tế.");
        if (progressPercent is < 0 or > 100)
            throw new DesignScheduleOperationException("Tiến độ phải từ 0 đến 100 phần trăm.");
        if (weight is < 1 or > 100)
            throw new DesignScheduleOperationException("Trọng số phải từ 1 đến 100.");
        if (status == DesignScheduleStatus.NotStarted &&
            (actualStart.HasValue || actualEnd.HasValue || progressPercent != 0))
            throw new DesignScheduleOperationException("NotStarted yêu cầu chưa có ngày thực tế và tiến độ bằng 0.");
        if (status == DesignScheduleStatus.InProgress && !actualStart.HasValue)
            throw new DesignScheduleOperationException("InProgress yêu cầu ngày bắt đầu thực tế.");
        if (status == DesignScheduleStatus.Completed &&
            (!actualStart.HasValue || !actualEnd.HasValue || progressPercent != 100))
            throw new DesignScheduleOperationException(
                "Completed yêu cầu ngày bắt đầu, ngày kết thúc thực tế và tiến độ bằng 100.");
        if (status != DesignScheduleStatus.Completed && actualEnd.HasValue)
            throw new DesignScheduleOperationException("Ngày kết thúc thực tế chỉ được ghi nhận khi trạng thái là Completed.");
    }

    public static void ValidateTransition(DesignScheduleStatus current, DesignScheduleStatus next)
    {
        if (current == next) return;
        if (!Transitions[current].Contains(next))
            throw new DesignScheduleOperationException(
                $"Không thể chuyển trạng thái từ {current} sang {next}.");
    }

    public static (bool BaselineReady, decimal? Progress) CalculateRollup(
        IEnumerable<(int Weight, int Progress)> sources)
    {
        var values = sources.ToList();
        var ready = values.Count > 0 && values.Sum(value => value.Weight) == 100;
        return (ready, ready
            ? Math.Round(values.Sum(value => value.Weight * value.Progress) / 100m, 2)
            : null);
    }

    public static bool HasCycle(int taskId, IReadOnlyDictionary<int, IReadOnlyCollection<int>> predecessors)
    {
        var visiting = new HashSet<int>();
        var visited = new HashSet<int>();
        return Visit(taskId);

        bool Visit(int current)
        {
            if (visited.Contains(current)) return false;
            if (!visiting.Add(current)) return true;
            foreach (var predecessor in predecessors.GetValueOrDefault(current) ?? [])
                if (Visit(predecessor)) return true;
            visiting.Remove(current);
            visited.Add(current);
            return false;
        }
    }
}

public interface IDetailDesignScheduleService
{
    Task<DesignScheduleResponse?> GetAsync(int projectId, DesignScheduleQuery query, int callerUserId, CancellationToken ct);
    Task<DesignScheduleResponse> InitializeAsync(int projectId, InitializeDesignScheduleRequest request, int callerUserId, CancellationToken ct);
    Task<DesignSchedulePhaseResponse?> UpdatePhaseAsync(int projectId, int phaseId, UpsertDesignSchedulePhaseRequest request, int callerUserId, CancellationToken ct);
    Task<DesignScheduleTaskResponse?> CreateTaskAsync(int projectId, int phaseId, UpsertDesignScheduleTaskRequest request, int callerUserId, CancellationToken ct);
    Task<DesignScheduleTaskResponse?> UpdateTaskAsync(int projectId, int taskId, UpsertDesignScheduleTaskRequest request, int callerUserId, CancellationToken ct);
}

public sealed class DetailDesignScheduleService(AppDbContext db, IProjectAccessService access)
    : IDetailDesignScheduleService
{
    private const string DepartmentCategory = "project-department";

    public async Task<DesignScheduleResponse?> GetAsync(
        int projectId,
        DesignScheduleQuery query,
        int callerUserId,
        CancellationToken ct)
    {
        if (!await access.CanViewOperationalProjectAsync(callerUserId, projectId, ct)) return null;
        var designProjectId = await GetDesignProjectIdAsync(projectId, ct);
        if (!designProjectId.HasValue) return null;
        return await BuildResponseAsync(projectId, designProjectId.Value, query, callerUserId, ct);
    }

    public async Task<DesignScheduleResponse> InitializeAsync(
        int projectId,
        InitializeDesignScheduleRequest request,
        int callerUserId,
        CancellationToken ct)
    {
        await EnsureCanManageAsync(projectId, callerUserId, ct);
        var project = await db.DesignProjects.SingleOrDefaultAsync(item =>
            item.OperationalProjectId == projectId, ct)
            ?? throw new DesignScheduleOperationException("Dự án thiết kế không tồn tại trong Dự án vận hành.");
        if (!project.StartDate.HasValue || !project.Deadline.HasValue)
            throw new DesignScheduleOperationException(
                "Khởi tạo lịch yêu cầu Dự án thiết kế có ngày bắt đầu và hạn hoàn thành.");

        var definitions = ParseInitialization(request);
        await using var transaction = await BeginScheduleMutationAsync(projectId, ct);
        var existing = await db.DesignSchedulePhases.Where(item =>
                item.OperationalProjectId == projectId)
            .OrderBy(item => item.Code)
            .ToListAsync(ct);
        if (existing.Count > 0)
        {
            var canonicalCodes = Enum.GetValues<DesignSchedulePhaseCode>();
            if (existing.Count != canonicalCodes.Length ||
                existing.Select(item => item.Code).Distinct().Count() != canonicalCodes.Length ||
                canonicalCodes.Any(code => existing.All(item => item.Code != code)) ||
                existing.Any(item => item.DesignProjectId != project.Id))
                throw new DesignScheduleOperationException(
                    "Lịch thiết kế hiện có không chứa đúng ba giai đoạn chuẩn; cần xử lý dữ liệu trước khi khởi tạo lại.");
        }
        else
        {
            var start = DateOnly.FromDateTime(project.StartDate.Value.ToUniversalTime());
            var end = DateOnly.FromDateTime(project.Deadline.Value.ToUniversalTime());
            if (end < start)
                throw new DesignScheduleOperationException("Hạn hoàn thành Dự án thiết kế không được trước ngày bắt đầu.");
            var inclusiveDays = end.DayNumber - start.DayNumber + 1;
            if (inclusiveDays < definitions.Count)
                throw new DesignScheduleOperationException(
                    "Khoảng ngày Dự án thiết kế phải có ít nhất ba ngày để khởi tạo ba giai đoạn không chồng lấn.");
            var baseLength = inclusiveDays / definitions.Count;
            var remainder = inclusiveDays % definitions.Count;
            var phaseStart = start;
            var now = DateTime.UtcNow;
            foreach (var (code, weight, index) in definitions.Select((value, index) =>
                         (value.Code, value.Weight, index)))
            {
                var phaseLength = baseLength + (index < remainder ? 1 : 0);
                var phaseEnd = phaseStart.AddDays(phaseLength - 1);
                var phase = new DesignSchedulePhase
                {
                    OperationalProjectId = projectId,
                    DesignProjectId = project.Id,
                    Code = code,
                    PlannedStart = phaseStart,
                    PlannedEnd = phaseEnd,
                    Status = DesignScheduleStatus.NotStarted,
                    ProgressPercent = 0,
                    Weight = weight,
                    CreatedAt = now,
                    UpdatedAt = now,
                    CreatedByUserId = callerUserId,
                    UpdatedByUserId = callerUserId,
                };
                db.DesignSchedulePhases.Add(phase);
                phaseStart = phaseEnd.AddDays(1);
            }
            await db.SaveChangesAsync(ct);
            foreach (var phase in await db.DesignSchedulePhases.Where(item =>
                         item.OperationalProjectId == projectId).ToListAsync(ct))
                await AddHistoryAsync(phase, "Phase", phase.Id, "Initialized", callerUserId, ct);
        }

        if (transaction is not null) await transaction.CommitAsync(ct);

        return await BuildResponseAsync(projectId, project.Id, new DesignScheduleQuery(), callerUserId, ct);
    }

    public async Task<DesignSchedulePhaseResponse?> UpdatePhaseAsync(
        int projectId,
        int phaseId,
        UpsertDesignSchedulePhaseRequest request,
        int callerUserId,
        CancellationToken ct)
    {
        await EnsureCanManageAsync(projectId, callerUserId, ct);
        await using var transaction = await BeginScheduleMutationAsync(projectId, ct);
        var phase = await db.DesignSchedulePhases.SingleOrDefaultAsync(item =>
            item.Id == phaseId && item.OperationalProjectId == projectId, ct);
        if (phase is null) return null;
        var status = DesignScheduleRules.ParseStatus(request.Status);
        DesignScheduleRules.ValidateTransition(phase.Status, status);
        var plannedStart = RequireDate(request.PlannedStart, "Ngày bắt đầu kế hoạch");
        var plannedEnd = RequireDate(request.PlannedEnd, "Ngày kết thúc kế hoạch");
        DesignScheduleRules.ValidateDatesAndStatus(plannedStart, plannedEnd,
            request.ActualStart, request.ActualEnd, status, request.ProgressPercent, request.Weight);
        var baseline = await db.DesignSchedulePhases.AsNoTracking()
            .Where(item => item.OperationalProjectId == projectId)
            .Select(item => new { item.Id, item.DesignProjectId, item.Code, item.Weight })
            .ToListAsync(ct);
        var canonicalCodes = Enum.GetValues<DesignSchedulePhaseCode>();
        if (baseline.Count != canonicalCodes.Length ||
            baseline.Select(item => item.Code).Distinct().Count() != canonicalCodes.Length ||
            canonicalCodes.Any(code => baseline.All(item => item.Code != code)) ||
            baseline.Any(item => item.DesignProjectId != phase.DesignProjectId))
            throw new DesignScheduleOperationException(
                "Lịch thiết kế hiện có không chứa đúng ba giai đoạn chuẩn; cần xử lý dữ liệu trước khi cập nhật.");
        var resultingWeightTotal = request.Weight + baseline
            .Where(item => item.Id != phaseId)
            .Sum(item => item.Weight);
        if (resultingWeightTotal != 100)
            throw new DesignScheduleOperationException("Tổng trọng số ba giai đoạn phải bằng 100.");
        CrmConcurrency.Apply(db, phase, request.RowVersion);
        phase.PlannedStart = plannedStart;
        phase.PlannedEnd = plannedEnd;
        phase.ActualStart = request.ActualStart;
        phase.ActualEnd = request.ActualEnd;
        phase.Status = status;
        phase.ProgressPercent = request.ProgressPercent;
        phase.Weight = request.Weight;
        phase.UpdatedAt = DateTime.UtcNow;
        phase.UpdatedByUserId = callerUserId;
        await CrmConcurrency.SaveChangesAsync(db, ct);
        var response = await MapPhaseAsync(phase.Id, ct);
        await AddHistoryAsync(phase, "Phase", phase.Id, "Updated", callerUserId, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return response;
    }

    public async Task<DesignScheduleTaskResponse?> CreateTaskAsync(
        int projectId,
        int phaseId,
        UpsertDesignScheduleTaskRequest request,
        int callerUserId,
        CancellationToken ct)
    {
        await EnsureCanManageAsync(projectId, callerUserId, ct);
        await using var transaction = await BeginScheduleMutationAsync(projectId, ct);
        var phase = await db.DesignSchedulePhases.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == phaseId && item.OperationalProjectId == projectId, ct);
        if (phase is null) return null;
        var status = await ValidateTaskAsync(projectId, null, request, ct);
        var predecessorIds = NormalizePredecessors(request.PredecessorTaskIds);
        await ValidatePredecessorScopeAsync(projectId, predecessorIds, ct);
        var plannedStart = RequireDate(request.PlannedStart, "Ngày bắt đầu kế hoạch");
        var plannedEnd = RequireDate(request.PlannedEnd, "Ngày kết thúc kế hoạch");
        var now = DateTime.UtcNow;
        var task = new DesignScheduleTask
        {
            OperationalProjectId = projectId,
            DesignProjectId = phase.DesignProjectId,
            PhaseId = phase.Id,
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            DepartmentCode = request.DepartmentCode.Trim().ToLowerInvariant(),
            AssigneeMemberId = request.AssigneeMemberId,
            IsMilestone = request.IsMilestone,
            PlannedStart = plannedStart,
            PlannedEnd = plannedEnd,
            ActualStart = request.ActualStart,
            ActualEnd = request.ActualEnd,
            Status = status,
            ProgressPercent = request.ProgressPercent,
            Weight = request.Weight,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedByUserId = callerUserId,
            UpdatedByUserId = callerUserId,
        };
        db.DesignScheduleTasks.Add(task);
        await db.SaveChangesAsync(ct);
        await ReplaceDependenciesAsync(task, predecessorIds, ct);
        await db.SaveChangesAsync(ct);
        var response = await MapTaskAsync(task.Id, ct);
        await AddHistoryAsync(task, "Task", task.Id, "Created", callerUserId, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return response;
    }

    public async Task<DesignScheduleTaskResponse?> UpdateTaskAsync(
        int projectId,
        int taskId,
        UpsertDesignScheduleTaskRequest request,
        int callerUserId,
        CancellationToken ct)
    {
        await EnsureCanManageAsync(projectId, callerUserId, ct);
        await using var transaction = await BeginScheduleMutationAsync(projectId, ct);
        var task = await db.DesignScheduleTasks.Include(item => item.Predecessors).SingleOrDefaultAsync(item =>
            item.Id == taskId && item.OperationalProjectId == projectId, ct);
        if (task is null) return null;
        var status = await ValidateTaskAsync(projectId, taskId, request, ct);
        DesignScheduleRules.ValidateTransition(task.Status, status);
        var predecessorIds = NormalizePredecessors(request.PredecessorTaskIds);
        var plannedStart = RequireDate(request.PlannedStart, "Ngày bắt đầu kế hoạch");
        var plannedEnd = RequireDate(request.PlannedEnd, "Ngày kết thúc kế hoạch");
        CrmConcurrency.Apply(db, task, request.RowVersion);
        task.Code = request.Code.Trim();
        task.Name = request.Name.Trim();
        task.DepartmentCode = request.DepartmentCode.Trim().ToLowerInvariant();
        task.AssigneeMemberId = request.AssigneeMemberId;
        task.IsMilestone = request.IsMilestone;
        task.PlannedStart = plannedStart;
        task.PlannedEnd = plannedEnd;
        task.ActualStart = request.ActualStart;
        task.ActualEnd = request.ActualEnd;
        task.Status = status;
        task.ProgressPercent = request.ProgressPercent;
        task.Weight = request.Weight;
        task.UpdatedAt = DateTime.UtcNow;
        task.UpdatedByUserId = callerUserId;
        await ReplaceDependenciesAsync(task, predecessorIds, ct);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        var response = await MapTaskAsync(task.Id, ct);
        await AddHistoryAsync(task, "Task", task.Id, "Updated", callerUserId, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return response;
    }

    private async Task<DesignScheduleStatus> ValidateTaskAsync(
        int projectId,
        int? taskId,
        UpsertDesignScheduleTaskRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
            throw new DesignScheduleOperationException("Mã và tên công việc là bắt buộc.");
        var status = DesignScheduleRules.ParseStatus(request.Status);
        var plannedStart = RequireDate(request.PlannedStart, "Ngày bắt đầu kế hoạch");
        var plannedEnd = RequireDate(request.PlannedEnd, "Ngày kết thúc kế hoạch");
        DesignScheduleRules.ValidateDatesAndStatus(plannedStart, plannedEnd,
            request.ActualStart, request.ActualEnd, status, request.ProgressPercent, request.Weight,
            request.IsMilestone);
        var department = request.DepartmentCode.Trim().ToLowerInvariant();
        if (!await db.MasterDataOptions.AsNoTracking().AnyAsync(option =>
                option.Category == DepartmentCategory && option.Code == department && option.IsActive, ct))
            throw new DesignScheduleOperationException(
                "Phòng ban không hợp lệ hoặc đã ngừng hoạt động. Ví dụ hợp lệ: design.");
        if (!await db.OperationalProjectMembers.AsNoTracking().AnyAsync(member =>
                member.Id == request.AssigneeMemberId && member.OperationalProjectId == projectId &&
                member.EndedAt == null && member.User.IsActive, ct))
            throw new DesignScheduleOperationException(
                "Người phụ trách phải là thành viên đang hiệu lực của cùng Dự án.");
        if (await db.DesignScheduleTasks.AsNoTracking().AnyAsync(item =>
                item.OperationalProjectId == projectId && item.Code == request.Code.Trim() &&
                item.Id != taskId, ct))
            throw new DesignScheduleOperationException("Mã công việc đã tồn tại trong lịch thiết kế.");
        return status;
    }

    private async Task ReplaceDependenciesAsync(
        DesignScheduleTask task,
        IEnumerable<int> requestedIds,
        CancellationToken ct)
    {
        var predecessorIds = requestedIds.Distinct().ToList();
        if (predecessorIds.Contains(task.Id))
            throw new DesignScheduleOperationException("Công việc không thể phụ thuộc vào chính nó.");
        await ValidatePredecessorScopeAsync(task.OperationalProjectId, predecessorIds, ct);

        var edges = (await db.DesignScheduleTaskDependencies.AsNoTracking()
                .Where(item => item.OperationalProjectId == task.OperationalProjectId && item.TaskId != task.Id)
                .Select(item => new { item.TaskId, item.PredecessorTaskId })
                .ToListAsync(ct))
            .GroupBy(item => item.TaskId)
            .ToDictionary(group => group.Key,
                group => (IReadOnlyCollection<int>)group.Select(item => item.PredecessorTaskId).ToList());
        edges[task.Id] = predecessorIds;
        if (DesignScheduleRules.HasCycle(task.Id, edges))
            throw new DesignScheduleOperationException("Quan hệ công việc tiền nhiệm tạo thành vòng lặp.");

        db.DesignScheduleTaskDependencies.RemoveRange(task.Predecessors);
        task.Predecessors = predecessorIds.Select(predecessorId => new DesignScheduleTaskDependency
        {
            OperationalProjectId = task.OperationalProjectId,
            TaskId = task.Id,
            PredecessorTaskId = predecessorId,
        }).ToList();
    }

    private async Task ValidatePredecessorScopeAsync(
        int projectId,
        IEnumerable<int> requestedIds,
        CancellationToken ct)
    {
        var predecessorIds = requestedIds.Distinct().ToList();
        var validCount = await db.DesignScheduleTasks.AsNoTracking().CountAsync(item =>
            predecessorIds.Contains(item.Id) && item.OperationalProjectId == projectId, ct);
        if (validCount != predecessorIds.Count)
            throw new DesignScheduleOperationException(
                "Mọi công việc tiền nhiệm phải thuộc cùng Dự án vận hành.");
    }

    private static DateOnly RequireDate(DateOnly? value, string fieldName) =>
        value ?? throw new DesignScheduleOperationException($"{fieldName} là bắt buộc.");

    private static IReadOnlyList<int> NormalizePredecessors(IEnumerable<int>? requestedIds)
    {
        if (requestedIds is null)
            throw new DesignScheduleOperationException("Danh sách công việc tiền nhiệm là bắt buộc.");
        var predecessorIds = requestedIds.Distinct().ToList();
        if (predecessorIds.Count > 100)
            throw new DesignScheduleOperationException("Danh sách công việc tiền nhiệm không được vượt quá 100 mục.");
        if (predecessorIds.Any(id => id <= 0))
            throw new DesignScheduleOperationException("Mã công việc tiền nhiệm phải là số nguyên dương.");
        return predecessorIds;
    }

    private async Task<IDbContextTransaction?> BeginScheduleMutationAsync(
        int projectId,
        CancellationToken ct)
    {
        if (!db.Database.IsRelational()) return null;
        var isSqlServer = string.Equals(
            db.Database.ProviderName,
            "Microsoft.EntityFrameworkCore.SqlServer",
            StringComparison.Ordinal);
        var transaction = await db.Database.BeginTransactionAsync(
            isSqlServer ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable,
            ct);
        if (!isSqlServer) return transaction;

        try
        {
            var resource = $"detail-design-schedule-{projectId}";
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock
                    @Resource = {resource},
                    @LockMode = 'Exclusive',
                    @LockOwner = 'Transaction',
                    @LockTimeout = 15000;
                IF @result < 0
                    THROW 51000, 'Unable to acquire the detail design schedule lock.', 1;
                """, ct);
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private async Task<DesignScheduleResponse> BuildResponseAsync(
        int projectId,
        int designProjectId,
        DesignScheduleQuery query,
        int callerUserId,
        CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var phases = await db.DesignSchedulePhases.AsNoTracking()
            .Where(item => item.OperationalProjectId == projectId)
            .OrderBy(item => item.Code)
            .ToListAsync(ct);
        var allTasks = await db.DesignScheduleTasks.AsNoTracking()
            .Include(item => item.Predecessors)
            .Include(item => item.Phase)
            .Where(item => item.OperationalProjectId == projectId)
            .ToListAsync(ct);
        var filtered = allTasks.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(query.Phase))
        {
            if (!Enum.TryParse<DesignSchedulePhaseCode>(query.Phase.Trim(), true, out var phaseCode) ||
                !Enum.IsDefined(phaseCode))
                throw new DesignScheduleOperationException("Giai đoạn không hợp lệ.");
            filtered = filtered.Where(item => item.Phase.Code == phaseCode);
        }
        if (query.AssigneeMemberId.HasValue)
            filtered = filtered.Where(item => item.AssigneeMemberId == query.AssigneeMemberId);
        if (!string.IsNullOrWhiteSpace(query.DepartmentCode))
            filtered = filtered.Where(item => string.Equals(item.DepartmentCode,
                query.DepartmentCode.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = DesignScheduleRules.ParseStatus(query.Status);
            filtered = filtered.Where(item => item.Status == status);
        }
        if (query.PlannedFrom.HasValue)
            filtered = filtered.Where(item => item.PlannedEnd >= query.PlannedFrom.Value);
        if (query.PlannedTo.HasValue)
            filtered = filtered.Where(item => item.PlannedStart <= query.PlannedTo.Value);
        if (query.OverdueOnly)
            filtered = filtered.Where(item => IsOverdue(item.PlannedEnd, item.Status, today));
        var ordered = filtered.OrderBy(item => item.PlannedStart).ThenBy(item => item.Id).ToList();
        var skip = (long)(query.Page - 1) * query.PageSize;

        var rollupSources = phases.Select(phase =>
        {
            var taskSources = allTasks.Where(task => task.PhaseId == phase.Id)
                .Select(task => new DesignScheduleTaskRollupSourceResponse
                {
                    TaskId = task.Id,
                    Weight = task.Weight,
                    ProgressPercent = task.ProgressPercent,
                    WeightedValue = task.Weight * task.ProgressPercent / 100m,
                }).ToList();
            var result = DesignScheduleRules.CalculateRollup(taskSources.Select(source =>
                (source.Weight, source.ProgressPercent)));
            return new DesignSchedulePhaseRollupResponse
            {
                PhaseId = phase.Id,
                Weight = phase.Weight,
                BaselineReady = result.BaselineReady,
                ProgressPercent = result.Progress,
                WeightedValue = result.Progress.HasValue ? phase.Weight * result.Progress / 100m : null,
                TaskSources = taskSources,
            };
        }).ToList();
        var projectReady = phases.Count == 3 && phases.Sum(item => item.Weight) == 100 &&
            rollupSources.All(item => item.BaselineReady);

        return new DesignScheduleResponse
        {
            OperationalProjectId = projectId,
            DesignProjectId = designProjectId,
            CanManage = await access.CanManageDesignScheduleAsync(callerUserId, projectId, ct),
            BaselineReady = projectReady,
            ProgressPercent = projectReady ? rollupSources.Sum(item => item.WeightedValue!.Value) : null,
            Phases = phases.Select(phase => MapPhase(phase, rollupSources.Single(item =>
                item.PhaseId == phase.Id), today)).ToList(),
            RollupSources = rollupSources,
            Tasks = new PagedDesignScheduleTasksResponse
            {
                Page = query.Page,
                PageSize = query.PageSize,
                TotalCount = ordered.Count,
                Items = skip >= ordered.Count
                    ? []
                    : ordered.Skip((int)skip).Take(query.PageSize)
                        .Select(task => MapTask(task, today)).ToList(),
            },
        };
    }

    private async Task<DesignSchedulePhaseResponse> MapPhaseAsync(int phaseId, CancellationToken ct)
    {
        var phase = await db.DesignSchedulePhases.AsNoTracking().SingleAsync(item => item.Id == phaseId, ct);
        var tasks = await db.DesignScheduleTasks.AsNoTracking().Where(item => item.PhaseId == phaseId).ToListAsync(ct);
        var rollup = DesignScheduleRules.CalculateRollup(tasks.Select(item => (item.Weight, item.ProgressPercent)));
        return MapPhase(phase, new DesignSchedulePhaseRollupResponse
        {
            PhaseId = phase.Id,
            Weight = phase.Weight,
            BaselineReady = rollup.BaselineReady,
            ProgressPercent = rollup.Progress,
        }, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    private async Task<DesignScheduleTaskResponse> MapTaskAsync(int taskId, CancellationToken ct) =>
        MapTask(await db.DesignScheduleTasks.AsNoTracking().Include(item => item.Phase)
            .Include(item => item.Predecessors).SingleAsync(item => item.Id == taskId, ct),
            DateOnly.FromDateTime(DateTime.UtcNow));

    private static DesignSchedulePhaseResponse MapPhase(
        DesignSchedulePhase phase,
        DesignSchedulePhaseRollupResponse rollup,
        DateOnly today) => new()
        {
            Id = phase.Id,
            Code = phase.Code.ToString(),
            PlannedStart = phase.PlannedStart,
            PlannedEnd = phase.PlannedEnd,
            ActualStart = phase.ActualStart,
            ActualEnd = phase.ActualEnd,
            Status = phase.Status.ToString(),
            ProgressPercent = phase.ProgressPercent,
            Weight = phase.Weight,
            Overdue = IsOverdue(phase.PlannedEnd, phase.Status, today),
            BaselineReady = rollup.BaselineReady,
            RolledUpProgressPercent = rollup.ProgressPercent,
            RowVersion = CrmConcurrency.Encode(phase.RowVersion),
        };

    private static DesignScheduleTaskResponse MapTask(DesignScheduleTask task, DateOnly today) => new()
    {
        Id = task.Id,
        PhaseId = task.PhaseId,
        PhaseCode = task.Phase.Code.ToString(),
        Code = task.Code,
        Name = task.Name,
        DepartmentCode = task.DepartmentCode,
        AssigneeMemberId = task.AssigneeMemberId,
        IsMilestone = task.IsMilestone,
        PlannedStart = task.PlannedStart,
        PlannedEnd = task.PlannedEnd,
        ActualStart = task.ActualStart,
        ActualEnd = task.ActualEnd,
        Status = task.Status.ToString(),
        ProgressPercent = task.ProgressPercent,
        Weight = task.Weight,
        Overdue = IsOverdue(task.PlannedEnd, task.Status, today),
        PredecessorTaskIds = task.Predecessors.Select(item => item.PredecessorTaskId).Order().ToList(),
        RowVersion = CrmConcurrency.Encode(task.RowVersion),
    };

    private static bool IsOverdue(DateOnly plannedEnd, DesignScheduleStatus status, DateOnly today) =>
        plannedEnd < today && status != DesignScheduleStatus.Completed;

    private static List<(DesignSchedulePhaseCode Code, int Weight)> ParseInitialization(
        InitializeDesignScheduleRequest request)
    {
        var expected = Enum.GetValues<DesignSchedulePhaseCode>();
        var parsed = request.Phases.Select(item =>
        {
            if (!Enum.TryParse<DesignSchedulePhaseCode>(item.Code?.Trim(), true, out var code))
                throw new DesignScheduleOperationException("Giai đoạn khởi tạo không hợp lệ.");
            if (item.Weight is < 1 or > 100)
                throw new DesignScheduleOperationException("Trọng số giai đoạn phải từ 1 đến 100.");
            return (Code: code, item.Weight);
        }).ToList();
        if (parsed.Count != expected.Length || parsed.Select(item => item.Code).Distinct().Count() != expected.Length ||
            expected.Any(code => parsed.All(item => item.Code != code)))
            throw new DesignScheduleOperationException(
                "Khởi tạo yêu cầu đúng ba giai đoạn Concept, BasicDesign và ShopDrawing, không trùng lặp.");
        if (parsed.Sum(item => item.Weight) != 100)
            throw new DesignScheduleOperationException("Tổng trọng số ba giai đoạn phải bằng 100.");
        return parsed.OrderBy(item => item.Code).ToList();
    }

    private async Task<int?> GetDesignProjectIdAsync(int projectId, CancellationToken ct) =>
        await db.DesignProjects.AsNoTracking().Where(item => item.OperationalProjectId == projectId)
            .Select(item => (int?)item.Id).SingleOrDefaultAsync(ct);

    private async Task EnsureCanManageAsync(int projectId, int callerUserId, CancellationToken ct)
    {
        if (!await access.CanManageDesignScheduleAsync(callerUserId, projectId, ct))
            throw new DesignScheduleOperationException(
                "Không tìm thấy Dự án hoặc bạn không có quyền quản lý lịch thiết kế.");
    }

    private async Task AddHistoryAsync(
        object snapshot,
        string entityType,
        int entityId,
        string action,
        int callerUserId,
        CancellationToken ct)
    {
        var projectId = snapshot switch
        {
            DesignSchedulePhase phase => (phase.OperationalProjectId, phase.DesignProjectId),
            DesignScheduleTask task => (task.OperationalProjectId, task.DesignProjectId),
            _ => throw new ArgumentOutOfRangeException(nameof(snapshot)),
        };
        object historySnapshot = snapshot switch
        {
            DesignSchedulePhase phase => new
            {
                phase.Id,
                phase.OperationalProjectId,
                phase.DesignProjectId,
                Code = phase.Code.ToString(),
                phase.PlannedStart,
                phase.PlannedEnd,
                phase.ActualStart,
                phase.ActualEnd,
                Status = phase.Status.ToString(),
                phase.ProgressPercent,
                phase.Weight,
                phase.CreatedAt,
                phase.CreatedByUserId,
                phase.UpdatedAt,
                phase.UpdatedByUserId,
            },
            DesignScheduleTask task => new
            {
                task.Id,
                task.OperationalProjectId,
                task.DesignProjectId,
                task.PhaseId,
                task.Code,
                task.Name,
                task.DepartmentCode,
                task.AssigneeMemberId,
                task.IsMilestone,
                task.PlannedStart,
                task.PlannedEnd,
                task.ActualStart,
                task.ActualEnd,
                Status = task.Status.ToString(),
                task.ProgressPercent,
                task.Weight,
                PredecessorTaskIds = task.Predecessors.Select(item => item.PredecessorTaskId).Order().ToList(),
                task.CreatedAt,
                task.CreatedByUserId,
                task.UpdatedAt,
                task.UpdatedByUserId,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(snapshot)),
        };
        db.DesignScheduleHistory.Add(new DesignScheduleHistory
        {
            OperationalProjectId = projectId.OperationalProjectId,
            DesignProjectId = projectId.DesignProjectId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            SnapshotJson = JsonSerializer.Serialize(historySnapshot),
            ChangedAt = DateTime.UtcNow,
            ChangedByUserId = callerUserId,
        });
        await db.SaveChangesAsync(ct);
    }
}