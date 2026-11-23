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
        return await DeletionImpactPlanner.ForWorkTaskAsync(db, packageId, taskId, ct);
    }

    public async Task<bool> DeleteTaskAsync(
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
        await EnsureTenderIsMutableAsync(task.WorkPackage, ct);
        if (task.WorkPackage.Status is WorkPackageStatus.Completed or WorkPackageStatus.Cancelled)
            throw Error("Không thể xóa đầu việc trong nhóm đã kết thúc.");
        var impact = await DeletionImpactPlanner.ForWorkTaskAsync(db, packageId, taskId, ct);
        if (!string.Equals(request.PlanToken, impact!.PlanToken, StringComparison.Ordinal))
            throw new DeletionPlanChangedException("Dữ liệu liên quan đã thay đổi. Hãy kiểm tra lại kế hoạch xóa.");
        if (!string.Equals(request.Confirmation, impact.RequiredConfirmation, StringComparison.Ordinal))
            throw Error("Nhập đúng mã nhóm và mã WBS để xác nhận xóa vĩnh viễn.");
        if (!impact.CanDelete)
            throw Error("Tệp đính kèm còn trên máy chủ. Hãy xóa tệp trước khi xóa đầu việc.");
        CrmConcurrency.Apply(db, task, request.RowVersion);

        var descendants = GetTaskDescendants(taskId, tasks);
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
