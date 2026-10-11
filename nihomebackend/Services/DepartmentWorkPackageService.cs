using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Models.Rbac;

namespace NihomeBackend.Services;

public sealed class DepartmentWorkPackageService(
    AppDbContext db,
    IProjectAccessService projectAccess,
    INotificationService notifications,
    ILogger<DepartmentWorkPackageService> logger) : IDepartmentWorkPackageService
{
    private const int MaxPageSize = 100;
    private static readonly string[] GenericProjectDepartmentCodes = ["PROCUREMENT", "FINANCE"];

    public async Task<DepartmentWorkPackageListResponse> ListAsync(
        DepartmentWorkPackageQuery query,
        int callerUserId,
        bool canViewAll,
        CancellationToken ct = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        var packages = BaseQuery();
        if (!canViewAll && !await IsAdministratorAsync(callerUserId, ct))
        {
            var projectIds = await projectAccess.GetAccessibleOperationalProjectIdsAsync(callerUserId, ct);
            packages = packages.Where(item =>
                item.CreatedByUserId == callerUserId || item.LeadUserId == callerUserId ||
                item.Tasks.Any(task => task.AssigneeUserId == callerUserId ||
                    task.ApproverUserId == callerUserId || task.CcUsers.Any(cc => cc.UserId == callerUserId)) ||
                item.OperationalProjectId.HasValue && projectIds.Contains(item.OperationalProjectId.Value));
        }
        if (!string.IsNullOrWhiteSpace(query.Kind) && Enum.TryParse<WorkPackageKind>(query.Kind, true, out var kind))
            packages = packages.Where(item => item.Kind == kind);
        if (query.OperationalProjectId.HasValue)
            packages = packages.Where(item => item.OperationalProjectId == query.OperationalProjectId);
        if (query.ContractId.HasValue) packages = packages.Where(item => item.ContractId == query.ContractId);
        if (query.SourceOpportunityId.HasValue)
            packages = packages.Where(item => item.SourceOpportunityId == query.SourceOpportunityId);
        if (query.DepartmentRoleGroupId.HasValue)
            packages = packages.Where(item => item.DepartmentRoleGroupId == query.DepartmentRoleGroupId);
        if (!string.IsNullOrWhiteSpace(query.Status) && Enum.TryParse<WorkPackageStatus>(query.Status, true, out var status))
            packages = packages.Where(item => item.Status == status);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            packages = packages.Where(item => item.Code.Contains(search) || item.Name.Contains(search));
        }

        var total = await packages.CountAsync(ct);
        var rows = await packages.OrderByDescending(item => item.UpdatedAt).ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var admin = await IsAdministratorAsync(callerUserId, ct);
        return new DepartmentWorkPackageListResponse
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Items = rows.Select(item => MapPackage(item, callerUserId, admin)).ToList(),
        };
    }

    public async Task<DepartmentWorkPackageResponse?> GetAsync(
        int id,
        int callerUserId,
        bool canViewAll,
        CancellationToken ct = default)
    {
        var item = await BaseQuery().SingleOrDefaultAsync(package => package.Id == id, ct);
        if (item is null || !await CanViewAsync(item, callerUserId, canViewAll, ct)) return null;
        return MapPackage(item, callerUserId, await IsAdministratorAsync(callerUserId, ct));
    }

    public async Task<DepartmentWorkPackageOptionsResponse> OptionsAsync(
        int callerUserId,
        CancellationToken ct = default)
    {
        var departments = await db.RoleGroups.AsNoTracking().Where(item => item.IsActive)
            .OrderBy(item => item.SortOrder).ThenBy(item => item.Code)
            .Select(item => new DepartmentOptionResponse
            {
                Id = item.Id,
                Code = item.Code,
                LabelKey = item.LabelKey,
                CanReceiveProjectPackage = GenericProjectDepartmentCodes.Contains(item.Code),
            })
            .ToListAsync(ct);
        var users = await db.Users.AsNoTracking()
            .Where(item => item.IsActive && item.RoleEntity != null && item.RoleEntity.IsActive && item.RoleEntity.RoleGroupId != null)
            .OrderBy(item => item.FullName ?? item.Email)
            .Select(item => new DepartmentUserOptionResponse
            {
                UserId = item.Id,
                Name = item.FullName ?? item.Email,
                Email = item.Email,
                RoleGroupId = item.RoleEntity!.RoleGroupId!.Value,
                CanLead = item.RoleEntity.RolePermissions.Any(rolePermission =>
                    rolePermission.Permission.IsActive &&
                    rolePermission.Permission.Module == "work.packages" &&
                    rolePermission.Permission.Action == "assign"),
            }).ToListAsync(ct);
        var definitions = await db.KpiDefinitions.AsNoTracking()
            .Where(item => item.IsActive && item.MetricCode == "VerifiedWorkTaskCount")
            .OrderBy(item => item.RoleCode).ThenBy(item => item.Code)
            .Select(item => new KpiDefinitionOptionResponse { Id = item.Id, Code = item.Code, NameKey = item.NameKey })
            .ToListAsync(ct);
        return new DepartmentWorkPackageOptionsResponse
        {
            Departments = departments,
            Users = users,
            KpiDefinitions = definitions,
        };
    }

    public async Task<IReadOnlyList<DepartmentWorkPackageHistoryResponse>?> HistoryAsync(
        int id,
        int callerUserId,
        bool canViewAll,
        CancellationToken ct = default)
    {
        var package = await BaseQuery().SingleOrDefaultAsync(item => item.Id == id, ct);
        if (package is null || !await CanViewAsync(package, callerUserId, canViewAll, ct)) return null;
        return await db.DepartmentWorkPackageHistory.AsNoTracking()
            .Where(item => item.WorkPackageId == id)
            .OrderByDescending(item => item.ChangedAt).ThenByDescending(item => item.Id)
            .Select(item => new DepartmentWorkPackageHistoryResponse
            {
                Id = item.Id,
                EntityType = item.EntityType,
                EntityId = item.EntityId,
                Action = item.Action,
                SnapshotJson = item.SnapshotJson,
                ChangedAt = item.ChangedAt,
                ChangedByUserId = item.ChangedByUserId,
                ChangedByName = item.ChangedByUser.FullName ?? item.ChangedByUser.Email,
            }).ToListAsync(ct);
    }

    public async Task<DepartmentWorkPackageResponse> CreateAsync(
        CreateDepartmentWorkPackageRequest request,
        int callerUserId,
        CancellationToken ct = default)
    {
        if (!Enum.TryParse<WorkPackageKind>(request.Kind, true, out var kind))
            throw Error("Loại nhóm công việc không hợp lệ. Giá trị hợp lệ: Project hoặc Internal.");
        ValidateDates(request.PlannedStart, request.PlannedEnd);
        var admin = await IsAdministratorAsync(callerUserId, ct);
        await ValidateDepartmentLeadAsync(request.LeadUserId, request.DepartmentRoleGroupId, ct);

        if (kind == WorkPackageKind.Internal)
        {
            if (request.OperationalProjectId.HasValue || request.ContractId.HasValue || request.SourceOpportunityId.HasValue)
                throw Error("Công việc nội bộ không được gắn Dự án, Hợp đồng hoặc Cơ hội.");
            var callerGroup = await UserRoleGroupIdAsync(callerUserId, ct);
            if (!admin && (callerGroup != request.DepartmentRoleGroupId || request.LeadUserId != callerUserId))
                throw Error("Công việc nội bộ phải do người chủ trì của chính phòng ban tạo.");
        }
        else
        {
            if (request.ParticipantDepartmentRoleGroupIds.Count > 0)
                throw Error("Nhóm công việc dự án chỉ có một phòng ban nhận việc.");
            if (!request.OperationalProjectId.HasValue)
                throw Error("Nhóm công việc dự án phải chọn Dự án vận hành.");
            await ValidateProjectScopeAsync(request, callerUserId, admin, ct);
        }

        var now = DateTime.UtcNow;
        var entity = new DepartmentWorkPackage
        {
            Code = $"WP-{now:yyyy}-{Guid.NewGuid():N}"[..20].ToUpperInvariant(),
            Kind = kind,
            Name = request.Name.Trim(),
            Description = TrimOrNull(request.Description),
            OperationalProjectId = request.OperationalProjectId,
            ContractId = request.ContractId,
            SourceOpportunityId = request.SourceOpportunityId,
            DepartmentRoleGroupId = request.DepartmentRoleGroupId,
            LeadUserId = request.LeadUserId,
            PlannedStart = request.PlannedStart?.ToUniversalTime(),
            PlannedEnd = request.PlannedEnd?.ToUniversalTime(),
            CreatedAt = now,
            CreatedByUserId = callerUserId,
            UpdatedAt = now,
            UpdatedByUserId = callerUserId,
        };
        entity.ParticipantGroups = await BuildParticipantGroupsAsync(request, kind, ct);
        await using var transaction = await BeginTransactionAsync(ct);
        db.DepartmentWorkPackages.Add(entity);
        await db.SaveChangesAsync(ct);
        var response = await LoadAndMapAsync(entity.Id, callerUserId, admin, ct);
        await AddHistoryAsync(entity.Id, "Package", entity.Id, "Created", response, callerUserId, ct);
        await CommitAsync(transaction, ct);
        await NotifySafelyAsync(entity.LeadUserId, "work-package.assigned",
            new Dictionary<string, string> { ["packageName"] = entity.Name, ["packageCode"] = entity.Code },
            "DepartmentWorkPackage", entity.Id, $"/admin/work-packages/{entity.Id}", ct);
        return response;
    }

    private async Task<List<DepartmentWorkPackageParticipantGroup>> BuildParticipantGroupsAsync(
        CreateDepartmentWorkPackageRequest request,
        WorkPackageKind kind,
        CancellationToken ct)
    {
        var ids = kind == WorkPackageKind.Project
            ? new[] { request.DepartmentRoleGroupId }.ToHashSet()
            : request.ParticipantDepartmentRoleGroupIds
                .Append(request.DepartmentRoleGroupId)
                .Where(id => id > 0)
                .ToHashSet();
        var validIds = await db.RoleGroups.AsNoTracking()
            .Where(group => group.IsActive && ids.Contains(group.Id))
            .Select(group => group.Id)
            .ToListAsync(ct);
        if (validIds.Count != ids.Count)
            throw Error("Danh sách phòng ban tham gia có phòng ban không tồn tại hoặc đã ngừng hoạt động.");
        return validIds.Select(roleGroupId => new DepartmentWorkPackageParticipantGroup
        {
            RoleGroupId = roleGroupId,
        }).ToList();
    }

    public async Task<DepartmentWorkPackageResponse?> UpdateAsync(
        int id,
        UpdateDepartmentWorkPackageRequest request,
        int callerUserId,
        CancellationToken ct = default)
    {
        var entity = await db.DepartmentWorkPackages.Include(item => item.Tasks)
            .SingleOrDefaultAsync(item => item.Id == id, ct);
        if (entity is null) return null;
        var admin = await IsAdministratorAsync(callerUserId, ct);
        if (!await CanManagePackageAsync(entity, callerUserId, admin, ct)) throw ScopeError();
        if (!Enum.TryParse<WorkPackageStatus>(request.Status, true, out var status))
            throw Error("Trạng thái nhóm công việc không hợp lệ.");
        ValidatePackageTransition(entity, status);
        ValidateDates(request.PlannedStart, request.PlannedEnd);
        if (entity.Kind == WorkPackageKind.Internal && request.ContractId.HasValue)
            throw Error("Công việc nội bộ không được gắn Hợp đồng.");
        if (request.ContractId.HasValue)
            await ValidateContractAsync(request.ContractId.Value, entity.OperationalProjectId!.Value, ct);
        await using var transaction = await BeginTransactionAsync(ct);
        CrmConcurrency.Apply(db, entity, request.RowVersion);
        entity.Name = request.Name.Trim();
        entity.Description = TrimOrNull(request.Description);
        entity.ContractId = request.ContractId;
        entity.Status = status;
        entity.PlannedStart = request.PlannedStart?.ToUniversalTime();
        entity.PlannedEnd = request.PlannedEnd?.ToUniversalTime();
        entity.CompletedAt = status == WorkPackageStatus.Completed ? DateTime.UtcNow : null;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedByUserId = callerUserId;
        await CrmConcurrency.SaveChangesAsync(db, ct);
        var response = await LoadAndMapAsync(id, callerUserId, admin, ct);
        await AddHistoryAsync(id, "Package", id, "Updated", response, callerUserId, ct);
        await CommitAsync(transaction, ct);
        return response;
    }

    public async Task<DepartmentWorkTaskResponse> AddTaskAsync(
        int packageId,
        UpsertDepartmentWorkTaskRequest request,
        int callerUserId,
        CancellationToken ct = default)
    {
        var package = await db.DepartmentWorkPackages.SingleOrDefaultAsync(item => item.Id == packageId, ct)
            ?? throw Error("Không tìm thấy nhóm công việc.");
        await EnsureCanAssignAsync(package, callerUserId, ct);
        if (package.Status is WorkPackageStatus.Completed or WorkPackageStatus.Cancelled)
            throw Error("Nhóm công việc đã kết thúc và không thể thêm đầu việc.");
        await ValidateTaskAsync(package, null, request, ct);
        await using var transaction = await BeginTransactionAsync(ct);
        var now = DateTime.UtcNow;
        var task = new DepartmentWorkTask
        {
            WorkPackageId = packageId,
            WbsCode = request.WbsCode.Trim(),
            Title = request.Title.Trim(),
            Description = TrimOrNull(request.Description),
            Resources = TrimOrNull(request.Resources),
            DepartmentRoleGroupId = ResolveTaskDepartmentRoleGroupId(package, request),
            AssigneeUserId = request.AssigneeUserId,
            ApproverUserId = request.ApproverUserId,
            KpiDefinitionId = request.KpiDefinitionId,
            PlannedStart = request.PlannedStart.ToUniversalTime(),
            PlannedEnd = request.PlannedEnd.ToUniversalTime(),
            CreatedAt = now,
            CreatedByUserId = callerUserId,
            UpdatedAt = now,
            UpdatedByUserId = callerUserId,
        };
        SetTaskLinks(task, request);
        TouchPackage(package, callerUserId, now);
        db.DepartmentWorkTasks.Add(task);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        var response = await LoadTaskAsync(task.Id, callerUserId, await IsAdministratorAsync(callerUserId, ct), ct);
        await AddHistoryAsync(packageId, "Task", task.Id, "Created", response, callerUserId, ct);
        await CommitAsync(transaction, ct);
        await NotifyTaskAssignedAsync(package, task, ct);
        return response;
    }

    public async Task<DepartmentWorkTaskResponse?> UpdateTaskAsync(
        int packageId,
        int taskId,
        UpsertDepartmentWorkTaskRequest request,
        int callerUserId,
        CancellationToken ct = default)
    {
        var package = await db.DepartmentWorkPackages.SingleOrDefaultAsync(item => item.Id == packageId, ct)
            ?? throw Error("Không tìm thấy nhóm công việc.");
        await EnsureCanAssignAsync(package, callerUserId, ct);
        if (package.Status is WorkPackageStatus.Completed or WorkPackageStatus.Cancelled)
            throw Error("Nhóm công việc đã kết thúc và không thể chỉnh sửa đầu việc.");
        var task = await db.DepartmentWorkTasks.Include(item => item.CcUsers).Include(item => item.Predecessors)
            .SingleOrDefaultAsync(item => item.Id == taskId && item.WorkPackageId == packageId, ct);
        if (task is null) return null;
        if (task.Status is WorkTaskStatus.Completed or WorkTaskStatus.Verified or WorkTaskStatus.Cancelled)
            throw Error("Đầu việc đã kết thúc và không thể chỉnh sửa nội dung phân công.");
        await ValidateTaskAsync(package, taskId, request, ct);
        await using var transaction = await BeginTransactionAsync(ct);
        CrmConcurrency.Apply(db, task, request.RowVersion);
        var assigneeChanged = task.AssigneeUserId != request.AssigneeUserId;
        task.WbsCode = request.WbsCode.Trim();
        task.Title = request.Title.Trim();
        task.Description = TrimOrNull(request.Description);
        task.Resources = TrimOrNull(request.Resources);
        task.DepartmentRoleGroupId = ResolveTaskDepartmentRoleGroupId(package, request);
        task.AssigneeUserId = request.AssigneeUserId;
        task.ApproverUserId = request.ApproverUserId;
        task.KpiDefinitionId = request.KpiDefinitionId;
        task.PlannedStart = request.PlannedStart.ToUniversalTime();
        task.PlannedEnd = request.PlannedEnd.ToUniversalTime();
        task.UpdatedAt = DateTime.UtcNow;
        task.UpdatedByUserId = callerUserId;
        SyncTaskLinks(task, request);
        TouchPackage(package, callerUserId, DateTime.UtcNow);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        var response = await LoadTaskAsync(task.Id, callerUserId, await IsAdministratorAsync(callerUserId, ct), ct);
        await AddHistoryAsync(packageId, "Task", task.Id, "Updated", response, callerUserId, ct);
        await CommitAsync(transaction, ct);
        if (assigneeChanged) await NotifyTaskAssignedAsync(package, task, ct);
        return response;
    }

    public async Task<DepartmentWorkTaskResponse?> UpdateProgressAsync(
        int packageId,
        int taskId,
        UpdateDepartmentWorkTaskProgressRequest request,
        int callerUserId,
        CancellationToken ct = default)
    {
        var task = await db.DepartmentWorkTasks.Include(item => item.WorkPackage)
            .Include(item => item.Predecessors)
            .SingleOrDefaultAsync(item => item.Id == taskId && item.WorkPackageId == packageId, ct);
        if (task is null) return null;
        var admin = await IsAdministratorAsync(callerUserId, ct);
        if (!admin && task.AssigneeUserId != callerUserId)
            throw Error("Chỉ người phụ trách đầu việc mới được cập nhật tiến độ.");
        if (task.WorkPackage.Status != WorkPackageStatus.Active)
            throw Error("Nhóm công việc phải ở trạng thái Đang thực hiện trước khi cập nhật tiến độ.");
        if (!Enum.TryParse<WorkTaskStatus>(request.Status, true, out var status) ||
            status is WorkTaskStatus.Verified or WorkTaskStatus.Cancelled)
            throw Error("Người phụ trách chỉ được chuyển đầu việc sang Đang thực hiện hoặc Hoàn thành.");
        ValidateTaskProgressTransition(task, status, request.ProgressPercent);
        if (status is (WorkTaskStatus.InProgress or WorkTaskStatus.Completed) &&
            await db.DepartmentWorkTaskDependencies.AsNoTracking().AnyAsync(link =>
                link.WorkTaskId == task.Id &&
                link.PredecessorTask.Status != WorkTaskStatus.Completed &&
                link.PredecessorTask.Status != WorkTaskStatus.Verified, ct))
            throw Error("Chỉ được bắt đầu đầu việc sau khi mọi công tác tiên quyết đã hoàn thành.");
        await using var transaction = await BeginTransactionAsync(ct);
        CrmConcurrency.Apply(db, task, request.RowVersion);
        var now = DateTime.UtcNow;
        task.Status = status;
        task.ProgressPercent = request.ProgressPercent;
        task.ActualStart ??= status != WorkTaskStatus.Planned ? now : null;
        task.CompletedAt = status == WorkTaskStatus.Completed ? now : null;
        task.UpdatedAt = now;
        task.UpdatedByUserId = callerUserId;
        TouchPackage(task.WorkPackage, callerUserId, now);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        var response = await LoadTaskAsync(task.Id, callerUserId, admin, ct);
        await AddHistoryAsync(packageId, "Task", task.Id, "Progressed", response, callerUserId, ct);
        await CommitAsync(transaction, ct);
        if (status == WorkTaskStatus.Completed && task.KpiDefinitionId.HasValue)
        {
            var verifierId = task.ApproverUserId ?? task.WorkPackage.LeadUserId;
            await NotifySafelyAsync(verifierId, "work-task.awaiting-verification",
                new Dictionary<string, string> { ["taskTitle"] = task.Title, ["packageName"] = task.WorkPackage.Name },
                "DepartmentWorkTask", task.Id, $"/admin/work-packages/{packageId}?taskId={task.Id}", ct);
        }
        return response;
    }

    public async Task<DepartmentWorkTaskResponse?> VerifyTaskAsync(
        int packageId,
        int taskId,
        VerifyDepartmentWorkTaskRequest request,
        int callerUserId,
        CancellationToken ct = default)
    {
        var task = await db.DepartmentWorkTasks.Include(item => item.WorkPackage)
            .SingleOrDefaultAsync(item => item.Id == taskId && item.WorkPackageId == packageId, ct);
        if (task is null) return null;
        var admin = await IsAdministratorAsync(callerUserId, ct);
        var expectedVerifier = task.ApproverUserId ?? task.WorkPackage.LeadUserId;
        if (!admin && expectedVerifier != callerUserId)
            throw Error("Chỉ người duyệt được chỉ định hoặc người chủ trì phòng mới được xác nhận KPI.");
        if (task.AssigneeUserId == callerUserId)
            throw Error("Người thực hiện không được tự xác nhận bằng chứng KPI của chính mình.");
        if (task.WorkPackage.Status != WorkPackageStatus.Active)
            throw Error("Chỉ được xác nhận KPI khi nhóm công việc đang thực hiện.");
        if (!request.Confirm || !task.KpiDefinitionId.HasValue || task.Status != WorkTaskStatus.Completed)
            throw Error("Chỉ đầu việc KPI đã hoàn thành mới được xác nhận.");
        await using var transaction = await BeginTransactionAsync(ct);
        CrmConcurrency.Apply(db, task, request.RowVersion);
        task.Status = WorkTaskStatus.Verified;
        task.ProgressPercent = 100;
        task.VerifiedAt = DateTime.UtcNow;
        task.VerifiedByUserId = callerUserId;
        task.UpdatedAt = DateTime.UtcNow;
        task.UpdatedByUserId = callerUserId;
        TouchPackage(task.WorkPackage, callerUserId, DateTime.UtcNow);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        var response = await LoadTaskAsync(task.Id, callerUserId, admin, ct);
        await AddHistoryAsync(packageId, "Task", task.Id, "Verified", response, callerUserId, ct);
        await CommitAsync(transaction, ct);
        await NotifySafelyAsync(task.AssigneeUserId, "work-task.verified",
            new Dictionary<string, string> { ["taskTitle"] = task.Title, ["packageName"] = task.WorkPackage.Name },
            "DepartmentWorkTask", task.Id, $"/admin/work-packages/{packageId}?taskId={task.Id}", ct);
        return response;
    }

    private IQueryable<DepartmentWorkPackage> BaseQuery() => db.DepartmentWorkPackages.AsNoTracking()
        .Include(item => item.OperationalProject)
        .Include(item => item.Contract)
        .Include(item => item.SourceOpportunity)
        .Include(item => item.DepartmentRoleGroup)
        .Include(item => item.ParticipantGroups).ThenInclude(participant => participant.RoleGroup)
        .Include(item => item.LeadUser)
        .Include(item => item.Tasks).ThenInclude(task => task.AssigneeUser)
        .Include(item => item.Tasks).ThenInclude(task => task.ApproverUser)
        .Include(item => item.Tasks).ThenInclude(task => task.KpiDefinition)
        .Include(item => item.Tasks).ThenInclude(task => task.DepartmentRoleGroup)
        .Include(item => item.Tasks).ThenInclude(task => task.CcUsers)
        .Include(item => item.Tasks).ThenInclude(task => task.Predecessors);

    private async Task ValidateProjectScopeAsync(
        CreateDepartmentWorkPackageRequest request,
        int callerUserId,
        bool admin,
        CancellationToken ct)
    {
        var project = await db.OperationalProjects.AsNoTracking()
            .Where(item => item.Id == request.OperationalProjectId)
            .Select(item => new { item.Id, item.ProjectManagerUserId })
            .SingleOrDefaultAsync(ct) ?? throw Error("Dự án vận hành không tồn tại.");
        var receivingGroupCode = await db.RoleGroups.AsNoTracking()
            .Where(group => group.Id == request.DepartmentRoleGroupId && group.IsActive)
            .Select(group => group.Code)
            .SingleOrDefaultAsync(ct);
        if (receivingGroupCode is null)
            throw Error("Phòng ban nhận việc không tồn tại hoặc đã ngừng hoạt động.");
        if (!GenericProjectDepartmentCodes.Contains(receivingGroupCode, StringComparer.OrdinalIgnoreCase))
            throw Error("Phòng ban này đã có không gian công việc chuyên môn. Hãy tạo công việc trong module tương ứng để tránh dữ liệu trùng.");
        var isOpportunityOwner = false;
        if (request.SourceOpportunityId.HasValue)
        {
            var opportunity = await db.Opportunities.AsNoTracking()
                .Where(item => item.Id == request.SourceOpportunityId)
                .Select(item => new { item.OperationalProjectId, item.OwnerUserId, item.Stage })
                .SingleOrDefaultAsync(ct);
            if (opportunity is null || opportunity.OperationalProjectId != project.Id ||
                opportunity.Stage == OpportunityStage.Lost)
                throw Error("Cơ hội phải đang hoạt động và gắn đúng Dự án vận hành.");
            isOpportunityOwner = opportunity.OwnerUserId == callerUserId;
            if (!isOpportunityOwner && !admin)
                throw Error("Cơ hội không thuộc phạm vi của người dùng hoặc không gắn đúng Dự án.");
        }
        if (!admin && project.ProjectManagerUserId != callerUserId && !isOpportunityOwner)
            throw Error("Chỉ PM Dự án hoặc Kinh doanh phụ trách Cơ hội được giao việc cho phòng ban.");
        if (request.ContractId.HasValue) await ValidateContractAsync(request.ContractId.Value, project.Id, ct);
    }

    private async Task ValidateContractAsync(int contractId, int projectId, CancellationToken ct)
    {
        if (!await db.Contracts.AsNoTracking().AnyAsync(item => item.Id == contractId && item.OperationalProjectId == projectId, ct))
            throw Error("Hợp đồng phải thuộc cùng Dự án vận hành với nhóm công việc.");
    }

    private async Task ValidateTaskAsync(
        DepartmentWorkPackage package,
        int? taskId,
        UpsertDepartmentWorkTaskRequest request,
        CancellationToken ct)
    {
        var taskGroupId = ResolveTaskDepartmentRoleGroupId(package, request);
        if (package.Kind == WorkPackageKind.Internal)
        {
            var participant = await db.DepartmentWorkPackageParticipantGroups.AsNoTracking().AnyAsync(item =>
                item.WorkPackageId == package.Id && item.RoleGroupId == taskGroupId, ct);
            if (!participant)
                throw Error("Phòng ban của đầu việc phải thuộc danh sách phòng ban tham gia công việc nội bộ.");
        }
        if (request.PlannedEnd < request.PlannedStart)
            throw Error("Ngày kết thúc đầu việc không được trước ngày bắt đầu.");
        if (request.ApproverUserId == request.AssigneeUserId)
            throw Error("Người duyệt phải khác người phụ trách đầu việc.");
        await ValidateDepartmentUserAsync(request.AssigneeUserId, taskGroupId, "Người phụ trách", ct);
        if (request.ApproverUserId.HasValue)
            await ValidateDepartmentUserAsync(request.ApproverUserId.Value, taskGroupId, "Người duyệt", ct);
        foreach (var ccUserId in request.CcUserIds.Distinct())
            await EnsureActiveUserAsync(ccUserId, "Người CC", ct);
        if (await db.DepartmentWorkTasks.AsNoTracking().AnyAsync(item =>
            item.WorkPackageId == package.Id && item.WbsCode == request.WbsCode.Trim() && item.Id != taskId, ct))
            throw Error("Mã WBS đã tồn tại trong nhóm công việc.");
        if (request.KpiDefinitionId.HasValue)
        {
            var definitionRole = await db.KpiDefinitions.AsNoTracking()
                .Where(item => item.Id == request.KpiDefinitionId && item.IsActive &&
                    item.MetricCode == "VerifiedWorkTaskCount")
                .Select(item => item.RoleCode)
                .SingleOrDefaultAsync(ct);
            var assigneeRole = await db.Users.AsNoTracking().Where(item => item.Id == request.AssigneeUserId)
                .Select(item => item.RoleEntity != null ? item.RoleEntity.Code : item.Role.ToString())
                .SingleAsync(ct);
            if (definitionRole is null || definitionRole != KpiService.KpiPosition(assigneeRole))
                throw Error("KPI phải đang hoạt động, hỗ trợ đầu việc và phù hợp với vị trí của người phụ trách.");
        }
        var predecessorIds = request.PredecessorTaskIds.Distinct().ToList();
        if (taskId.HasValue && predecessorIds.Contains(taskId.Value))
            throw Error("Đầu việc không thể phụ thuộc chính nó.");
        var validCount = await db.DepartmentWorkTasks.AsNoTracking().CountAsync(item =>
            item.WorkPackageId == package.Id && predecessorIds.Contains(item.Id), ct);
        if (validCount != predecessorIds.Count)
            throw Error("Tất cả công tác trước phải thuộc cùng nhóm công việc.");
        if (taskId.HasValue && await CreatesDependencyCycleAsync(taskId.Value, predecessorIds, ct))
            throw Error("Quan hệ công tác trước tạo thành vòng lặp không hợp lệ.");
    }

    private static int ResolveTaskDepartmentRoleGroupId(
        DepartmentWorkPackage package,
        UpsertDepartmentWorkTaskRequest request)
    {
        if (package.Kind == WorkPackageKind.Project) return package.DepartmentRoleGroupId;
        if (!request.DepartmentRoleGroupId.HasValue || request.DepartmentRoleGroupId.Value <= 0)
            throw Error("Đầu việc nội bộ phải chọn phòng ban phụ trách.");
        return request.DepartmentRoleGroupId.Value;
    }

    private static void TouchPackage(DepartmentWorkPackage package, int callerUserId, DateTime now)
    {
        package.UpdatedAt = now;
        package.UpdatedByUserId = callerUserId;
    }

    private async Task<bool> CreatesDependencyCycleAsync(int taskId, IReadOnlyCollection<int> predecessors, CancellationToken ct)
    {
        var edges = await db.DepartmentWorkTaskDependencies.AsNoTracking()
            .Where(item => item.WorkTaskId != taskId)
            .Select(item => new { item.WorkTaskId, item.PredecessorTaskId }).ToListAsync(ct);
        var map = edges.GroupBy(item => item.WorkTaskId)
            .ToDictionary(group => group.Key, group => group.Select(item => item.PredecessorTaskId).ToList());
        map[taskId] = predecessors.ToList();
        var stack = new HashSet<int>();
        var visited = new HashSet<int>();
        bool Visit(int current)
        {
            if (!stack.Add(current)) return true;
            if (visited.Contains(current)) { stack.Remove(current); return false; }
            if (map.TryGetValue(current, out var next) && next.Any(Visit)) return true;
            stack.Remove(current);
            visited.Add(current);
            return false;
        }
        return Visit(taskId);
    }

    private static void SetTaskLinks(DepartmentWorkTask task, UpsertDepartmentWorkTaskRequest request)
    {
        task.CcUsers = request.CcUserIds.Distinct().Select(userId => new DepartmentWorkTaskCc { UserId = userId }).ToList();
        task.Predecessors = request.PredecessorTaskIds.Distinct()
            .Select(predecessorTaskId => new DepartmentWorkTaskDependency { PredecessorTaskId = predecessorTaskId }).ToList();
    }

    private void SyncTaskLinks(DepartmentWorkTask task, UpsertDepartmentWorkTaskRequest request)
    {
        var requestedCc = request.CcUserIds.Distinct().ToHashSet();
        db.DepartmentWorkTaskCcUsers.RemoveRange(task.CcUsers.Where(item => !requestedCc.Contains(item.UserId)));
        foreach (var userId in requestedCc.Except(task.CcUsers.Select(item => item.UserId)))
            task.CcUsers.Add(new DepartmentWorkTaskCc { WorkTaskId = task.Id, UserId = userId });

        var requestedPredecessors = request.PredecessorTaskIds.Distinct().ToHashSet();
        db.DepartmentWorkTaskDependencies.RemoveRange(task.Predecessors
            .Where(item => !requestedPredecessors.Contains(item.PredecessorTaskId)));
        foreach (var predecessorId in requestedPredecessors.Except(task.Predecessors.Select(item => item.PredecessorTaskId)))
            task.Predecessors.Add(new DepartmentWorkTaskDependency
            {
                WorkTaskId = task.Id,
                PredecessorTaskId = predecessorId,
            });
    }

    private static void ValidateTaskProgressTransition(DepartmentWorkTask task, WorkTaskStatus status, int progress)
    {
        if (task.Status is WorkTaskStatus.Completed or WorkTaskStatus.Verified or WorkTaskStatus.Cancelled)
            throw Error("Đầu việc đã kết thúc và không thể cập nhật tiến độ.");
        if (status == WorkTaskStatus.Planned && progress != 0)
            throw Error("Đầu việc chưa bắt đầu phải có tiến độ 0%.");
        if (status == WorkTaskStatus.InProgress && (progress < 1 || progress > 99))
            throw Error("Đầu việc đang thực hiện phải có tiến độ từ 1% đến 99%.");
        if (status == WorkTaskStatus.Completed && progress != 100)
            throw Error("Đầu việc hoàn thành phải có tiến độ 100%.");
        if (progress < task.ProgressPercent)
            throw Error("Tiến độ đầu việc không được giảm.");
    }

    private static void ValidatePackageTransition(DepartmentWorkPackage package, WorkPackageStatus status)
    {
        if (status == package.Status) return;
        if (package.Status is WorkPackageStatus.Completed or WorkPackageStatus.Cancelled)
            throw Error("Nhóm công việc đã kết thúc và không thể chuyển trạng thái.");
        var transitionAllowed = package.Status switch
        {
            WorkPackageStatus.Planned => status is WorkPackageStatus.Active or WorkPackageStatus.Cancelled,
            WorkPackageStatus.Active => status is WorkPackageStatus.Completed or WorkPackageStatus.Cancelled,
            _ => false,
        };
        if (!transitionAllowed)
            throw Error($"Không thể chuyển nhóm công việc từ {package.Status} sang {status}.");
        if (status == WorkPackageStatus.Completed)
        {
            if (package.Tasks.Count == 0) throw Error("Nhóm công việc phải có ít nhất một đầu việc trước khi hoàn thành.");
            if (package.Tasks.Any(task => task.Status is WorkTaskStatus.Planned or WorkTaskStatus.InProgress ||
                task.KpiDefinitionId.HasValue && task.Status != WorkTaskStatus.Verified))
                throw Error("Mọi đầu việc phải hoàn tất; đầu việc KPI phải được xác nhận trước khi đóng nhóm.");
        }
        if (status == WorkPackageStatus.Cancelled && package.Tasks.Any(task => task.Status == WorkTaskStatus.Verified))
            throw Error("Không thể hủy nhóm công việc đã có bằng chứng KPI được xác nhận.");
    }

    private async Task<bool> CanViewAsync(DepartmentWorkPackage item, int callerUserId, bool canViewAll, CancellationToken ct)
    {
        if (canViewAll || await IsAdministratorAsync(callerUserId, ct)) return true;
        if (item.CreatedByUserId == callerUserId || item.LeadUserId == callerUserId ||
            item.Tasks.Any(task => task.AssigneeUserId == callerUserId || task.ApproverUserId == callerUserId ||
                task.CcUsers.Any(cc => cc.UserId == callerUserId))) return true;
        return item.OperationalProjectId.HasValue &&
            await projectAccess.CanViewOperationalProjectAsync(callerUserId, item.OperationalProjectId.Value, ct);
    }

    private async Task<bool> CanManagePackageAsync(DepartmentWorkPackage item, int callerUserId, bool admin, CancellationToken ct)
    {
        if (admin) return true;
        if (item.Kind == WorkPackageKind.Internal) return item.CreatedByUserId == callerUserId && item.LeadUserId == callerUserId;
        if (item.OperationalProjectId.HasValue && await db.OperationalProjects.AsNoTracking().AnyAsync(project =>
            project.Id == item.OperationalProjectId && project.ProjectManagerUserId == callerUserId, ct)) return true;
        return item.SourceOpportunityId.HasValue && await db.Opportunities.AsNoTracking().AnyAsync(opportunity =>
            opportunity.Id == item.SourceOpportunityId && opportunity.OwnerUserId == callerUserId, ct);
    }

    private async Task EnsureCanAssignAsync(DepartmentWorkPackage package, int callerUserId, CancellationToken ct)
    {
        if (await IsAdministratorAsync(callerUserId, ct)) return;
        if (package.LeadUserId != callerUserId)
            throw ScopeError();
    }

    private async Task ValidateDepartmentUserAsync(int userId, int roleGroupId, string field, CancellationToken ct)
    {
        var group = await UserRoleGroupIdAsync(userId, ct);
        if (group != roleGroupId) throw Error($"{field} phải là người dùng đang hoạt động thuộc đúng phòng ban nhận việc.");
    }

    private async Task ValidateDepartmentLeadAsync(int userId, int roleGroupId, CancellationToken ct)
    {
        var canLead = await db.Users.AsNoTracking().AnyAsync(item =>
            item.Id == userId && item.IsActive && item.RoleEntity != null && item.RoleEntity.IsActive &&
            item.RoleEntity.RoleGroupId == roleGroupId && item.RoleEntity.RolePermissions.Any(rolePermission =>
                rolePermission.Permission.IsActive &&
                rolePermission.Permission.Module == "work.packages" &&
                rolePermission.Permission.Action == "assign"), ct);
        if (!canLead)
            throw Error("Người chủ trì phải là người dùng đang hoạt động thuộc phòng ban nhận việc và có quyền phân công.");
    }

    private async Task EnsureActiveUserAsync(int userId, string field, CancellationToken ct)
    {
        if (!await db.Users.AsNoTracking().AnyAsync(item => item.Id == userId && item.IsActive, ct))
            throw Error($"{field} không tồn tại hoặc đã ngừng hoạt động.");
    }

    private async Task<int?> UserRoleGroupIdAsync(int userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().Where(item => item.Id == userId && item.IsActive)
            .Select(item => new { item.Role, GroupId = item.RoleEntity == null ? null : item.RoleEntity.RoleGroupId })
            .SingleOrDefaultAsync(ct);
        if (user is null) return null;
        if (user.GroupId.HasValue) return user.GroupId;
        var code = user.Role.ToString();
        return await db.Roles.AsNoTracking().Where(item => item.Code == code && item.IsActive)
            .Select(item => item.RoleGroupId).SingleOrDefaultAsync(ct);
    }

    private async Task<bool> IsAdministratorAsync(int userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().Where(item => item.Id == userId && item.IsActive)
            .Select(item => new
            {
                item.Role,
                RoleCode = item.RoleEntity == null ? null : item.RoleEntity.Code,
                IsSystem = item.RoleEntity != null && item.RoleEntity.IsSystem && item.RoleEntity.IsActive
            })
            .SingleOrDefaultAsync(ct);
        if (user is null) return false;
        return user.IsSystem && (user.RoleCode == "ADMIN" || user.RoleCode == "SUPER_ADMIN") ||
            user.Role is UserRole.ADMIN or UserRole.SUPER_ADMIN;
    }

    private async Task<DepartmentWorkPackageResponse> LoadAndMapAsync(int id, int callerUserId, bool admin, CancellationToken ct)
        => MapPackage(await BaseQuery().SingleAsync(item => item.Id == id, ct), callerUserId, admin);

    private async Task<DepartmentWorkTaskResponse> LoadTaskAsync(int taskId, int callerUserId, bool admin, CancellationToken ct)
    {
        var task = await db.DepartmentWorkTasks.AsNoTracking()
            .Include(item => item.WorkPackage).Include(item => item.AssigneeUser).Include(item => item.ApproverUser)
            .Include(item => item.DepartmentRoleGroup).Include(item => item.KpiDefinition)
            .Include(item => item.CcUsers).Include(item => item.Predecessors)
            .SingleAsync(item => item.Id == taskId, ct);
        return MapTask(task, callerUserId, admin, task.WorkPackage.LeadUserId);
    }

    private static DepartmentWorkPackageResponse MapPackage(DepartmentWorkPackage item, int callerUserId, bool admin)
    {
        var activeTasks = item.Tasks.Where(task => task.Status != WorkTaskStatus.Cancelled).ToList();
        return new DepartmentWorkPackageResponse
        {
            Id = item.Id,
            Code = item.Code,
            Kind = item.Kind.ToString(),
            Name = item.Name,
            Description = item.Description,
            OperationalProjectId = item.OperationalProjectId,
            OperationalProjectCode = item.OperationalProject?.Code,
            OperationalProjectName = item.OperationalProject?.Name,
            ContractId = item.ContractId,
            ContractNumber = item.Contract?.ContractNumber,
            SourceOpportunityId = item.SourceOpportunityId,
            SourceOpportunityName = item.SourceOpportunity?.Name,
            DepartmentRoleGroupId = item.DepartmentRoleGroupId,
            DepartmentCode = item.DepartmentRoleGroup.Code,
            DepartmentLabelKey = item.DepartmentRoleGroup.LabelKey,
            ParticipantDepartments = item.ParticipantGroups
                .OrderBy(participant => participant.RoleGroup.SortOrder)
                .Select(participant => new DepartmentOptionResponse
                {
                    Id = participant.RoleGroupId,
                    Code = participant.RoleGroup.Code,
                    LabelKey = participant.RoleGroup.LabelKey,
                    CanReceiveProjectPackage = GenericProjectDepartmentCodes.Contains(participant.RoleGroup.Code),
                }).ToList(),
            LeadUserId = item.LeadUserId,
            LeadUserName = item.LeadUser.FullName ?? item.LeadUser.Email,
            Status = item.Status.ToString(),
            PlannedStart = item.PlannedStart,
            PlannedEnd = item.PlannedEnd,
            CompletedAt = item.CompletedAt,
            ProgressPercent = activeTasks.Count == 0 ? 0 : (int)Math.Round(activeTasks.Average(task => task.ProgressPercent)),
            CanManagePackage = admin ||
                (item.Kind == WorkPackageKind.Internal && item.CreatedByUserId == callerUserId &&
                    item.LeadUserId == callerUserId) ||
                item.OperationalProject?.ProjectManagerUserId == callerUserId ||
                item.SourceOpportunity?.OwnerUserId == callerUserId,
            CanAssignTasks = admin || item.LeadUserId == callerUserId,
            RowVersion = CrmConcurrency.Encode(item.RowVersion),
            Tasks = item.Tasks.OrderBy(task => task.WbsCode).ThenBy(task => task.Id)
                .Select(task => MapTask(task, callerUserId, admin, item.LeadUserId)).ToList(),
        };
    }

    private static DepartmentWorkTaskResponse MapTask(
        DepartmentWorkTask task,
        int callerUserId,
        bool admin,
        int packageLeadUserId)
    {
        var verifier = task.ApproverUserId ?? packageLeadUserId;
        return new DepartmentWorkTaskResponse
        {
            Id = task.Id,
            WorkPackageId = task.WorkPackageId,
            WbsCode = task.WbsCode,
            Title = task.Title,
            Description = task.Description,
            Resources = task.Resources,
            DepartmentRoleGroupId = task.DepartmentRoleGroupId,
            DepartmentCode = task.DepartmentRoleGroup.Code,
            DepartmentLabelKey = task.DepartmentRoleGroup.LabelKey,
            AssigneeUserId = task.AssigneeUserId,
            AssigneeName = task.AssigneeUser.FullName ?? task.AssigneeUser.Email,
            ApproverUserId = task.ApproverUserId,
            ApproverName = task.ApproverUser == null ? null : task.ApproverUser.FullName ?? task.ApproverUser.Email,
            KpiDefinitionId = task.KpiDefinitionId,
            KpiDefinitionCode = task.KpiDefinition?.Code,
            Status = task.Status.ToString(),
            ProgressPercent = task.ProgressPercent,
            PlannedStart = task.PlannedStart,
            PlannedEnd = task.PlannedEnd,
            ActualStart = task.ActualStart,
            CompletedAt = task.CompletedAt,
            VerifiedAt = task.VerifiedAt,
            VerifiedByUserId = task.VerifiedByUserId,
            CcUserIds = task.CcUsers.Select(item => item.UserId).ToList(),
            PredecessorTaskIds = task.Predecessors.Select(item => item.PredecessorTaskId).ToList(),
            CanUpdateProgress = admin || task.AssigneeUserId == callerUserId,
            CanVerify = task.KpiDefinitionId.HasValue && task.Status == WorkTaskStatus.Completed &&
                task.AssigneeUserId != callerUserId && (admin || verifier == callerUserId),
            RowVersion = CrmConcurrency.Encode(task.RowVersion),
        };
    }

    private async Task AddHistoryAsync(int packageId, string entityType, int entityId, string action, object snapshot, int callerUserId, CancellationToken ct)
    {
        db.DepartmentWorkPackageHistory.Add(new DepartmentWorkPackageHistory
        {
            WorkPackageId = packageId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            SnapshotJson = JsonSerializer.Serialize(snapshot),
            ChangedAt = DateTime.UtcNow,
            ChangedByUserId = callerUserId,
        });
        await db.SaveChangesAsync(ct);
    }

    private async Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken ct) =>
        db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;

    private static async Task CommitAsync(IDbContextTransaction? transaction, CancellationToken ct)
    {
        if (transaction is not null) await transaction.CommitAsync(ct);
    }

    private async Task NotifyTaskAssignedAsync(DepartmentWorkPackage package, DepartmentWorkTask task, CancellationToken ct)
    {
        var data = new Dictionary<string, string> { ["taskTitle"] = task.Title, ["packageName"] = package.Name };
        var link = $"/admin/work-packages/{package.Id}?taskId={task.Id}";
        await NotifySafelyAsync(task.AssigneeUserId, "work-task.assigned", data, "DepartmentWorkTask", task.Id, link, ct);
        foreach (var userId in task.CcUsers.Select(item => item.UserId).Distinct().Where(id => id != task.AssigneeUserId))
            await NotifySafelyAsync(userId, "work-task.cc", data, "DepartmentWorkTask", task.Id, link, ct);
    }

    private async Task NotifySafelyAsync(int userId, string template, IDictionary<string, string> data,
        string entityType, int entityId, string link, CancellationToken ct)
    {
        try { await notifications.NotifyFromTemplateAsync(userId, template, data, entityType, entityId, link); }
        catch (Exception exception) { logger.LogWarning(exception, "Work notification {Template} failed for user {UserId}", template, userId); }
    }

    private static void ValidateDates(DateTime? start, DateTime? end)
    {
        if (start.HasValue && end.HasValue && end < start)
            throw Error("Ngày kết thúc không được trước ngày bắt đầu.");
    }

    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static DepartmentWorkPackageOperationException Error(string message) => new(message);
    private static DepartmentWorkPackageScopeException ScopeError() =>
        new("Không tìm thấy nhóm công việc hoặc bạn không có quyền thao tác.");
}
