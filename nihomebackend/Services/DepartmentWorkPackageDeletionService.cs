using System.Data;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public sealed partial class DepartmentWorkPackageService
{
    public async Task<DeletionImpactResponse?> GetTaskDeletionImpactAsync(
        int packageId, int taskId, int callerUserId, CancellationToken ct = default)
    {
        var task = await db.DepartmentWorkTasks.AsNoTracking()
            .Include(item => item.WorkPackage)
            .SingleOrDefaultAsync(item => item.Id == taskId && item.WorkPackageId == packageId, ct);
        if (task is null) return null;
        if (!await CanOperateTaskAsync(task, callerUserId,
                await IsAdministratorAsync(callerUserId, ct), ct))
            throw ScopeError();
        if (task.Status != WorkTaskStatus.Disabled || task.DisabledRootTaskId != taskId)
            throw Error("Chỉ có thể xóa vĩnh viễn một cây công việc đã ở trạng thái Disabled.");
        return await DeletionImpactPlanner.ForWorkTaskAsync(db, packageId, taskId, ct);
    }

    public async Task<bool> DisableTaskAsync(
        int packageId, int taskId, string? rowVersion, int callerUserId, CancellationToken ct = default)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var tasks = await db.DepartmentWorkTasks.Include(item => item.WorkPackage)
            .Where(item => item.WorkPackageId == packageId).ToListAsync(ct);
        var task = tasks.SingleOrDefault(item => item.Id == taskId);
        if (task is null) return false;
        if (!await CanOperateTaskAsync(task, callerUserId,
                await IsAdministratorAsync(callerUserId, ct), ct))
            throw ScopeError();
        await EnsureTenderIsMutableAsync(task.WorkPackage, ct);
        if (task.WorkPackage.Status is WorkPackageStatus.Completed or WorkPackageStatus.Cancelled)
            throw Error("Không thể vô hiệu hóa đầu việc trong nhóm đã kết thúc.");
        if (task.Status == WorkTaskStatus.Disabled)
            throw Error("Đầu việc đã ở trạng thái Disabled.");

        var descendants = GetTaskDescendants(taskId, tasks);
        if (descendants.Any(item => item.Status == WorkTaskStatus.Disabled))
            throw Error("Hãy khôi phục hoặc xóa vĩnh viễn công việc Disabled bên dưới trước.");
        var affected = new[] { task }.Concat(descendants).ToList();
        CrmConcurrency.Apply(db, task, rowVersion);
        var now = DateTime.UtcNow;
        foreach (var item in affected)
        {
            item.StatusBeforeDisabled = item.Status;
            item.Status = WorkTaskStatus.Disabled;
            item.DisabledAt = now;
            item.DisabledByUserId = callerUserId;
            item.DisabledRootTaskId = taskId;
            item.UpdatedAt = now;
            item.UpdatedByUserId = callerUserId;
        }

        TouchPackage(task.WorkPackage, callerUserId, now);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        foreach (var item in affected)
            await AddHistoryAsync(packageId, "Task", item.Id, "Disabled",
                new { item.WbsCode, PreviousStatus = item.StatusBeforeDisabled }, callerUserId, ct);
        if (task.ParentTaskId.HasValue)
        {
            await RollupParentTasksAsync(packageId, task.ParentTaskId.Value, callerUserId, ct);
            await CrmConcurrency.SaveChangesAsync(db, ct);
        }
        if (transaction is not null) await transaction.CommitAsync(ct);
        return true;
    }

    public async Task<bool> RestoreTaskAsync(
        int packageId, int taskId, string? rowVersion, int callerUserId, CancellationToken ct = default)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var tasks = await db.DepartmentWorkTasks.Include(item => item.WorkPackage)
            .Where(item => item.WorkPackageId == packageId).ToListAsync(ct);
        var task = tasks.SingleOrDefault(item => item.Id == taskId);
        if (task is null) return false;
        if (!await CanOperateTaskAsync(task, callerUserId,
                await IsAdministratorAsync(callerUserId, ct), ct))
            throw ScopeError();
        await EnsureTenderIsMutableAsync(task.WorkPackage, ct);
        if (task.Status != WorkTaskStatus.Disabled || task.DisabledRootTaskId != taskId ||
            !task.DisabledAt.HasValue || !task.StatusBeforeDisabled.HasValue)
            throw Error("Chỉ có thể khôi phục cây công việc Disabled từ đầu cây.");
        if (task.DisabledAt.Value.AddDays(30) <= DateTime.UtcNow)
            throw Error("Thời hạn khôi phục 30 ngày đã hết. Cây công việc sẽ được xóa tự động.");
        if (task.WorkPackage.Status is WorkPackageStatus.Completed or WorkPackageStatus.Cancelled)
            throw Error("Không thể khôi phục đầu việc trong nhóm đã kết thúc.");

        var affected = tasks.Where(item => item.Status == WorkTaskStatus.Disabled &&
            item.DisabledRootTaskId == taskId).ToList();
        if (!affected.Any(item => item.Id == taskId))
            throw Error("Không tìm thấy cây công việc Disabled để khôi phục.");
        CrmConcurrency.Apply(db, task, rowVersion);
        var now = DateTime.UtcNow;
        var restored = affected.Select(item => new { Task = item, PreviousStatus = item.StatusBeforeDisabled }).ToList();
        foreach (var entry in restored)
        {
            entry.Task.Status = entry.PreviousStatus ?? WorkTaskStatus.Planned;
            entry.Task.DisabledAt = null;
            entry.Task.DisabledByUserId = null;
            entry.Task.DisabledRootTaskId = null;
            entry.Task.StatusBeforeDisabled = null;
            entry.Task.UpdatedAt = now;
            entry.Task.UpdatedByUserId = callerUserId;
        }
        TouchPackage(task.WorkPackage, callerUserId, now);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        foreach (var entry in restored)
            await AddHistoryAsync(packageId, "Task", entry.Task.Id, "Restored",
                new { entry.Task.WbsCode, RestoredStatus = entry.PreviousStatus }, callerUserId, ct);
        if (task.ParentTaskId.HasValue)
        {
            await RollupParentTasksAsync(packageId, task.ParentTaskId.Value, callerUserId, ct);
            await CrmConcurrency.SaveChangesAsync(db, ct);
        }
        if (transaction is not null) await transaction.CommitAsync(ct);
        return true;
    }

    public async Task<bool> PermanentlyDeleteTaskAsync(
        int packageId, int taskId, ConfirmDeletionRequest request, int callerUserId, CancellationToken ct = default)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var tasks = await db.DepartmentWorkTasks.Include(item => item.WorkPackage)
            .Where(item => item.WorkPackageId == packageId).ToListAsync(ct);
        var task = tasks.SingleOrDefault(item => item.Id == taskId);
        if (task is null) return false;
        if (!await CanOperateTaskAsync(task, callerUserId,
                await IsAdministratorAsync(callerUserId, ct), ct))
            throw ScopeError();
        if (task.Status != WorkTaskStatus.Disabled || task.DisabledRootTaskId != taskId)
            throw Error("Hãy vô hiệu hóa đầu việc trước khi xóa vĩnh viễn.");
        var descendants = GetTaskDescendants(taskId, tasks);
        if (descendants.Any(item => item.Status != WorkTaskStatus.Disabled || item.DisabledRootTaskId != taskId))
            throw Error("Toàn bộ cây công việc phải ở trạng thái Disabled trước khi xóa vĩnh viễn.");
        var impact = await DeletionImpactPlanner.ForWorkTaskAsync(db, packageId, taskId, ct);
        if (!string.Equals(request.PlanToken, impact!.PlanToken, StringComparison.Ordinal))
            throw new DeletionPlanChangedException("Dữ liệu liên quan đã thay đổi. Hãy kiểm tra lại kế hoạch xóa.");
        if (!string.Equals(request.Confirmation, impact.RequiredConfirmation, StringComparison.Ordinal))
            throw Error("Nhập đúng mã nhóm và mã WBS để xác nhận xóa vĩnh viễn.");
        if (!impact.CanDelete)
            throw Error("Tệp đính kèm còn trên máy chủ. Hãy xóa tệp trước khi xóa đầu việc.");
        CrmConcurrency.Apply(db, task, request.RowVersion);

        var deletedTasks = new[] { task }.Concat(descendants).ToList();
        var deletedIds = deletedTasks.Select(item => item.Id).ToHashSet();
        foreach (var permit in await db.PermitChecklistItems
                     .Where(item => item.WorkTaskId.HasValue && deletedIds.Contains(item.WorkTaskId.Value)).ToListAsync(ct))
            permit.WorkTaskId = null;
        foreach (var checklist in await db.TenderChecklistItems
                     .Where(item => item.WorkTaskId.HasValue && deletedIds.Contains(item.WorkTaskId.Value)).ToListAsync(ct))
            checklist.WorkTaskId = null;
        db.DepartmentWorkTaskDependencies.RemoveRange(await db.DepartmentWorkTaskDependencies
            .Where(item => deletedIds.Contains(item.WorkTaskId) || deletedIds.Contains(item.PredecessorTaskId)).ToListAsync(ct));
        db.DepartmentWorkTaskCcUsers.RemoveRange(await db.DepartmentWorkTaskCcUsers
            .Where(item => deletedIds.Contains(item.WorkTaskId)).ToListAsync(ct));
        db.DepartmentWorkTaskAttachments.RemoveRange(await db.DepartmentWorkTaskAttachments
            .Where(item => deletedIds.Contains(item.WorkTaskId)).ToListAsync(ct));
        db.EntityTranslations.RemoveRange(await db.EntityTranslations.Where(item =>
            item.EntityType == EntityTypes.DepartmentWorkTask && deletedIds.Contains(item.EntityId)).ToListAsync(ct));
        var package = task.WorkPackage;
        var parentTaskId = task.ParentTaskId;
        var now = DateTime.UtcNow;
        db.DepartmentWorkTasks.RemoveRange(deletedTasks);
        TouchPackage(package, callerUserId, now);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        if (parentTaskId.HasValue)
        {
            await RollupParentTasksAsync(packageId, parentTaskId.Value, callerUserId, ct);
            await CrmConcurrency.SaveChangesAsync(db, ct);
        }
        await AddHistoryAsync(packageId, "Task", taskId, "Deleted",
            new { RootTask = task.WbsCode + " · " + task.Title, DescendantCount = descendants.Count }, callerUserId, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return true;
    }

    public async Task<int> PurgeExpiredDisabledTasksAsync(DateTime utcNow, CancellationToken ct = default)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Purge time must be expressed in UTC.", nameof(utcNow));
        var cutoff = utcNow.AddDays(-30);
        var dueRoots = await db.DepartmentWorkTasks.AsNoTracking()
            .Where(item => item.Status == WorkTaskStatus.Disabled && item.DisabledRootTaskId == item.Id &&
                item.DisabledAt <= cutoff)
            .OrderBy(item => item.DisabledAt).ThenBy(item => item.Id)
            .Select(item => new { item.WorkPackageId, item.Id })
            .Take(20).ToListAsync(ct);
        var purged = 0;
        foreach (var root in dueRoots)
        {
            if (await PurgeExpiredTaskTreeAsync(root.WorkPackageId, root.Id, cutoff, utcNow, ct)) purged++;
        }
        return purged;
    }

    private async Task<bool> PurgeExpiredTaskTreeAsync(
        int packageId, int taskId, DateTime cutoff, DateTime utcNow, CancellationToken ct)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var tasks = await db.DepartmentWorkTasks.Include(item => item.WorkPackage)
            .Where(item => item.WorkPackageId == packageId).ToListAsync(ct);
        var root = tasks.SingleOrDefault(item => item.Id == taskId);
        if (root is null || root.Status != WorkTaskStatus.Disabled || root.DisabledRootTaskId != taskId ||
            !root.DisabledAt.HasValue || root.DisabledAt.Value > cutoff) return false;
        var descendants = GetTaskDescendants(taskId, tasks);
        if (descendants.Any(item => item.Status != WorkTaskStatus.Disabled || item.DisabledRootTaskId != taskId))
            return false;
        var deletedTasks = new[] { root }.Concat(descendants).ToList();
        var deletedIds = deletedTasks.Select(item => item.Id).ToHashSet();
        var attachments = await db.DepartmentWorkTaskAttachments
            .Where(item => deletedIds.Contains(item.WorkTaskId)).ToListAsync(ct);
        foreach (var attachment in attachments.Where(item => item.StorageDeletedAt == null))
        {
            if (!taskStorage.Delete(attachment.Path, BusinessDocumentArea.WorkTasks))
            {
                logger.LogWarning("Task retention purge deferred because attachment cleanup failed. packageId={PackageId} taskId={TaskId} attachmentId={AttachmentId}",
                    packageId, taskId, attachment.Id);
                return false;
            }
            attachment.DeletedAt = attachment.StorageDeletedAt = utcNow;
        }
        if (attachments.Count > 0) await db.SaveChangesAsync(ct);

        foreach (var permit in await db.PermitChecklistItems
                     .Where(item => item.WorkTaskId.HasValue && deletedIds.Contains(item.WorkTaskId.Value)).ToListAsync(ct))
            permit.WorkTaskId = null;
        foreach (var checklist in await db.TenderChecklistItems
                     .Where(item => item.WorkTaskId.HasValue && deletedIds.Contains(item.WorkTaskId.Value)).ToListAsync(ct))
            checklist.WorkTaskId = null;
        db.DepartmentWorkTaskDependencies.RemoveRange(await db.DepartmentWorkTaskDependencies
            .Where(item => deletedIds.Contains(item.WorkTaskId) || deletedIds.Contains(item.PredecessorTaskId)).ToListAsync(ct));
        db.DepartmentWorkTaskCcUsers.RemoveRange(await db.DepartmentWorkTaskCcUsers
            .Where(item => deletedIds.Contains(item.WorkTaskId)).ToListAsync(ct));
        db.DepartmentWorkTaskAttachments.RemoveRange(attachments);
        db.EntityTranslations.RemoveRange(await db.EntityTranslations.Where(item =>
            item.EntityType == EntityTypes.DepartmentWorkTask && deletedIds.Contains(item.EntityId)).ToListAsync(ct));
        db.DepartmentWorkTasks.RemoveRange(deletedTasks);
        TouchPackage(root.WorkPackage, root.DisabledByUserId ?? root.UpdatedByUserId, utcNow);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        await AddHistoryAsync(packageId, "Task", taskId, "PermanentlyDeletedAfterRetention",
            new
            {
                RootTask = root.WbsCode + " · " + root.Title,
                DescendantCount = descendants.Count,
                DisabledAt = root.DisabledAt,
                RetentionDays = 30
            },
            root.DisabledByUserId ?? root.UpdatedByUserId, ct);
        if (root.ParentTaskId.HasValue)
        {
            await RollupParentTasksAsync(packageId, root.ParentTaskId.Value,
                root.DisabledByUserId ?? root.UpdatedByUserId, ct);
            await CrmConcurrency.SaveChangesAsync(db, ct);
        }
        if (transaction is not null) await transaction.CommitAsync(ct);
        logger.LogInformation("Expired disabled task tree purged. packageId={PackageId} taskId={TaskId} descendants={DescendantCount}",
            packageId, taskId, descendants.Count);
        return true;
    }

    public async Task<DeletionImpactResponse?> GetDeletionImpactAsync(int id, int callerUserId, CancellationToken ct = default)
    {
        var entity = await db.DepartmentWorkPackages.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, ct);
        if (entity is null) return null;
        if (!await CanManagePackageAsync(entity, callerUserId, await IsAdministratorAsync(callerUserId, ct), ct)) throw ScopeError();
        return await DeletionImpactPlanner.ForWorkPackageAsync(db, id, ct);
    }

    public async Task<bool> DeleteAsync(int id, ConfirmDeletionRequest request, int callerUserId, CancellationToken ct = default)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var entity = await db.DepartmentWorkPackages
            .Include(item => item.Tasks).ThenInclude(item => item.CcUsers)
            .Include(item => item.ParticipantGroups)
            .Include(item => item.History)
            .Include(item => item.InputPriceOffers).ThenInclude(item => item.Lines)
            .Include(item => item.FinanceReview)
            .AsSplitQuery().SingleOrDefaultAsync(item => item.Id == id, ct);
        if (entity is null) return false;
        var impact = await GetDeletionImpactAsync(id, callerUserId, ct);
        if (!string.Equals(request.PlanToken, impact!.PlanToken, StringComparison.Ordinal))
            throw new DeletionPlanChangedException("Dữ liệu liên quan đã thay đổi. Hãy kiểm tra lại kế hoạch xóa.");
        if (!string.Equals(request.Confirmation, impact.RequiredConfirmation, StringComparison.Ordinal))
            throw Error("Nhập đúng mã nhóm công việc để xác nhận xóa vĩnh viễn.");
        if (!impact.CanDelete) throw Error("Nhóm công việc còn hồ sơ hoặc tệp đang được sử dụng. Hãy xử lý các mục chặn trước khi xóa.");
        CrmConcurrency.Apply(db, entity, request.RowVersion);
        var taskIds = db.DepartmentWorkTasks.Where(item => item.WorkPackageId == id).Select(item => item.Id);
        foreach (var permit in await db.PermitChecklistItems.Where(item => item.WorkTaskId.HasValue && taskIds.Contains(item.WorkTaskId.Value)).ToListAsync(ct))
            permit.WorkTaskId = null;
        foreach (var checklist in await db.TenderChecklistItems.Where(item => item.WorkTaskId.HasValue && taskIds.Contains(item.WorkTaskId.Value)).ToListAsync(ct))
            checklist.WorkTaskId = null;
        // An uploaded BOQ belongs to the project and outlives the package it came from.
        foreach (var revision in await db.ProjectBoqRevisions.Where(item => item.SourceWorkPackageId == id).ToListAsync(ct))
            revision.SourceWorkPackageId = null;
        foreach (var document in await DeletionImpactPlanner.WorkPackageDocuments(db, id).ToListAsync(ct))
        {
            document.SourceEntityType = null;
            document.SourceRecordId = null;
        }
        db.DepartmentWorkTaskDependencies.RemoveRange(await db.DepartmentWorkTaskDependencies.Where(item => taskIds.Contains(item.WorkTaskId) || taskIds.Contains(item.PredecessorTaskId)).ToListAsync(ct));
        db.DepartmentWorkTaskAttachments.RemoveRange(await db.DepartmentWorkTaskAttachments
            .Where(item => taskIds.Contains(item.WorkTaskId)).ToListAsync(ct));
        db.EntityTranslations.RemoveRange(await db.EntityTranslations.Where(item =>
            (item.EntityType == EntityTypes.DepartmentWorkPackage && item.EntityId == id) ||
            (item.EntityType == EntityTypes.DepartmentWorkTask && taskIds.Contains(item.EntityId))).ToListAsync(ct));
        db.DepartmentWorkPackages.Remove(entity);
        db.WorkPackageApprovalEmails.RemoveRange(await db.WorkPackageApprovalEmails.Where(item => item.WorkPackageId == id).ToListAsync(ct));
        await CrmConcurrency.SaveChangesAsync(db, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return true;
    }
}
