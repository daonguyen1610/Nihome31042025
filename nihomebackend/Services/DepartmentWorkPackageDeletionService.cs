using System.Data;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public sealed partial class DepartmentWorkPackageService
{
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
        foreach (var document in await DeletionImpactPlanner.WorkPackageDocuments(db, id).ToListAsync(ct))
        {
            document.SourceEntityType = null;
            document.SourceRecordId = null;
        }
        db.DepartmentWorkTaskDependencies.RemoveRange(await db.DepartmentWorkTaskDependencies.Where(item => taskIds.Contains(item.WorkTaskId) || taskIds.Contains(item.PredecessorTaskId)).ToListAsync(ct));
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
