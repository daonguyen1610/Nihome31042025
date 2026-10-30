using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public sealed class ProjectTeamService(
    AppDbContext db,
    IProjectAccessService access) : IProjectTeamService
{
    private static readonly IReadOnlyList<ProjectRoleDefinitionResponse> RoleDefinitions =
    [
        Role(ProjectTeamRoleCode.ProjectManager, "A", true, false),
        Role(ProjectTeamRoleCode.DesignLead, "A/R", true, true),
        Role(ProjectTeamRoleCode.Architect, "R"),
        Role(ProjectTeamRoleCode.StructuralEngineer, "R"),
        Role(ProjectTeamRoleCode.MepEngineer, "R"),
        Role(ProjectTeamRoleCode.InteriorDesigner, "R"),
        Role(ProjectTeamRoleCode.LegalOfficer, "C"),
        Role(ProjectTeamRoleCode.SiteEngineer, "C/I"),
        Role(ProjectTeamRoleCode.QuantitySurveyor, "C"),
        Role(ProjectTeamRoleCode.Observer, "I"),
    ];

    public async Task<OperationalProjectTeamResponse?> GetAsync(
        int projectId,
        int callerUserId,
        CancellationToken ct = default,
        int? designProjectId = null)
    {
        if (!await CanViewTeamAsync(projectId, designProjectId, callerUserId, ct)) return null;
        var members = await MemberQuery(projectId).Where(member => member.DesignProjectId == designProjectId).ToListAsync(ct);
        var assignments = await AssignmentQuery(projectId).Where(item => item.AssigneeMember.DesignProjectId == designProjectId).ToListAsync(ct);
        var disciplineOptions = await db.MasterDataOptions.AsNoTracking()
            .Where(option => option.Category == ProjectTeamCatalog.DisciplineCategory && option.IsActive)
            .OrderBy(option => option.SortOrder)
            .ThenBy(option => option.Code)
            .Select(option => option.Code)
            .ToListAsync(ct);
        return new OperationalProjectTeamResponse
        {
            OperationalProjectId = projectId,
            CanManage = await CanManageTeamAsync(projectId, designProjectId, callerUserId, ct),
            RoleDefinitions = RoleDefinitions.Select(CloneRoleDefinition).ToList(),
            ModuleOptions = [.. ProjectTeamCatalog.ModuleCodes],
            DisciplineOptions = disciplineOptions,
            Members = members.Select(MapMember).ToList(),
            Assignments = assignments.Select(MapAssignment).ToList(),
        };
    }

    public async Task<IReadOnlyList<OperationalProjectTeamHistoryResponse>?> GetHistoryAsync(
        int projectId,
        int callerUserId,
        CancellationToken ct = default,
        int? designProjectId = null)
    {
        if (!await CanViewTeamAsync(projectId, designProjectId, callerUserId, ct)) return null;
        var teamHistory = await db.OperationalProjectTeamHistory.AsNoTracking()
            .Where(item => item.OperationalProjectId == projectId &&
                (item.EntityType == "Member" && db.OperationalProjectMembers.Any(member =>
                    member.Id == item.EntityId && member.DesignProjectId == designProjectId) ||
                 item.EntityType == "Assignment" && db.OperationalProjectAssignments.Any(assignment =>
                    assignment.Id == item.EntityId && assignment.AssigneeMember.DesignProjectId == designProjectId) ||
                 !designProjectId.HasValue && item.EntityType != "Member" && item.EntityType != "Assignment"))
            .OrderByDescending(item => item.ChangedAt)
            .ThenByDescending(item => item.Id)
            .Select(item => new OperationalProjectTeamHistoryResponse
            {
                Id = item.Id,
                EntityType = item.EntityType,
                EntityId = item.EntityId,
                Action = item.Action,
                SnapshotJson = item.SnapshotJson,
                ChangedAt = item.ChangedAt,
                ChangedByUserId = item.ChangedByUserId,
                ChangedByName = item.ChangedByUser.FullName ?? item.ChangedByUser.Email,
            })
            .ToListAsync(ct);
        var codeHistory = await (
            from change in db.OperationalProjectCodeChanges.AsNoTracking()
            where change.OperationalProjectId == projectId && designProjectId == null
            join user in db.Users.AsNoTracking() on change.ChangedByUserId equals user.Id into actors
            from actor in actors.DefaultIfEmpty()
            select new OperationalProjectTeamHistoryResponse
            {
                Id = change.Id,
                EntityType = "ProjectCode",
                EntityId = projectId,
                Action = "Renamed",
                ChangedAt = change.ChangedAt,
                ChangedByUserId = change.ChangedByUserId,
                ChangedByName = actor == null ? change.ChangedByUserId.ToString() : actor.FullName ?? actor.Email,
                OldCode = change.OldCode,
                NewCode = change.NewCode,
                Reason = change.Reason,
            }).ToListAsync(ct);
        return teamHistory.Concat(codeHistory)
            .OrderByDescending(item => item.ChangedAt)
            .ThenByDescending(item => item.Id)
            .ThenBy(item => item.EntityType, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<IReadOnlyList<ProjectMemberCandidateResponse>?> GetCandidatesAsync(
        int projectId,
        int callerUserId,
        CancellationToken ct = default,
        int? designProjectId = null)
    {
        if (!await CanManageTeamAsync(projectId, designProjectId, callerUserId, ct)) return null;
        return await db.Users.AsNoTracking()
            .Where(user => user.IsActive)
            .OrderBy(user => user.FullName ?? user.Email)
            .Select(user => new ProjectMemberCandidateResponse
            {
                UserId = user.Id,
                Name = user.FullName ?? user.Email,
                Email = user.Email,
                RoleCode = user.RoleEntity != null ? user.RoleEntity.Code : user.Role.ToString(),
                RoleName = user.RoleEntity != null ? user.RoleEntity.Name : user.Role.ToString(),
                RoleLabelKey = user.RoleEntity != null ? user.RoleEntity.LabelKey : null,
                DepartmentId = user.RoleEntity != null && user.RoleEntity.RoleGroup != null && user.RoleEntity.RoleGroup.IsActive ? user.RoleEntity.RoleGroupId : null,
                DepartmentLabelKey = user.RoleEntity != null && user.RoleEntity.RoleGroup != null && user.RoleEntity.RoleGroup.IsActive ? user.RoleEntity.RoleGroup.LabelKey : null,
            })
            .ToListAsync(ct);
    }

    public async Task<OperationalProjectMemberResponse> AddMemberAsync(
        int projectId,
        UpsertOperationalProjectMemberRequest request,
        int callerUserId,
        CancellationToken ct = default)
    {
        await EnsureCanManageAsync(projectId, callerUserId, ct, request.DesignProjectId);
        await ValidateMemberAsync(projectId, null, request, ct);
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(ct)
            : null;
        var now = DateTime.UtcNow;
        var member = new OperationalProjectMember
        {
            DesignProjectId = request.DesignProjectId,
            OperationalProjectId = projectId,
            UserId = request.UserId,
            Position = request.Position.Trim(),
            ReportsToMemberId = request.ReportsToMemberId,
            StartedAt = request.StartedAt.ToUniversalTime(),
            EndedAt = request.EndedAt?.ToUniversalTime(),
            CreatedAt = now,
            UpdatedAt = now,
            CreatedByUserId = callerUserId,
            UpdatedByUserId = callerUserId,
        };
        member.Roles.AddRange(ParseRoles(request.Roles, member.StartedAt, member.EndedAt));
        db.OperationalProjectMembers.Add(member);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw new ProjectTeamOperationException(
                "Người dùng đã có phân công đang hiệu lực trong Dự án. Hãy cập nhật phân công hiện có.");
        }
        var response = MapMember(await MemberQuery(projectId).SingleAsync(item => item.Id == member.Id, ct));
        await AddHistoryAsync(projectId, "Member", member.Id, "Created", response, callerUserId, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return response;
    }

    public async Task<OperationalProjectMemberResponse?> UpdateMemberAsync(
        int projectId,
        int memberId,
        UpsertOperationalProjectMemberRequest request,
        int callerUserId,
        CancellationToken ct = default)
    {
        var member = await db.OperationalProjectMembers
            .Include(item => item.Roles)
            .SingleOrDefaultAsync(item => item.Id == memberId && item.OperationalProjectId == projectId, ct);
        if (member is null) return null;
        if (member.DesignProjectId != request.DesignProjectId)
            throw new ProjectTeamOperationException("Không thể chuyển thành viên sang luồng Thiết kế khác. Hãy tạo phân công riêng tại luồng cần dùng.");
        await EnsureCanManageAsync(projectId, callerUserId, ct, member.DesignProjectId);
        if (request.UserId != member.UserId)
            throw new ProjectTeamOperationException(
                "Không thể thay đổi người dùng của một thành viên Dự án. Hãy kết thúc phân công cũ và tạo thành viên mới.");
        await ValidateMemberAsync(projectId, memberId, request, ct);
        if (request.EndedAt.HasValue && !member.EndedAt.HasValue)
            await EnsureMemberCanEndAsync(projectId, memberId, ct);
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(ct)
            : null;
        CrmConcurrency.Apply(db, member, request.RowVersion);
        var now = DateTime.UtcNow;
        var requestedRoles = ParseRoles(request.Roles, now, request.EndedAt);
        foreach (var role in member.Roles.Where(role => role.EndedAt == null))
        {
            var matchingRole = requestedRoles.FirstOrDefault(candidate =>
                candidate.RoleCode == role.RoleCode && candidate.Scope == role.Scope &&
                string.Equals(candidate.ScopeValue, role.ScopeValue, StringComparison.OrdinalIgnoreCase));
            if (request.EndedAt.HasValue || matchingRole is null) role.EndedAt = now;
            else requestedRoles.Remove(matchingRole);
        }
        member.UserId = request.UserId;
        member.Position = request.Position.Trim();
        member.ReportsToMemberId = request.ReportsToMemberId;
        member.StartedAt = request.StartedAt.ToUniversalTime();
        member.EndedAt = request.EndedAt?.ToUniversalTime();
        member.UpdatedAt = now;
        member.UpdatedByUserId = callerUserId;
        if (!request.EndedAt.HasValue) member.Roles.AddRange(requestedRoles);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        var response = MapMember(await MemberQuery(projectId).SingleAsync(item => item.Id == member.Id, ct));
        await AddHistoryAsync(projectId, "Member", member.Id,
            member.EndedAt.HasValue ? "Ended" : "Updated", response, callerUserId, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return response;
    }

    public async Task<OperationalProjectAssignmentResponse> AddAssignmentAsync(
        int projectId,
        UpsertOperationalProjectAssignmentRequest request,
        int callerUserId,
        CancellationToken ct = default)
    {
        if (!await CanManageTeamAsync(projectId, request.DesignProjectId, callerUserId, ct))
            throw new ProjectTeamOperationException("Bạn không có quyền quản lý đội ngũ của luồng này.");
        var status = await ValidateAssignmentAsync(projectId, null, request, ct);
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(ct)
            : null;
        var now = DateTime.UtcNow;
        var assignment = new OperationalProjectAssignment
        {
            OperationalProjectId = projectId,
            WorkKey = request.WorkKey.Trim(),
            Title = request.Title.Trim(),
            Module = request.Module.Trim(),
            Discipline = TrimOrNull(request.Discipline),
            ParallelGroup = TrimOrNull(request.ParallelGroup),
            AssigneeMemberId = request.AssigneeMemberId,
            ManagerMemberId = request.ManagerMemberId,
            Status = status,
            PlannedStart = request.PlannedStart?.ToUniversalTime(),
            PlannedEnd = request.PlannedEnd?.ToUniversalTime(),
            CompletedAt = status == ProjectAssignmentStatus.Completed ? now : null,
            Note = TrimOrNull(request.Note),
            CreatedAt = now,
            UpdatedAt = now,
            CreatedByUserId = callerUserId,
            UpdatedByUserId = callerUserId,
        };
        db.OperationalProjectAssignments.Add(assignment);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw new ProjectTeamOperationException(
                "Người phụ trách đã được gán cho công việc này. Hãy cập nhật phân công hiện có.");
        }
        var response = MapAssignment(await AssignmentQuery(projectId)
            .SingleAsync(item => item.Id == assignment.Id, ct));
        await AddHistoryAsync(projectId, "Assignment", assignment.Id, "Created", response, callerUserId, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return response;
    }

    public async Task<OperationalProjectAssignmentResponse?> UpdateAssignmentAsync(
        int projectId,
        int assignmentId,
        UpsertOperationalProjectAssignmentRequest request,
        int callerUserId,
        CancellationToken ct = default)
    {
        if (!await CanManageTeamAsync(projectId, request.DesignProjectId, callerUserId, ct)) return null;
        var assignment = await db.OperationalProjectAssignments.SingleOrDefaultAsync(item =>
            item.Id == assignmentId && item.OperationalProjectId == projectId &&
            item.AssigneeMember.DesignProjectId == request.DesignProjectId, ct);
        if (assignment is null) return null;
        if (assignment.Status is ProjectAssignmentStatus.Completed or ProjectAssignmentStatus.Cancelled)
        {
            throw new ProjectTeamOperationException(
                "Phân công đã hoàn tất hoặc đã huỷ là dữ liệu lịch sử và không thể chỉnh sửa.");
        }
        if (!string.Equals(assignment.WorkKey, request.WorkKey.Trim(), StringComparison.Ordinal) ||
            assignment.AssigneeMemberId != request.AssigneeMemberId)
        {
            throw new ProjectTeamOperationException(
                "Mã công việc và người phụ trách tạo thành định danh KPI và không thể thay đổi. Hãy tạo phân công mới.");
        }
        var status = await ValidateAssignmentAsync(projectId, assignmentId, request, ct);
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(ct)
            : null;
        CrmConcurrency.Apply(db, assignment, request.RowVersion);
        var now = DateTime.UtcNow;
        assignment.Title = request.Title.Trim();
        assignment.Module = request.Module.Trim();
        assignment.Discipline = TrimOrNull(request.Discipline);
        assignment.ParallelGroup = TrimOrNull(request.ParallelGroup);
        assignment.ManagerMemberId = request.ManagerMemberId;
        assignment.Status = status;
        assignment.PlannedStart = request.PlannedStart?.ToUniversalTime();
        assignment.PlannedEnd = request.PlannedEnd?.ToUniversalTime();
        assignment.CompletedAt = status == ProjectAssignmentStatus.Completed ? now : null;
        assignment.Note = TrimOrNull(request.Note);
        assignment.UpdatedAt = now;
        assignment.UpdatedByUserId = callerUserId;
        await CrmConcurrency.SaveChangesAsync(db, ct);
        var response = MapAssignment(await AssignmentQuery(projectId)
            .SingleAsync(item => item.Id == assignment.Id, ct));
        await AddHistoryAsync(projectId, "Assignment", assignment.Id, "Updated", response, callerUserId, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return response;
    }

    private async Task ValidateMemberAsync(
        int projectId,
        int? memberId,
        UpsertOperationalProjectMemberRequest request,
        CancellationToken ct)
    {
        if (!await db.Users.AsNoTracking().AnyAsync(user => user.Id == request.UserId && user.IsActive, ct))
            throw new ProjectTeamOperationException("Người dùng không tồn tại hoặc đã ngừng hoạt động.");
        if (request.EndedAt.HasValue && request.EndedAt.Value < request.StartedAt)
            throw new ProjectTeamOperationException("Ngày kết thúc phân công không được trước ngày bắt đầu.");
        await NormalizeRoleScopeValuesAsync(request.Roles, ct);
        var roles = ParseRoles(request.Roles, request.StartedAt, request.EndedAt);
        if (request.DesignProjectId.HasValue && roles.Any(role =>
            role.Scope == ProjectRoleScope.Module && role.ScopeValue != "Design"))
            throw new ProjectTeamOperationException("Vai trò của đội ngũ Thiết kế phải thuộc phân hệ Design hoặc bộ môn của luồng hiện tại.");
        if (!request.DesignProjectId.HasValue && roles.Any(role => role.RoleCode == ProjectTeamRoleCode.ProjectManager))
        {
            var primaryManagerUserId = await db.OperationalProjects.AsNoTracking()
                .Where(project => project.Id == projectId)
                .Select(project => project.ProjectManagerUserId)
                .SingleAsync(ct);
            if (primaryManagerUserId != request.UserId)
            {
                throw new ProjectTeamOperationException(
                    "Chỉ PM chính của Dự án được giữ vai trò Accountable Project Manager.");
            }
        }
        if (memberId.HasValue && request.ReportsToMemberId == memberId)
            throw new ProjectTeamOperationException("Thành viên không thể tự quản lý chính mình.");
        if (request.ReportsToMemberId.HasValue)
        {
            var manager = await db.OperationalProjectMembers.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == request.ReportsToMemberId.Value, ct);
            if (manager is null || manager.OperationalProjectId != projectId || manager.EndedAt.HasValue ||
                manager.DesignProjectId != request.DesignProjectId)
                throw new ProjectTeamOperationException("Người quản lý phải là thành viên đang hiệu lực của cùng Dự án.");
            if (memberId.HasValue) await EnsureNoReportingCycleAsync(memberId.Value, manager.Id, ct);
        }
        if (await db.OperationalProjectMembers.AsNoTracking().AnyAsync(item =>
            item.OperationalProjectId == projectId && item.UserId == request.UserId &&
            item.DesignProjectId == request.DesignProjectId &&
            item.EndedAt == null && item.Id != memberId, ct))
        {
            throw new ProjectTeamOperationException(
                "Người dùng đã có phân công đang hiệu lực trong Dự án. Hãy cập nhật phân công hiện có.");
        }
    }

    private async Task<ProjectAssignmentStatus> ValidateAssignmentAsync(
        int projectId,
        int? assignmentId,
        UpsertOperationalProjectAssignmentRequest request,
        CancellationToken ct)
    {
        request.Module = NormalizeModule(request.Module);
        if (request.DesignProjectId.HasValue && request.Module != "Design")
            throw new ProjectTeamOperationException("Phân công của đội Design phải thuộc phân hệ Design.");
        request.Discipline = await NormalizeDisciplineAsync(request.Discipline, ct);
        if (!Enum.TryParse<ProjectAssignmentStatus>(request.Status, true, out var status))
            throw new ProjectTeamOperationException($"Trạng thái phân công '{request.Status}' không hợp lệ.");
        if (request.PlannedStart.HasValue && request.PlannedEnd.HasValue &&
            request.PlannedEnd.Value < request.PlannedStart.Value)
            throw new ProjectTeamOperationException("Ngày kết thúc dự kiến không được trước ngày bắt đầu.");
        var memberIds = new[] { request.AssigneeMemberId, request.ManagerMemberId ?? 0 }
            .Where(id => id > 0)
            .Distinct()
            .ToList();
        var activeMemberIds = await db.OperationalProjectMembers.AsNoTracking()
            .Where(item => item.OperationalProjectId == projectId &&
                item.DesignProjectId == request.DesignProjectId &&
                item.EndedAt == null && memberIds.Contains(item.Id))
            .Select(item => item.Id)
            .ToListAsync(ct);
        if (!activeMemberIds.Contains(request.AssigneeMemberId))
            throw new ProjectTeamOperationException("Người phụ trách phải là thành viên đang hiệu lực của Dự án.");
        if (request.ManagerMemberId.HasValue && !activeMemberIds.Contains(request.ManagerMemberId.Value))
            throw new ProjectTeamOperationException("Người quản lý công việc phải là thành viên đang hiệu lực của Dự án.");
        if (request.ManagerMemberId == request.AssigneeMemberId)
            throw new ProjectTeamOperationException("Người quản lý công việc phải khác người phụ trách.");
        if (await db.OperationalProjectAssignments.AsNoTracking().AnyAsync(item =>
            item.OperationalProjectId == projectId &&
            item.WorkKey == request.WorkKey.Trim() &&
            item.AssigneeMemberId == request.AssigneeMemberId &&
            item.Id != assignmentId, ct))
            throw new ProjectTeamOperationException("Người phụ trách đã được gán cho công việc này.");
        return status;
    }

    private async Task NormalizeRoleScopeValuesAsync(
        IEnumerable<ProjectMemberRoleRequest> roles,
        CancellationToken ct)
    {
        var disciplineCodes = await db.MasterDataOptions.AsNoTracking()
            .Where(option => option.Category == ProjectTeamCatalog.DisciplineCategory && option.IsActive)
            .Select(option => option.Code)
            .ToListAsync(ct);
        foreach (var role in roles)
        {
            if (!Enum.TryParse<ProjectRoleScope>(role.Scope, true, out var scope)) continue;
            if (scope == ProjectRoleScope.Module)
            {
                role.ScopeValue = NormalizeModule(role.ScopeValue);
            }
            else if (scope == ProjectRoleScope.Discipline)
            {
                role.ScopeValue = FindCanonical(
                    role.ScopeValue,
                    disciplineCodes,
                    "Bộ môn không hợp lệ. Ví dụ hợp lệ: architecture.");
            }
        }
    }

    private async Task<string?> NormalizeDisciplineAsync(string? value, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var disciplineCodes = await db.MasterDataOptions.AsNoTracking()
            .Where(option => option.Category == ProjectTeamCatalog.DisciplineCategory && option.IsActive)
            .Select(option => option.Code)
            .ToListAsync(ct);
        return FindCanonical(
            value,
            disciplineCodes,
            "Bộ môn của phân công không hợp lệ. Ví dụ hợp lệ: architecture.");
    }

    private static string NormalizeModule(string? value) => FindCanonical(
        value,
        ProjectTeamCatalog.ModuleCodes,
        "Module không hợp lệ. Ví dụ hợp lệ: Design.");

    private static string FindCanonical(
        string? value,
        IEnumerable<string> options,
        string errorMessage)
    {
        var canonical = options.FirstOrDefault(option =>
            string.Equals(option, value?.Trim(), StringComparison.OrdinalIgnoreCase));
        return canonical ?? throw new ProjectTeamOperationException(errorMessage);
    }

    private async Task EnsureMemberCanEndAsync(int projectId, int memberId, CancellationToken ct)
    {
        var hasActiveAssignments = await db.OperationalProjectAssignments.AsNoTracking().AnyAsync(item =>
            item.OperationalProjectId == projectId &&
            (item.AssigneeMemberId == memberId || item.ManagerMemberId == memberId) &&
            (item.Status == ProjectAssignmentStatus.Planned || item.Status == ProjectAssignmentStatus.InProgress), ct);
        if (hasActiveAssignments)
            throw new ProjectTeamOperationException(
                "Không thể kết thúc thành viên khi còn công việc đang hoạt động với vai trò phụ trách hoặc quản lý.");

        var hasActiveReports = await db.OperationalProjectMembers.AsNoTracking().AnyAsync(item =>
            item.OperationalProjectId == projectId && item.ReportsToMemberId == memberId && item.EndedAt == null, ct);
        if (hasActiveReports)
            throw new ProjectTeamOperationException(
                "Không thể kết thúc thành viên khi còn thành viên đang báo cáo trực tiếp.");
    }

    private async Task EnsureNoReportingCycleAsync(int memberId, int managerId, CancellationToken ct)
    {
        var current = managerId;
        var visited = new HashSet<int>();
        while (visited.Add(current))
        {
            if (current == memberId)
                throw new ProjectTeamOperationException("Quan hệ quản lý tạo thành vòng lặp không hợp lệ.");
            var next = await db.OperationalProjectMembers.AsNoTracking()
                .Where(item => item.Id == current)
                .Select(item => item.ReportsToMemberId)
                .SingleOrDefaultAsync(ct);
            if (!next.HasValue) return;
            current = next.Value;
        }
        throw new ProjectTeamOperationException("Quan hệ quản lý tạo thành vòng lặp không hợp lệ.");
    }

    private async Task<bool> CanViewTeamAsync(int projectId, int? designProjectId, int userId, CancellationToken ct)
    {
        if (!designProjectId.HasValue) return await access.CanViewOperationalProjectAsync(userId, projectId, ct);
        return await db.DesignProjects.AnyAsync(design => design.Id == designProjectId &&
            design.OperationalProjectId == projectId, ct) && await access.CanViewDesignProjectAsync(userId, designProjectId.Value, ct);
    }

    private async Task<bool> CanManageTeamAsync(int projectId, int? designProjectId, int userId, CancellationToken ct)
    {
        if (!designProjectId.HasValue) return await access.CanManageTeamAsync(userId, projectId, ct);
        return await db.DesignProjects.AnyAsync(design => design.Id == designProjectId &&
            design.OperationalProjectId == projectId, ct) &&
            (await access.CanManageTeamAsync(userId, projectId, ct) ||
             await access.CanManageDesignProjectAsync(userId, designProjectId.Value, ct));
    }

    private async Task EnsureCanManageAsync(int projectId, int callerUserId, CancellationToken ct, int? designProjectId = null)
    {
        if (!await CanManageTeamAsync(projectId, designProjectId, callerUserId, ct))
            throw new ProjectTeamOperationException("Không tìm thấy Dự án hoặc bạn không có quyền quản lý đội Dự án.");
    }

    private async Task AddHistoryAsync(
        int projectId,
        string entityType,
        int entityId,
        string action,
        object snapshot,
        int callerUserId,
        CancellationToken ct)
    {
        db.OperationalProjectTeamHistory.Add(new OperationalProjectTeamHistory
        {
            OperationalProjectId = projectId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            SnapshotJson = JsonSerializer.Serialize(snapshot),
            ChangedAt = DateTime.UtcNow,
            ChangedByUserId = callerUserId,
        });
        await db.SaveChangesAsync(ct);
    }

    private IQueryable<OperationalProjectMember> MemberQuery(int projectId) =>
        db.OperationalProjectMembers.AsNoTracking()
            .Include(item => item.User)
                .ThenInclude(user => user.RoleEntity).ThenInclude(role => role!.RoleGroup)
            .Include(item => item.ReportsToMember)!
                .ThenInclude(item => item!.User)
            .Include(item => item.Roles)
            .Where(item => item.OperationalProjectId == projectId)
            .OrderBy(item => item.EndedAt.HasValue)
            .ThenBy(item => item.User.FullName ?? item.User.Email);

    private IQueryable<OperationalProjectAssignment> AssignmentQuery(int projectId) =>
        db.OperationalProjectAssignments.AsNoTracking()
            .Include(item => item.AssigneeMember).ThenInclude(item => item.User)
            .Include(item => item.ManagerMember)!.ThenInclude(item => item!.User)
            .Where(item => item.OperationalProjectId == projectId)
            .OrderBy(item => item.Status)
            .ThenBy(item => item.PlannedStart)
            .ThenBy(item => item.Id);

    private static List<OperationalProjectMemberRole> ParseRoles(
        IEnumerable<ProjectMemberRoleRequest> requests,
        DateTime startedAt,
        DateTime? endedAt)
    {
        var roles = new List<OperationalProjectMemberRole>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var request in requests)
        {
            if (!Enum.TryParse<ProjectTeamRoleCode>(request.RoleCode, true, out var roleCode))
                throw new ProjectTeamOperationException($"Vai trò Dự án '{request.RoleCode}' không hợp lệ.");
            if (!Enum.TryParse<ProjectRoleScope>(request.Scope, true, out var scope))
                throw new ProjectTeamOperationException($"Phạm vi vai trò '{request.Scope}' không hợp lệ.");
            var scopeValue = TrimOrNull(request.ScopeValue);
            if (scope == ProjectRoleScope.Project && scopeValue is not null)
                throw new ProjectTeamOperationException("Vai trò phạm vi toàn Dự án không được có giá trị phạm vi.");
            if (scope != ProjectRoleScope.Project && scopeValue is null)
                throw new ProjectTeamOperationException("Vai trò theo Module hoặc Bộ môn phải chọn giá trị phạm vi.");
            var key = $"{roleCode}|{scope}|{scopeValue}";
            if (!keys.Add(key))
                throw new ProjectTeamOperationException("Danh sách vai trò có mục bị trùng.");
            roles.Add(new OperationalProjectMemberRole
            {
                RoleCode = roleCode,
                Scope = scope,
                ScopeValue = scopeValue,
                Source = LegacyProjectTeamSyncService.ManualSource,
                StartedAt = startedAt.ToUniversalTime(),
                EndedAt = endedAt?.ToUniversalTime(),
            });
        }
        if (roles.Count == 0) throw new ProjectTeamOperationException("Thành viên phải có ít nhất một vai trò Dự án.");
        return roles;
    }

    private static OperationalProjectMemberResponse MapMember(OperationalProjectMember member) => new()
    {
        DesignProjectId = member.DesignProjectId,
        Id = member.Id,
        UserId = member.UserId,
        UserName = member.User.FullName ?? member.User.Email,
        RoleCode = member.User.RoleEntity?.Code ?? member.User.Role.ToString(),
        RoleName = member.User.RoleEntity?.Name ?? member.User.Role.ToString(),
        RoleLabelKey = member.User.RoleEntity?.LabelKey,
        DepartmentId = member.User.RoleEntity?.RoleGroup is { IsActive: true } group ? group.Id : null,
        DepartmentLabelKey = member.User.RoleEntity?.RoleGroup is { IsActive: true } department ? department.LabelKey : null,
        Email = member.User.Email,
        Position = member.Position,
        ReportsToMemberId = member.ReportsToMemberId,
        ReportsToName = member.ReportsToMember?.User.FullName ?? member.ReportsToMember?.User.Email,
        StartedAt = member.StartedAt,
        EndedAt = member.EndedAt,
        IsActive = !member.EndedAt.HasValue,
        IsUserActive = member.User.IsActive,
        Source = member.Source,
        SourceReference = member.SourceReference,
        Roles = member.Roles.Where(role => role.EndedAt == null).Select(role => new ProjectMemberRoleResponse
        {
            RoleCode = role.RoleCode.ToString(),
            Scope = role.Scope.ToString(),
            ScopeValue = role.ScopeValue,
            StartedAt = role.StartedAt,
            EndedAt = role.EndedAt,
        }).ToList(),
        RowVersion = Convert.ToBase64String(member.RowVersion),
    };

    private static OperationalProjectAssignmentResponse MapAssignment(OperationalProjectAssignment assignment) => new()
    {
        Id = assignment.Id,
        WorkKey = assignment.WorkKey,
        KpiIdentity = $"{assignment.OperationalProjectId}:{assignment.WorkKey}:{assignment.AssigneeMemberId}",
        Title = assignment.Title,
        Module = assignment.Module,
        Discipline = assignment.Discipline,
        ParallelGroup = assignment.ParallelGroup,
        AssigneeMemberId = assignment.AssigneeMemberId,
        AssigneeName = assignment.AssigneeMember.User.FullName ?? assignment.AssigneeMember.User.Email,
        ManagerMemberId = assignment.ManagerMemberId,
        ManagerName = assignment.ManagerMember?.User.FullName ?? assignment.ManagerMember?.User.Email,
        Status = assignment.Status.ToString(),
        PlannedStart = assignment.PlannedStart,
        PlannedEnd = assignment.PlannedEnd,
        CompletedAt = assignment.CompletedAt,
        Note = assignment.Note,
        RowVersion = Convert.ToBase64String(assignment.RowVersion),
    };

    private static ProjectRoleDefinitionResponse Role(
        ProjectTeamRoleCode code,
        string raci,
        bool canManageTeam = false,
        bool canApproveDesign = false) => new()
        {
            Code = code.ToString(),
            Raci = raci,
            CanManageTeam = canManageTeam,
            CanApproveDesign = canApproveDesign,
            AllowedScopes = ["Project", "Module", "Discipline"],
        };

    private static ProjectRoleDefinitionResponse CloneRoleDefinition(ProjectRoleDefinitionResponse value) => new()
    {
        Code = value.Code,
        Raci = value.Raci,
        CanManageTeam = value.CanManageTeam,
        CanApproveDesign = value.CanApproveDesign,
        AllowedScopes = [.. value.AllowedScopes],
    };

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
