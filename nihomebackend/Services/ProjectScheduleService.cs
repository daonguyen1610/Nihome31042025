using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Models.Rbac;

namespace NihomeBackend.Services;

public sealed class ProjectScheduleOperationException(string message, int statusCode = 400) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public interface IProjectScheduleService
{
    Task<ProjectScheduleResponse?> GetAsync(int projectId, int callerUserId, CancellationToken ct);
    Task<ProjectScheduleResponse> CreateRevisionAsync(int projectId, SaveProjectScheduleRevisionRequest request, int callerUserId, CancellationToken ct);
    Task<ProjectScheduleResponse?> UpdateRevisionAsync(int projectId, int revisionId, SaveProjectScheduleRevisionRequest request, int callerUserId, CancellationToken ct);
    Task<ProjectScheduleResponse?> SubmitAsync(int projectId, int revisionId, SubmitProjectScheduleRevisionRequest request, int callerUserId, CancellationToken ct);
    Task<ProjectScheduleResponse?> ReviewAsync(int projectId, int revisionId, ReviewProjectScheduleRevisionRequest request, int callerUserId, CancellationToken ct);
}

public sealed class ProjectScheduleService(
    AppDbContext db,
    IProjectAccessService access,
    IPermissionService permissions,
    INotificationService notifications,
    ILogger<ProjectScheduleService> logger) : IProjectScheduleService
{
    private const int NotificationChangeSummaryMaxLength = 900;
    private const int EmailChangeSummaryMaxLength = 6000;

    public async Task<ProjectScheduleResponse?> GetAsync(int projectId, int callerUserId, CancellationToken ct)
    {
        if (!await CanReadScheduleAsync(projectId, callerUserId, ct)) return null;
        var canManage = await access.CanViewOperationalProjectAsync(callerUserId, projectId, ct) &&
            await permissions.HasAsync(callerUserId, "operations.projects.manage", ct);
        return await BuildResponseAsync(projectId, ct, callerUserId, canManage);
    }

    public async Task<ProjectScheduleResponse> CreateRevisionAsync(
        int projectId, SaveProjectScheduleRevisionRequest request, int callerUserId, CancellationToken ct)
    {
        var project = await db.OperationalProjects.SingleOrDefaultAsync(item => item.Id == projectId, ct)
            ?? throw new ProjectScheduleOperationException("Dự án không tồn tại.", 404);
        if (!await access.CanViewOperationalProjectAsync(callerUserId, projectId, ct))
            throw new ProjectScheduleOperationException("Không tìm thấy dự án.", 404);
        if (await db.ProjectScheduleRevisions.AnyAsync(item => item.OperationalProjectId == projectId &&
                (item.Status == ProjectScheduleRevisionStatus.Draft ||
                 item.Status == ProjectScheduleRevisionStatus.PendingApproval), ct))
            throw new ProjectScheduleOperationException("Dự án đã có một revision đang soạn hoặc chờ duyệt.", 409);

        var latestVersion = await db.ProjectScheduleRevisions.AsNoTracking().Where(item => item.OperationalProjectId == projectId)
            .Select(item => (int?)item.Version).MaxAsync(ct) ?? 0;
        await using var transaction = await BeginTransactionAsync(null, ct);
        var revision = new ProjectScheduleRevision
        {
            OperationalProjectId = projectId,
            Version = latestVersion + 1,
            Status = ProjectScheduleRevisionStatus.Draft,
            ChangeReason = request.ChangeReason?.Trim(),
            CreatedByUserId = callerUserId,
        };
        db.ProjectScheduleRevisions.Add(revision);
        await db.SaveChangesAsync(ct);
        await ApplyItemsAsync(projectId, revision, request.Items, ct);
        await db.SaveChangesAsync(ct);
        await CommitAsync(transaction, ct);
        logger.LogInformation("Project schedule revision {RevisionId} created for project {ProjectId} by {UserId}.", revision.Id, projectId, callerUserId);
        return await BuildResponseAsync(projectId, ct);
    }

    public async Task<ProjectScheduleResponse?> UpdateRevisionAsync(
        int projectId, int revisionId, SaveProjectScheduleRevisionRequest request, int callerUserId, CancellationToken ct)
    {
        var revision = await db.ProjectScheduleRevisions.Include(item => item.Items)
            .SingleOrDefaultAsync(item => item.Id == revisionId && item.OperationalProjectId == projectId, ct);
        if (revision is null) return null;
        if (revision.Status is not (ProjectScheduleRevisionStatus.Draft or ProjectScheduleRevisionStatus.Rejected))
            throw new ProjectScheduleOperationException("Chỉ revision nháp hoặc bị trả lại mới được chỉnh sửa.", 409);
        if (!await access.CanViewOperationalProjectAsync(callerUserId, projectId, ct)) return null;
        if (await db.ProjectScheduleRevisions.AnyAsync(item => item.OperationalProjectId == projectId &&
                item.Id != revisionId && (item.Status == ProjectScheduleRevisionStatus.Draft ||
                    item.Status == ProjectScheduleRevisionStatus.PendingApproval), ct))
            throw new ProjectScheduleOperationException("Dự án đang có revision nháp hoặc chờ duyệt khác.", 409);

        await using var transaction = await BeginTransactionAsync(null, ct);
        revision.Status = ProjectScheduleRevisionStatus.Draft;
        revision.ChangeReason = request.ChangeReason?.Trim();
        revision.ApproverUserId = null;
        revision.SubmittedAt = null;
        revision.SubmittedByUserId = null;
        revision.ReviewedAt = null;
        revision.ReviewedByUserId = null;
        revision.ReviewComment = null;
        db.ProjectScheduleItems.RemoveRange(revision.Items);
        // Commit the removals before inserting the replacement set so the unique
        // (RevisionId, NodeId) index never sees the old and new rows at once.
        await db.SaveChangesAsync(ct);
        revision.Items.Clear();
        await ApplyItemsAsync(projectId, revision, request.Items, ct);
        await db.SaveChangesAsync(ct);
        await CommitAsync(transaction, ct);
        return await BuildResponseAsync(projectId, ct, callerUserId);
    }

    public async Task<ProjectScheduleResponse?> SubmitAsync(
        int projectId, int revisionId, SubmitProjectScheduleRevisionRequest request, int callerUserId, CancellationToken ct)
    {
        var revision = await db.ProjectScheduleRevisions.Include(item => item.Items)
            .SingleOrDefaultAsync(item => item.Id == revisionId && item.OperationalProjectId == projectId, ct);
        if (revision is null) return null;
        var reassigning = revision.Status == ProjectScheduleRevisionStatus.PendingApproval;
        if ((!reassigning && revision.Status != ProjectScheduleRevisionStatus.Draft) || revision.Items.Count == 0)
            throw new ProjectScheduleOperationException("Chỉ revision nháp có ít nhất một mốc mới được gửi duyệt.", 409);
        if (!await access.CanViewOperationalProjectAsync(callerUserId, projectId, ct)) return null;
        if (revision.Version > 1 && string.IsNullOrWhiteSpace(revision.ChangeReason))
            throw new ProjectScheduleOperationException("Revision mới cần ghi rõ yêu cầu khách hàng đã thay đổi.");
        if (await db.ProjectScheduleRevisions.AnyAsync(item => item.OperationalProjectId == projectId &&
                item.Id != revisionId && item.Status == ProjectScheduleRevisionStatus.PendingApproval, ct))
            throw new ProjectScheduleOperationException("Dự án đang có revision khác chờ duyệt.", 409);
        if (!await IsEligibleApproverAsync(projectId, request.ApproverUserId, ct))
            throw new ProjectScheduleOperationException("Người duyệt phải là PM dự án sau khi hợp đồng Upstream ký, hoặc người quản lý CRM trước khi ký.");
        if (!MatchesRowVersion(revision, request.RowVersion))
            throw new ProjectScheduleOperationException("Revision đã được cập nhật. Tải lại trước khi gửi duyệt.", 409);
        revision.ApproverUserId = request.ApproverUserId;
        revision.SubmittedByUserId = callerUserId;
        revision.SubmittedAt = DateTime.UtcNow;
        revision.Status = ProjectScheduleRevisionStatus.PendingApproval;
        await db.SaveChangesAsync(ct);
        try
        {
            await notifications.NotifyFromTemplateAsync(request.ApproverUserId, "project.schedule.submitted",
                new Dictionary<string, string> { ["projectId"] = projectId.ToString(), ["version"] = revision.Version.ToString() },
                "ProjectScheduleRevision", revision.Id, $"/admin/operational-projects/{projectId}?tab=schedule");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception,
                "Failed to create in-app notification for schedule revision {RevisionId} and approver {ApproverId}.",
                revision.Id, request.ApproverUserId);
        }
        await QueueEmailAsync(projectId, revision, request.ApproverUserId, "Yêu cầu duyệt tiến độ tổng thể",
            await GetChangeSummaryAsync(projectId, revision, ct), ct);
        logger.LogInformation("Project schedule revision {RevisionId} submitted for project {ProjectId}; approver {ApproverId}.", revision.Id, projectId, request.ApproverUserId);
        return await BuildResponseAsync(projectId, ct);
    }

    public async Task<ProjectScheduleResponse?> ReviewAsync(
        int projectId, int revisionId, ReviewProjectScheduleRevisionRequest request, int callerUserId, CancellationToken ct)
    {
        var revision = await db.ProjectScheduleRevisions.Include(item => item.Items)
            .SingleOrDefaultAsync(item => item.Id == revisionId && item.OperationalProjectId == projectId, ct);
        if (revision is null) return null;
        if (revision.Status != ProjectScheduleRevisionStatus.PendingApproval || revision.ApproverUserId != callerUserId)
            throw new ProjectScheduleOperationException("Chỉ người duyệt được chỉ định mới có thể quyết định revision đang chờ.", 403);
        if (!await IsEligibleApproverAsync(projectId, callerUserId, ct))
            throw new ProjectScheduleOperationException("Người duyệt không còn phù hợp với giai đoạn hợp đồng hiện tại của dự án.", 403);
        if (!MatchesRowVersion(revision, request.RowVersion))
            throw new ProjectScheduleOperationException("Revision đã được cập nhật. Tải lại trước khi duyệt.", 409);
        if (request.Decision == "Reject" && string.IsNullOrWhiteSpace(request.Comment))
            throw new ProjectScheduleOperationException("Cần nêu lý do khi trả lại revision.");

        await using var transaction = await BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (request.Decision == "Approve")
        {
            if (await db.ProjectScheduleRevisions.AnyAsync(item => item.OperationalProjectId == projectId &&
                    item.Id != revision.Id && item.Status == ProjectScheduleRevisionStatus.Published &&
                    item.Version > revision.Version, ct))
                throw new ProjectScheduleOperationException(
                    "Đã có phiên bản tiến độ mới hơn được phát hành. Hãy trả lại revision này thay vì phát hành.", 409);
            await ValidateLinkedTasksForPublicationAsync(projectId, revision, ct);
        }

        revision.ReviewedAt = DateTime.UtcNow;
        revision.ReviewedByUserId = callerUserId;
        revision.ReviewComment = request.Comment?.Trim();
        if (request.Decision == "Reject") revision.Status = ProjectScheduleRevisionStatus.Rejected;
        else
        {
            revision.Status = ProjectScheduleRevisionStatus.Published;
            revision.PublishedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        await CommitAsync(transaction, ct);
        if (revision.Status == ProjectScheduleRevisionStatus.Published)
            await NotifyAffectedDepartmentsAsync(projectId, revision, ct);
        logger.LogInformation("Project schedule revision {RevisionId} reviewed as {Decision} by assigned approver {UserId}.", revision.Id, request.Decision, callerUserId);
        var canManage = await access.CanViewOperationalProjectAsync(callerUserId, projectId, ct) &&
            await permissions.HasAsync(callerUserId, "operations.projects.manage", ct);
        return await BuildResponseAsync(projectId, ct, callerUserId, canManage);
    }

    private async Task<bool> IsEligibleApproverAsync(int projectId, int userId, CancellationToken ct)
    {
        var project = await db.OperationalProjects.AsNoTracking().Where(item => item.Id == projectId)
            .Select(item => new
            {
                item.ProjectManagerUserId,
                HasSignedContract = item.Contracts.Any(contract => contract.Direction == ContractDirection.Upstream &&
                    contract.Status != ContractStatus.Draft && contract.Status != ContractStatus.Cancelled)
            })
            .SingleAsync(ct);
        if (project.HasSignedContract)
            return project.ProjectManagerUserId == userId && await db.Users.AsNoTracking().AnyAsync(user => user.Id == userId && user.IsActive && user.Email != null, ct);
        return await db.Users.AsNoTracking().AnyAsync(user => user.Id == userId && user.IsActive && user.Email != null && user.RoleEntity != null &&
            user.RoleEntity.IsActive && user.RoleEntity.Code == RbacRoleCodes.SalesManager &&
            user.RoleEntity.RoleGroup != null && user.RoleEntity.RoleGroup.Code == RoleGroupCodes.Crm &&
            user.RoleEntity.RolePermissions.Any(grant => grant.Permission.IsActive &&
                grant.Permission.Module == "crm.opportunities" && grant.Permission.Action == "manage"), ct);
    }

    private async Task ApplyItemsAsync(int projectId, ProjectScheduleRevision revision,
        IReadOnlyCollection<SaveProjectScheduleItemRequest> requested, CancellationToken ct)
    {
        var ids = requested.Select(item => item.NodeId ?? Guid.NewGuid()).ToArray();
        if (ids.Distinct().Count() != ids.Length) throw new ProjectScheduleOperationException("Mỗi mốc WBS phải có ID duy nhất.");
        var nodes = await db.ProjectScheduleNodes.Where(item => ids.Contains(item.Id))
            .Select(item => new { item.Id, item.OperationalProjectId }).ToListAsync(ct);
        if (nodes.Any(item => item.OperationalProjectId != projectId))
            throw new ProjectScheduleOperationException("Một mốc WBS không thuộc dự án này.");
        var existingIds = nodes.Select(item => item.Id).ToHashSet();
        foreach (var nodeId in ids.Except(existingIds))
            db.ProjectScheduleNodes.Add(new ProjectScheduleNode { Id = nodeId, OperationalProjectId = projectId });
        var byId = requested.Select((item, index) => (item, nodeId: ids[index]))
            .ToDictionary(pair => pair.nodeId, pair => pair.item);
        var wbs = requested.Select(item => item.WbsCode.Trim()).ToArray();
        if (wbs.Any(string.IsNullOrWhiteSpace) || wbs.Distinct(StringComparer.OrdinalIgnoreCase).Count() != wbs.Length)
            throw new ProjectScheduleOperationException("Mã WBS bắt buộc và không được trùng nhau trong revision.");
        foreach (var (nodeId, item) in byId)
        {
            if (string.IsNullOrWhiteSpace(item.Title))
                throw new ProjectScheduleOperationException("Tên mốc WBS không được để trống.");
            if (item.Title.Trim().Length > 300 || item.WbsCode.Trim().Length > 80)
                throw new ProjectScheduleOperationException("Tên mốc tối đa 300 ký tự và mã WBS tối đa 80 ký tự.");
            var start = item.PlannedStart ?? throw new ProjectScheduleOperationException("Ngày bắt đầu là bắt buộc.");
            var end = item.PlannedEnd ?? throw new ProjectScheduleOperationException("Ngày kết thúc là bắt buộc.");
            if (end < start) throw new ProjectScheduleOperationException("Ngày kết thúc không được trước ngày bắt đầu.");
            if (item.ParentNodeId.HasValue)
            {
                if (!byId.TryGetValue(item.ParentNodeId.Value, out var parent) || item.ParentNodeId == nodeId)
                    throw new ProjectScheduleOperationException("Mốc cha phải tồn tại trong cùng revision và không thể là chính nó.");
                if (!parent.PlannedStart.HasValue || !parent.PlannedEnd.HasValue)
                    throw new ProjectScheduleOperationException("Mốc cha phải có đủ ngày bắt đầu và kết thúc.");
                if (start < parent.PlannedStart.Value || end > parent.PlannedEnd.Value)
                    throw new ProjectScheduleOperationException($"Thời gian WBS {item.WbsCode} phải nằm trong khung của mốc cha.");
            }
            revision.Items.Add(new ProjectScheduleItem
            {
                NodeId = nodeId,
                ParentNodeId = item.ParentNodeId,
                WbsCode = item.WbsCode.Trim(),
                Title = item.Title.Trim(),
                PlannedStart = start,
                PlannedEnd = end,
                SortOrder = item.SortOrder,
            });
        }
        if (HasParentCycle(revision.Items)) throw new ProjectScheduleOperationException("Cấu trúc WBS không được có vòng lặp cha-con.");
    }

    private static bool HasParentCycle(IReadOnlyCollection<ProjectScheduleItem> items)
    {
        // Build defensively: the tracked collection may still hold detached rows
        // from the previous revision while it is being replaced.
        var parents = new Dictionary<Guid, Guid?>();
        foreach (var item in items) parents[item.NodeId] = item.ParentNodeId;
        foreach (var id in parents.Keys)
        {
            var visited = new HashSet<Guid>();
            var current = (Guid?)id;
            while (current.HasValue)
            {
                if (!visited.Add(current.Value)) return true;
                current = parents.GetValueOrDefault(current.Value);
            }
        }
        return false;
    }

    private async Task<IDbContextTransaction?> BeginTransactionAsync(
        IsolationLevel? isolationLevel, CancellationToken ct)
    {
        // InMemory is used by unit tests and does not support transactions.
        if (!db.Database.IsRelational()) return null;
        return isolationLevel.HasValue
            ? await db.Database.BeginTransactionAsync(isolationLevel.Value, ct)
            : await db.Database.BeginTransactionAsync(ct);
    }

    private static Task CommitAsync(IDbContextTransaction? transaction, CancellationToken ct) =>
        transaction?.CommitAsync(ct) ?? Task.CompletedTask;

    private static bool MatchesRowVersion(ProjectScheduleRevision revision, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        try { return revision.RowVersion.SequenceEqual(Convert.FromBase64String(value)); }
        catch (FormatException) { return false; }
    }

    private async Task NotifyAffectedDepartmentsAsync(int projectId, ProjectScheduleRevision revision, CancellationToken ct)
    {
        var previousId = await db.ProjectScheduleRevisions.AsNoTracking().Where(item => item.OperationalProjectId == projectId &&
                item.Status == ProjectScheduleRevisionStatus.Published && item.Id != revision.Id)
            .OrderByDescending(item => item.Version).Select(item => (int?)item.Id).FirstOrDefaultAsync(ct);
        var oldItems = previousId.HasValue ? await db.ProjectScheduleItems.AsNoTracking().Where(item => item.RevisionId == previousId.Value)
            .ToDictionaryAsync(item => item.NodeId, ct) : new Dictionary<Guid, ProjectScheduleItem>();
        var currentItems = revision.Items.ToDictionary(item => item.NodeId);
        var changed = oldItems.Keys.Union(currentItems.Keys).Where(id =>
            !oldItems.TryGetValue(id, out var oldItem) || !currentItems.TryGetValue(id, out var newItem) ||
            oldItem.WbsCode != newItem.WbsCode || oldItem.Title != newItem.Title ||
            oldItem.PlannedStart != newItem.PlannedStart || oldItem.PlannedEnd != newItem.PlannedEnd ||
            oldItem.ParentNodeId != newItem.ParentNodeId).ToHashSet();
        var relatedDepartmentTasks = db.DepartmentWorkTasks.AsNoTracking().Where(task => task.WorkPackage.OperationalProjectId == projectId &&
                task.ProjectScheduleNodeId.HasValue && changed.Contains(task.ProjectScheduleNodeId.Value))
            .Select(task => new { task.AssigneeUserId, task.ApproverUserId });
        var departmentAssignees = await relatedDepartmentTasks.Select(task => task.AssigneeUserId).Distinct().ToListAsync(ct);
        var departmentApprovers = await relatedDepartmentTasks.Where(task => task.ApproverUserId.HasValue)
            .Select(task => task.ApproverUserId!.Value).Distinct().ToListAsync(ct);
        var designUserIds = await db.DesignScheduleTasks.AsNoTracking()
            .Where(task => task.OperationalProjectId == projectId && task.ProjectScheduleNodeId.HasValue &&
                changed.Contains(task.ProjectScheduleNodeId.Value))
            .Join(db.OperationalProjectMembers.AsNoTracking(), task => task.AssigneeMemberId, member => member.Id,
                (task, member) => member.UserId).Distinct().ToListAsync(ct);
        var constructionOwners = await db.ConstructionTasks.AsNoTracking()
            .Where(task => task.DesignProject.OperationalProjectId == projectId && task.OwnerUserId.HasValue &&
                task.ProjectScheduleNodeId.HasValue && changed.Contains(task.ProjectScheduleNodeId.Value))
            .Select(task => task.OwnerUserId!.Value).Distinct().ToListAsync(ct);
        var users = departmentAssignees.Union(departmentApprovers).Union(designUserIds)
            .Union(constructionOwners).Distinct().ToList();
        if (users.Count == 0) return;
        var changes = string.Join("; ", changed.Select(id => FormatChange(id, oldItems, currentItems)));
        // Notification.Body is capped at 1.000 characters; an unbounded change
        // summary would fail SaveChanges and silently drop the notification.
        var notificationChanges = Truncate(changes, NotificationChangeSummaryMaxLength);
        var emailChanges = Truncate(changes, EmailChangeSummaryMaxLength);
        foreach (var userId in users)
        {
            try
            {
                await notifications.NotifyFromTemplateAsync(userId, "project.schedule.published",
                    new Dictionary<string, string> { ["projectId"] = projectId.ToString(), ["version"] = revision.Version.ToString(), ["changes"] = notificationChanges },
                    "ProjectScheduleRevision", revision.Id, $"/admin/operational-projects/{projectId}?tab=schedule");
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to notify user {UserId} about schedule revision {RevisionId} for project {ProjectId}.", userId, revision.Id, projectId);
            }
            await QueueEmailAsync(projectId, revision, userId, "Tiến độ tổng thể đã cập nhật", emailChanges, ct);
        }
    }

    private async Task ValidateLinkedTasksForPublicationAsync(
        int projectId, ProjectScheduleRevision revision, CancellationToken ct)
    {
        var tasks = new List<ProjectScheduleLinkedTask>();
        var departmentTasks = await db.DepartmentWorkTasks.AsNoTracking()
            .Where(task => task.WorkPackage.OperationalProjectId == projectId && task.ProjectScheduleNodeId.HasValue)
            .Select(task => new
            {
                task.WbsCode,
                task.Title,
                NodeId = task.ProjectScheduleNodeId!.Value,
                task.PlannedStart,
                task.PlannedEnd,
            }).ToListAsync(ct);
        tasks.AddRange(departmentTasks.Select(task => new ProjectScheduleLinkedTask(
            "công việc phòng ban", $"{task.WbsCode} · {task.Title}", task.NodeId,
            DateOnly.FromDateTime(task.PlannedStart), DateOnly.FromDateTime(task.PlannedEnd))));

        var designTasks = await db.DesignScheduleTasks.AsNoTracking()
            .Where(task => task.OperationalProjectId == projectId && task.ProjectScheduleNodeId.HasValue)
            .Select(task => new
            {
                task.Code,
                task.Name,
                NodeId = task.ProjectScheduleNodeId!.Value,
                task.PlannedStart,
                task.PlannedEnd
            }).ToListAsync(ct);
        tasks.AddRange(designTasks.Select(task => new ProjectScheduleLinkedTask(
            "công việc Thiết kế", $"{task.Code} · {task.Name}", task.NodeId, task.PlannedStart, task.PlannedEnd)));

        var constructionTasks = await db.ConstructionTasks.AsNoTracking()
            .Where(task => task.DesignProject.OperationalProjectId == projectId && task.ProjectScheduleNodeId.HasValue)
            .Select(task => new
            {
                task.TaskCode,
                task.Name,
                NodeId = task.ProjectScheduleNodeId!.Value,
                task.PlannedStart,
                task.PlannedEnd
            }).ToListAsync(ct);
        tasks.AddRange(constructionTasks.Select(task => new ProjectScheduleLinkedTask(
            "công việc Thi công", $"{task.TaskCode} · {task.Name}", task.NodeId, task.PlannedStart, task.PlannedEnd)));

        ProjectSchedulePublicationValidator.Validate(revision, tasks);
    }

    private async Task<bool> CanReadScheduleAsync(int projectId, int callerUserId, CancellationToken ct)
    {
        if (await access.CanViewOperationalProjectAsync(callerUserId, projectId, ct)) return true;
        return await db.DepartmentWorkTasks.AsNoTracking().AnyAsync(task =>
                task.WorkPackage.OperationalProjectId == projectId &&
                (task.AssigneeUserId == callerUserId || task.ApproverUserId == callerUserId), ct) ||
            await db.DesignScheduleTasks.AsNoTracking().AnyAsync(task => task.OperationalProjectId == projectId &&
                task.AssigneeMember != null && task.AssigneeMember.UserId == callerUserId, ct) ||
            await db.ConstructionTasks.AsNoTracking().AnyAsync(task =>
                task.DesignProject.OperationalProjectId == projectId && task.OwnerUserId == callerUserId, ct) ||
            await db.OperationalProjectMembers.AsNoTracking().AnyAsync(member =>
                member.OperationalProjectId == projectId && member.UserId == callerUserId && member.EndedAt == null, ct) ||
            await db.ProjectScheduleRevisions.AsNoTracking().AnyAsync(revision =>
                revision.OperationalProjectId == projectId && revision.ApproverUserId == callerUserId &&
                revision.Status == ProjectScheduleRevisionStatus.PendingApproval, ct);
    }

    private async Task<ProjectScheduleResponse> BuildResponseAsync(int projectId, CancellationToken ct,
        int? callerUserId = null, bool includeUnpublished = true)
    {
        var hasContract = await db.Contracts.AsNoTracking().AnyAsync(item => item.OperationalProjectId == projectId &&
            item.Direction == ContractDirection.Upstream && item.Status != ContractStatus.Draft && item.Status != ContractStatus.Cancelled, ct);
        var revisions = await db.ProjectScheduleRevisions.AsNoTracking().Where(item => item.OperationalProjectId == projectId)
            .Include(item => item.ApproverUser).Include(item => item.Items)
            .OrderByDescending(item => item.Version).ToListAsync(ct);
        if (!includeUnpublished)
            revisions = revisions.Where(item => item.Status == ProjectScheduleRevisionStatus.Published ||
                callerUserId.HasValue && item.ApproverUserId == callerUserId).ToList();
        var projectManagerUserId = await db.OperationalProjects.AsNoTracking().Where(item => item.Id == projectId)
            .Select(item => item.ProjectManagerUserId).SingleOrDefaultAsync(ct);
        var approverOptions = hasContract
            ? await db.Users.AsNoTracking().Where(user => user.Id == projectManagerUserId && user.IsActive && user.Email != null)
                .Select(user => new ProjectScheduleApproverResponse { UserId = user.Id, Name = user.FullName ?? user.PhoneNumber, Email = user.Email })
                .ToListAsync(ct)
            : await db.Users.AsNoTracking().Where(user => user.IsActive && user.Email != null && user.RoleEntity != null &&
                    user.RoleEntity.IsActive && user.RoleEntity.Code == RbacRoleCodes.SalesManager &&
                    user.RoleEntity.RoleGroup != null && user.RoleEntity.RoleGroup.Code == RoleGroupCodes.Crm &&
                    user.RoleEntity.RolePermissions.Any(grant => grant.Permission.IsActive &&
                        grant.Permission.Module == "crm.opportunities" && grant.Permission.Action == "manage"))
                .OrderBy(user => user.FullName).Select(user => new ProjectScheduleApproverResponse
                { UserId = user.Id, Name = user.FullName ?? user.PhoneNumber, Email = user.Email }).ToListAsync(ct);
        return new ProjectScheduleResponse
        {
            OperationalProjectId = projectId,
            HasSignedUpstreamContract = hasContract,
            PublishedRevisionId = revisions.FirstOrDefault(item => item.Status == ProjectScheduleRevisionStatus.Published)?.Id,
            ApproverOptions = includeUnpublished ? approverOptions : [],
            Revisions = revisions.Select(item => new ProjectScheduleRevisionResponse
            {
                Id = item.Id,
                Version = item.Version,
                Status = item.Status.ToString(),
                ChangeReason = item.ChangeReason,
                ApproverUserId = item.ApproverUserId,
                ApproverName = item.ApproverUser?.FullName,
                SubmittedAt = item.SubmittedAt,
                ReviewedAt = item.ReviewedAt,
                ReviewedByUserId = item.ReviewedByUserId,
                ReviewComment = item.ReviewComment,
                PublishedAt = item.PublishedAt,
                RowVersion = Convert.ToBase64String(item.RowVersion),
                Items = item.Items.OrderBy(value => value.SortOrder).ThenBy(value => value.WbsCode).Select(value => new ProjectScheduleItemResponse
                {
                    NodeId = value.NodeId,
                    ParentNodeId = value.ParentNodeId,
                    WbsCode = value.WbsCode,
                    Title = value.Title,
                    PlannedStart = value.PlannedStart,
                    PlannedEnd = value.PlannedEnd,
                    SortOrder = value.SortOrder,
                }).ToList(),
            }).ToList(),
        };
    }

    private async Task<string> GetChangeSummaryAsync(int projectId, ProjectScheduleRevision revision, CancellationToken ct)
    {
        var previousId = await db.ProjectScheduleRevisions.AsNoTracking().Where(item => item.OperationalProjectId == projectId &&
                item.Status == ProjectScheduleRevisionStatus.Published && item.Id != revision.Id)
            .OrderByDescending(item => item.Version).Select(item => (int?)item.Id).FirstOrDefaultAsync(ct);
        if (!previousId.HasValue) return "Thiết lập tiến độ tổng thể ban đầu.";
        var before = await db.ProjectScheduleItems.AsNoTracking().Where(item => item.RevisionId == previousId.Value)
            .ToDictionaryAsync(item => item.NodeId, ct);
        var after = revision.Items.ToDictionary(item => item.NodeId);
        var changes = before.Keys.Union(after.Keys).Where(id =>
            !before.TryGetValue(id, out var oldItem) || !after.TryGetValue(id, out var newItem) ||
            oldItem.WbsCode != newItem.WbsCode || oldItem.Title != newItem.Title ||
            oldItem.PlannedStart != newItem.PlannedStart || oldItem.PlannedEnd != newItem.PlannedEnd ||
            oldItem.ParentNodeId != newItem.ParentNodeId).ToList();
        if (changes.Count == 0) return "Không thay đổi nội dung so với bản đã phát hành.";
        return string.Join("; ", changes.Select(id => FormatChange(id, before, after)));
    }

    private static string FormatChange(Guid nodeId,
        IReadOnlyDictionary<Guid, ProjectScheduleItem> before,
        IReadOnlyDictionary<Guid, ProjectScheduleItem> after)
    {
        static string Describe(ProjectScheduleItem item, IReadOnlyDictionary<Guid, ProjectScheduleItem> items)
        {
            var parentCode = item.ParentNodeId.HasValue && items.TryGetValue(item.ParentNodeId.Value, out var parent)
                ? parent.WbsCode
                : "—";
            return $"{item.WbsCode} · {item.Title} ({parentCode}) [{item.PlannedStart:yyyy-MM-dd}–{item.PlannedEnd:yyyy-MM-dd}]";
        }

        before.TryGetValue(nodeId, out var oldItem);
        after.TryGetValue(nodeId, out var newItem);
        if (oldItem is null) return $"(+) {Describe(newItem!, after)}";
        if (newItem is null) return $"(-) {Describe(oldItem, before)}";
        return $"{Describe(oldItem, before)} → {Describe(newItem, after)}";
    }

    private async Task QueueEmailAsync(int projectId, ProjectScheduleRevision revision, int userId,
        string eventName, string changeSummary, CancellationToken ct)
    {
        try
        {
            var recipient = await db.Users.AsNoTracking().Where(user => user.Id == userId && user.IsActive)
                .Select(user => new { user.Email, user.FullName }).SingleOrDefaultAsync(ct);
            if (string.IsNullOrWhiteSpace(recipient?.Email))
            {
                logger.LogWarning("Project schedule email skipped: user {UserId} has no email; revision {RevisionId}.", userId, revision.Id);
                return;
            }
            var project = await db.OperationalProjects.AsNoTracking().Where(item => item.Id == projectId)
                .Select(item => new { item.Code, item.Name }).SingleAsync(ct);
            var template = ProjectScheduleEmailTemplates.Get(await db.SiteSettings.AsNoTracking().FirstOrDefaultAsync(ct));
            var rendered = ProjectScheduleEmailTemplates.Render(template, new Dictionary<string, string>
            {
                ["eventName"] = eventName,
                ["recipientName"] = recipient.FullName ?? string.Empty,
                ["projectCode"] = project.Code,
                ["projectName"] = project.Name,
                ["version"] = revision.Version.ToString(),
                ["changeReason"] = revision.ChangeReason ?? "—",
                ["changeSummary"] = Truncate(changeSummary, EmailChangeSummaryMaxLength),
            });
            // Store first, send later: the worker retries a failed delivery and
            // keeps the row for review instead of losing the message.
            db.ProjectScheduleEmails.Add(new ProjectScheduleEmail
            {
                OperationalProjectId = projectId,
                RevisionId = revision.Id,
                RecipientUserId = userId,
                ToEmail = recipient.Email.Trim(),
                Subject = rendered.Subject,
                Body = rendered.Body,
                Status = ProjectScheduleEmail.PendingStatus,
                CreatedAt = DateTime.UtcNow,
                NextAttemptAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Project schedule email could not be queued for project {ProjectId}, revision {RevisionId}, recipient {UserId}.", projectId, revision.Id, userId);
        }
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength - 1), "…");
}
