using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
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
    Task<ProjectScheduleEmailPreviewResponse> PreviewEmailAsync(int projectId, int revisionId, string action, int? recipientUserId, int callerUserId, CancellationToken ct);
}

public sealed record ProjectScheduleEmailPreview(int RecipientUserId, string ToEmail, string Subject, string Body);
public sealed record ProjectScheduleEmailPreviewResponse(string PreviewToken, List<ProjectScheduleEmailPreview> Emails);

public sealed class ProjectScheduleService(
    AppDbContext db,
    IProjectAccessService access,
    IPermissionService permissions,
    INotificationService notifications,
    IDataProtectionProvider protection,
    ILogger<ProjectScheduleService> logger) : IProjectScheduleService
{
    private const int NotificationChangeSummaryMaxLength = 900;
    private const string ConcurrentEditMessage =
        "Bản tiến độ vừa được người khác cập nhật. Hãy tải lại để xem thay đổi mới nhất trước khi lưu.";
    private readonly IDataProtector previewProtector = protection.CreateProtector("ProjectSchedule.EmailPreview.v1");
    private sealed record EmailPreviewClaim(int ProjectId, int RevisionId, int UserId, string Action,
        int? RecipientUserId, string RowVersion, string ContentHash, DateTime ExpiresAt);

    public async Task<ProjectScheduleResponse?> GetAsync(int projectId, int callerUserId, CancellationToken ct)
    {
        if (!await CanReadScheduleAsync(projectId, callerUserId, ct)) return null;
        var canManage = await access.CanViewOperationalProjectAsync(callerUserId, projectId, ct) &&
            await CanAuthorScheduleAsync(callerUserId, ct);
        return await BuildResponseAsync(projectId, ct, callerUserId, canManage);
    }

    // The master frame belongs to CRM: sales staff author it, other departments
    // only detail inside it (MoM 2026-10-31).
    private async Task<bool> CanAuthorScheduleAsync(int userId, CancellationToken ct) =>
        await permissions.HasAsync(userId, "operations.projects.manage", ct) &&
        await permissions.HasAsync(userId, "crm.opportunities.manage", ct);

    public async Task<ProjectScheduleResponse> CreateRevisionAsync(
        int projectId, SaveProjectScheduleRevisionRequest request, int callerUserId, CancellationToken ct)
    {
        var project = await db.OperationalProjects.SingleOrDefaultAsync(item => item.Id == projectId, ct)
            ?? throw new ProjectScheduleOperationException("Dự án không tồn tại.", 404);
        if (!await access.CanViewOperationalProjectAsync(callerUserId, projectId, ct))
            throw new ProjectScheduleOperationException("Không tìm thấy dự án.", 404);
        if (!await CanAuthorScheduleAsync(callerUserId, ct))
            throw new ProjectScheduleOperationException("Chỉ phòng CRM được lập và gửi duyệt tiến độ tổng thể.", 403);
        if (await db.ProjectScheduleRevisions.AnyAsync(item => item.OperationalProjectId == projectId &&
                (item.Status == ProjectScheduleRevisionStatus.Draft ||
                 item.Status == ProjectScheduleRevisionStatus.PendingApproval), ct))
            throw new ProjectScheduleOperationException("Dự án đã có một phiên bản tiến độ đang soạn hoặc chờ duyệt.", 409);

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
        if (!await access.CanViewOperationalProjectAsync(callerUserId, projectId, ct)) return null;
        if (!await CanAuthorScheduleAsync(callerUserId, ct))
            throw new ProjectScheduleOperationException("Chỉ phòng CRM được lập và gửi duyệt tiến độ tổng thể.", 403);
        if (revision.Status is not (ProjectScheduleRevisionStatus.Draft or ProjectScheduleRevisionStatus.Rejected))
            throw new ProjectScheduleOperationException("Chỉ bản tiến độ đang soạn hoặc bị trả lại mới được chỉnh sửa.", 409);
        if (!string.IsNullOrWhiteSpace(request.RowVersion) && !MatchesRowVersion(revision, request.RowVersion))
            throw new ProjectScheduleOperationException(ConcurrentEditMessage, 409);
        if (await db.ProjectScheduleRevisions.AnyAsync(item => item.OperationalProjectId == projectId &&
                item.Id != revisionId && (item.Status == ProjectScheduleRevisionStatus.Draft ||
                    item.Status == ProjectScheduleRevisionStatus.PendingApproval), ct))
            throw new ProjectScheduleOperationException("Dự án đang có bản tiến độ khác đang soạn hoặc chờ duyệt.", 409);

        await using var transaction = await BeginTransactionAsync(null, ct);
        revision.Status = ProjectScheduleRevisionStatus.Draft;
        revision.ChangeReason = request.ChangeReason?.Trim();
        revision.ApproverUserId = null;
        revision.SubmittedAt = null;
        revision.SubmittedByUserId = null;
        revision.ReviewedAt = null;
        revision.ReviewedByUserId = null;
        revision.ReviewComment = null;
        // Always update the revision row so its RowVersion changes, even when only
        // the item rows differ; a concurrent save then fails instead of silently winning.
        db.Entry(revision).Property(item => item.Status).IsModified = true;
        db.ProjectScheduleItems.RemoveRange(revision.Items);
        // Commit the removals before inserting the replacement set so the unique
        // (RevisionId, NodeId) index never sees the old and new rows at once.
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new ProjectScheduleOperationException(ConcurrentEditMessage, 409); }
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
        if (!await access.CanViewOperationalProjectAsync(callerUserId, projectId, ct)) return null;
        if (!await CanAuthorScheduleAsync(callerUserId, ct))
            throw new ProjectScheduleOperationException("Chỉ phòng CRM được lập và gửi duyệt tiến độ tổng thể.", 403);
        var reassigning = revision.Status == ProjectScheduleRevisionStatus.PendingApproval;
        if ((!reassigning && revision.Status != ProjectScheduleRevisionStatus.Draft) || revision.Items.Count == 0)
            throw new ProjectScheduleOperationException("Chỉ bản tiến độ đang soạn có ít nhất một hạng mục mới được gửi duyệt.", 409);
        if (revision.Version > 1 && string.IsNullOrWhiteSpace(revision.ChangeReason))
            throw new ProjectScheduleOperationException("Phiên bản tiến độ mới cần ghi rõ yêu cầu khách hàng đã thay đổi.");
        if (await db.ProjectScheduleRevisions.AnyAsync(item => item.OperationalProjectId == projectId &&
                item.Id != revisionId && item.Status == ProjectScheduleRevisionStatus.PendingApproval, ct))
            throw new ProjectScheduleOperationException("Dự án đang có bản tiến độ khác chờ duyệt.", 409);
        if (!await IsEligibleApproverAsync(projectId, request.ApproverUserId, ct))
            throw new ProjectScheduleOperationException("Người duyệt phải là người quản lý dự án sau khi ký hợp đồng với chủ đầu tư, hoặc Trưởng phòng Kinh doanh trước khi ký.");
        if (!MatchesRowVersion(revision, request.RowVersion))
            throw new ProjectScheduleOperationException("Bản tiến độ đã được cập nhật. Hãy tải lại trước khi gửi duyệt.", 409);
        await using var transaction = await BeginTransactionAsync(null, ct);
        var approvalEmail = await RenderEmailAsync(projectId, revision, request.ApproverUserId,
            "Yêu cầu duyệt tiến độ tổng thể", await GetChangeSummaryAsync(projectId, revision, ct), ct);
        var approvalEmails = approvalEmail is null ? new List<ProjectScheduleEmailPreview>() : new List<ProjectScheduleEmailPreview> { approvalEmail };
        RequireEmailPreview(request.PreviewToken, projectId, revision, callerUserId, "Submit", request.ApproverUserId, approvalEmails);
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
        // Re-submitting to the same approver while the first notice is still queued
        // must not queue a second one; both would pass the worker's validity check.
        if (approvalEmail is not null &&
            !await db.ProjectScheduleEmails.AnyAsync(item => item.RevisionId == revision.Id &&
                !item.CmScheduleBaselineId.HasValue && item.RecipientUserId == request.ApproverUserId &&
                (item.Status == ProjectScheduleEmail.PendingStatus || item.Status == ProjectScheduleEmail.SendingStatus), ct))
            await QueueEmailAsync(projectId, revision, approvalEmail, ct);
        await CommitAsync(transaction, ct);
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
            throw new ProjectScheduleOperationException("Chỉ người duyệt được chỉ định mới có thể quyết định bản tiến độ đang chờ.", 403);
        if (!await IsEligibleApproverAsync(projectId, callerUserId, ct))
            throw new ProjectScheduleOperationException("Người duyệt không còn phù hợp với giai đoạn hợp đồng hiện tại của dự án.", 403);
        if (!MatchesRowVersion(revision, request.RowVersion))
            throw new ProjectScheduleOperationException("Bản tiến độ đã được cập nhật. Hãy tải lại trước khi duyệt.", 409);
        if (request.Decision == "Reject" && string.IsNullOrWhiteSpace(request.Comment))
            throw new ProjectScheduleOperationException("Cần nêu lý do khi trả lại bản tiến độ.");

        await using var transaction = await BeginTransactionAsync(IsolationLevel.Serializable, ct);
        List<int> publicationUsers = [];
        string publicationChanges = string.Empty;
        List<ProjectScheduleEmailPreview> publicationEmails = [];
        if (request.Decision == "Approve")
        {
            if (await db.ProjectScheduleRevisions.AnyAsync(item => item.OperationalProjectId == projectId &&
                    item.Id != revision.Id && item.Status == ProjectScheduleRevisionStatus.Published &&
                    item.Version > revision.Version, ct))
                throw new ProjectScheduleOperationException(
                    "Đã có phiên bản tiến độ mới hơn được phát hành. Hãy trả lại bản này thay vì phát hành.", 409);
            await ValidateLinkedTasksForPublicationAsync(projectId, revision, ct);
            (publicationUsers, publicationChanges) = await GetPublicationRecipientsAsync(projectId, revision, ct);
            publicationEmails = await RenderPublicationEmailsAsync(projectId, revision, publicationUsers, publicationChanges, ct);
            RequireEmailPreview(request.PreviewToken, projectId, revision, callerUserId, "Publish", null, publicationEmails);
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
        // The approval notice is stale the moment the approver decides, whether the
        // outcome is published or rejected. Only the approver's own notice can be
        // queued before this point, so every pending row for the revision is one.
        var staleEmails = await db.ProjectScheduleEmails
            .Where(item => item.RevisionId == revision.Id && !item.CmScheduleBaselineId.HasValue &&
                (item.Status == ProjectScheduleEmail.PendingStatus || item.Status == ProjectScheduleEmail.SendingStatus))
            .ToListAsync(ct);
        foreach (var staleEmail in staleEmails)
        {
            staleEmail.Status = ProjectScheduleEmail.CancelledStatus;
            staleEmail.NextAttemptAt = null;
        }
        await db.SaveChangesAsync(ct);
        if (revision.Status == ProjectScheduleRevisionStatus.Published)
            await NotifyAffectedDepartmentsAsync(projectId, revision, publicationUsers, publicationChanges, publicationEmails, ct);
        await CommitAsync(transaction, ct);
        logger.LogInformation("Project schedule revision {RevisionId} reviewed as {Decision} by assigned approver {UserId}.", revision.Id, request.Decision, callerUserId);
        var canManage = await access.CanViewOperationalProjectAsync(callerUserId, projectId, ct) &&
            await CanAuthorScheduleAsync(callerUserId, ct);
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
        if (ids.Distinct().Count() != ids.Length) throw new ProjectScheduleOperationException("Mỗi hạng mục tiến độ phải có mã nhận diện riêng.");
        var nodes = await db.ProjectScheduleNodes.Where(item => ids.Contains(item.Id))
            .Select(item => new { item.Id, item.OperationalProjectId }).ToListAsync(ct);
        if (nodes.Any(item => item.OperationalProjectId != projectId))
            throw new ProjectScheduleOperationException("Một hạng mục tiến độ không thuộc dự án này.");
        var existingIds = nodes.Select(item => item.Id).ToHashSet();
        foreach (var nodeId in ids.Except(existingIds))
            db.ProjectScheduleNodes.Add(new ProjectScheduleNode { Id = nodeId, OperationalProjectId = projectId });
        var byId = requested.Select((item, index) => (item, nodeId: ids[index]))
            .ToDictionary(pair => pair.nodeId, pair => pair.item);
        var wbs = requested.Select(item => item.WbsCode.Trim()).ToArray();
        if (wbs.Any(string.IsNullOrWhiteSpace) || wbs.Distinct(StringComparer.OrdinalIgnoreCase).Count() != wbs.Length)
            throw new ProjectScheduleOperationException("Mã WBS bắt buộc và không được trùng nhau trong phiên bản tiến độ.");
        foreach (var (nodeId, item) in byId)
        {
            if (string.IsNullOrWhiteSpace(item.Title))
                throw new ProjectScheduleOperationException("Tên hạng mục tiến độ không được để trống.");
            if (item.Title.Trim().Length > 300 || item.WbsCode.Trim().Length > 80)
                throw new ProjectScheduleOperationException("Tên hạng mục tối đa 300 ký tự và mã WBS tối đa 80 ký tự.");
            var start = item.PlannedStart ?? throw new ProjectScheduleOperationException("Ngày bắt đầu là bắt buộc.");
            var end = item.PlannedEnd ?? throw new ProjectScheduleOperationException("Ngày kết thúc là bắt buộc.");
            if (end < start) throw new ProjectScheduleOperationException("Ngày kết thúc không được trước ngày bắt đầu.");
            if (item.ParentNodeId.HasValue)
            {
                if (!byId.TryGetValue(item.ParentNodeId.Value, out var parent) || item.ParentNodeId == nodeId)
                    throw new ProjectScheduleOperationException("Hạng mục cha phải nằm trong cùng phiên bản tiến độ và không thể là chính nó.");
                if (!parent.PlannedStart.HasValue || !parent.PlannedEnd.HasValue)
                    throw new ProjectScheduleOperationException("Hạng mục cha phải có đủ ngày bắt đầu và kết thúc.");
                if (start < parent.PlannedStart.Value || end > parent.PlannedEnd.Value)
                    throw new ProjectScheduleOperationException($"Thời gian của hạng mục {item.WbsCode} phải nằm trong khung của hạng mục cha.");
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
        if (HasParentCycle(revision.Items)) throw new ProjectScheduleOperationException("Các hạng mục tiến độ không được liên kết cha-con thành vòng lặp.");
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

    private async Task NotifyAffectedDepartmentsAsync(int projectId, ProjectScheduleRevision revision,
        IReadOnlyCollection<int> users, string changes, IReadOnlyCollection<ProjectScheduleEmailPreview> emails, CancellationToken ct)
    {
        if (users.Count == 0) return;
        var notificationChanges = Truncate(changes, NotificationChangeSummaryMaxLength);
        var emailByUser = emails.ToDictionary(item => item.RecipientUserId);
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
            if (emailByUser.TryGetValue(userId, out var preview))
                await QueueEmailAsync(projectId, revision, preview, ct);
        }
    }

    private async Task<(List<int> Users, string Changes)> GetPublicationRecipientsAsync(int projectId, ProjectScheduleRevision revision, CancellationToken ct)
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
                task.ProjectScheduleNodeId.HasValue && changed.Contains(task.ProjectScheduleNodeId.Value) &&
                task.Status != WorkTaskStatus.Cancelled)
            .Select(task => new { task.AssigneeUserId, task.ApproverUserId, task.WorkPackage.LeadUserId });
        var departmentAssignees = await relatedDepartmentTasks.Select(task => task.AssigneeUserId).Distinct().ToListAsync(ct);
        var departmentApprovers = await relatedDepartmentTasks.Where(task => task.ApproverUserId.HasValue)
            .Select(task => task.ApproverUserId!.Value).Distinct().ToListAsync(ct);
        // The department lead owns the package and must review how the changed
        // frame moves their detail plan, even when they hold no task row.
        var departmentLeads = await relatedDepartmentTasks.Select(task => task.LeadUserId)
            .Distinct().ToListAsync(ct);
        var designUserIds = await db.DesignScheduleTasks.AsNoTracking()
            .Where(task => task.OperationalProjectId == projectId && task.ProjectScheduleNodeId.HasValue &&
                changed.Contains(task.ProjectScheduleNodeId.Value))
            .Join(db.OperationalProjectMembers.AsNoTracking(), task => task.AssigneeMemberId, member => member.Id,
                (task, member) => member.UserId).Distinct().ToListAsync(ct);
        var designApprovers = await db.DesignScheduleTasks.AsNoTracking()
            .Where(task => task.OperationalProjectId == projectId && task.ProjectScheduleNodeId.HasValue &&
                changed.Contains(task.ProjectScheduleNodeId.Value))
            .Join(db.OperationalProjectMembers.AsNoTracking(), task => task.ApproverMemberId, member => member.Id,
                (task, member) => member.UserId).Distinct().ToListAsync(ct);
        var constructionOwners = await db.ConstructionTasks.AsNoTracking()
            .Where(task => task.DesignProject.OperationalProjectId == projectId && task.OwnerUserId.HasValue &&
                task.ProjectScheduleNodeId.HasValue && changed.Contains(task.ProjectScheduleNodeId.Value) &&
                task.Status != ConstructionTaskStatus.Cancelled)
            .Select(task => task.OwnerUserId!.Value).Distinct().ToListAsync(ct);
        var users = departmentAssignees.Union(departmentApprovers).Union(departmentLeads)
            .Union(designUserIds).Union(designApprovers).Union(constructionOwners)
            .Distinct().OrderBy(userId => userId).ToList();
        var changes = string.Join("; ", changed.OrderBy(id => id).Select(id => FormatChange(id, oldItems, currentItems)));
        return (users, changes);
    }

    private async Task ValidateLinkedTasksForPublicationAsync(
        int projectId, ProjectScheduleRevision revision, CancellationToken ct)
    {
        var tasks = new List<ProjectScheduleLinkedTask>();
        var departmentTasks = await db.DepartmentWorkTasks.AsNoTracking()
            .Where(task => task.WorkPackage.OperationalProjectId == projectId && task.ProjectScheduleNodeId.HasValue &&
                task.Status != WorkTaskStatus.Cancelled)
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
            .Where(task => task.DesignProject.OperationalProjectId == projectId && task.ProjectScheduleNodeId.HasValue &&
                task.Status != ConstructionTaskStatus.Cancelled)
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
                (task.AssigneeUserId == callerUserId || task.ApproverUserId == callerUserId ||
                 task.WorkPackage.LeadUserId == callerUserId), ct) ||
            await db.DesignScheduleTasks.AsNoTracking().AnyAsync(task => task.OperationalProjectId == projectId &&
                (task.AssigneeMember != null && task.AssigneeMember.UserId == callerUserId ||
                 task.ApproverMember != null && task.ApproverMember.UserId == callerUserId), ct) ||
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
            oldItem.ParentNodeId != newItem.ParentNodeId).OrderBy(id => id).ToList();
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

    private async Task QueueEmailAsync(int projectId, ProjectScheduleRevision revision,
        ProjectScheduleEmailPreview preview, CancellationToken ct)
    {
        db.ProjectScheduleEmails.Add(new ProjectScheduleEmail
        {
            OperationalProjectId = projectId,
            RevisionId = revision.Id,
            RecipientUserId = preview.RecipientUserId,
            ToEmail = preview.ToEmail,
            Subject = preview.Subject,
            Body = preview.Body,
            Status = ProjectScheduleEmail.PendingStatus,
            CreatedAt = DateTime.UtcNow,
            NextAttemptAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<ProjectScheduleEmailPreviewResponse> PreviewEmailAsync(int projectId, int revisionId,
        string action, int? recipientUserId, int callerUserId, CancellationToken ct)
    {
        var revision = await db.ProjectScheduleRevisions.AsNoTracking().Include(item => item.Items)
            .SingleOrDefaultAsync(item => item.Id == revisionId && item.OperationalProjectId == projectId, ct)
            ?? throw new ProjectScheduleOperationException("Không tìm thấy bản tiến độ.", 404);
        if (action == "Submit")
        {
            if (!await access.CanViewOperationalProjectAsync(callerUserId, projectId, ct) ||
                !await CanAuthorScheduleAsync(callerUserId, ct))
                throw new ProjectScheduleOperationException("Bạn không có quyền gửi bản tiến độ này.", 403);
            if (revision.Status is not (ProjectScheduleRevisionStatus.Draft or ProjectScheduleRevisionStatus.PendingApproval) ||
                !recipientUserId.HasValue || !await IsEligibleApproverAsync(projectId, recipientUserId.Value, ct))
                throw new ProjectScheduleOperationException("Hãy chọn người duyệt phù hợp và kiểm tra trạng thái bản tiến độ.", 409);
            var preview = await RenderEmailAsync(projectId, revision, recipientUserId.Value,
                "Yêu cầu duyệt tiến độ tổng thể", await GetChangeSummaryAsync(projectId, revision, ct), ct);
            var emails = preview is null ? new List<ProjectScheduleEmailPreview>() : new List<ProjectScheduleEmailPreview> { preview };
            return CreateEmailPreview(projectId, revision, callerUserId, action, recipientUserId, emails);
        }
        if (action != "Publish" || revision.Status != ProjectScheduleRevisionStatus.PendingApproval ||
            revision.ApproverUserId != callerUserId || !await IsEligibleApproverAsync(projectId, callerUserId, ct))
            throw new ProjectScheduleOperationException("Bạn không thể xem email phát hành cho bản tiến độ này.", 403);
        var (users, changes) = await GetPublicationRecipientsAsync(projectId, revision, ct);
        var previews = await RenderPublicationEmailsAsync(projectId, revision, users, changes, ct);
        return CreateEmailPreview(projectId, revision, callerUserId, action, null, previews);
    }

    private async Task<List<ProjectScheduleEmailPreview>> RenderPublicationEmailsAsync(int projectId,
        ProjectScheduleRevision revision, IEnumerable<int> users, string changes, CancellationToken ct)
    {
        var previews = new List<ProjectScheduleEmailPreview>();
        foreach (var userId in users)
        {
            var preview = await RenderEmailAsync(projectId, revision, userId,
                "Tiến độ tổng thể đã cập nhật", changes, ct);
            if (preview is not null) previews.Add(preview);
        }
        return previews;
    }

    private ProjectScheduleEmailPreviewResponse CreateEmailPreview(int projectId, ProjectScheduleRevision revision,
        int callerUserId, string action, int? recipientUserId, List<ProjectScheduleEmailPreview> emails)
    {
        var claim = new EmailPreviewClaim(projectId, revision.Id, callerUserId, action, recipientUserId,
            Convert.ToBase64String(revision.RowVersion), EmailHash(emails), DateTime.UtcNow.AddMinutes(15));
        return new(previewProtector.Protect(JsonSerializer.Serialize(claim)), emails);
    }

    private void RequireEmailPreview(string? token, int projectId, ProjectScheduleRevision revision,
        int callerUserId, string action, int? recipientUserId, IReadOnlyCollection<ProjectScheduleEmailPreview> emails)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ProjectScheduleOperationException("Hãy xem trước email trước khi tiếp tục.");
        EmailPreviewClaim? claim;
        try { claim = JsonSerializer.Deserialize<EmailPreviewClaim>(previewProtector.Unprotect(token)); }
        catch (Exception error) when (error is CryptographicException or JsonException or ArgumentException)
        { throw new ProjectScheduleOperationException("Bản xem trước không còn hợp lệ. Hãy xem lại email trước khi tiếp tục.", 409); }
        if (claim is null || claim.ProjectId != projectId || claim.RevisionId != revision.Id ||
            claim.UserId != callerUserId || claim.Action != action || claim.RecipientUserId != recipientUserId ||
            claim.RowVersion != Convert.ToBase64String(revision.RowVersion) ||
            claim.ContentHash != EmailHash(emails) || claim.ExpiresAt < DateTime.UtcNow)
            throw new ProjectScheduleOperationException("Nội dung email hoặc người nhận đã thay đổi. Hãy xem lại email trước khi tiếp tục.", 409);
    }

    private static string EmailHash(IEnumerable<ProjectScheduleEmailPreview> emails) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
            emails.OrderBy(item => item.RecipientUserId)))));

    private async Task<ProjectScheduleEmailPreview?> RenderEmailAsync(int projectId, ProjectScheduleRevision revision,
        int userId, string eventName, string changeSummary, CancellationToken ct)
    {
        var recipient = await db.Users.AsNoTracking().Where(user => user.Id == userId && user.IsActive)
                .Select(user => new { user.Email, user.FullName }).SingleOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(recipient?.Email))
        {
            logger.LogWarning("Project schedule email skipped: user {UserId} has no email; revision {RevisionId}.", userId, revision.Id);
            return null;
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
            ["changeSummary"] = changeSummary,
        });
        return new ProjectScheduleEmailPreview(userId, recipient.Email.Trim(), rendered.Subject, rendered.Body);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength - 1), "…");
}
