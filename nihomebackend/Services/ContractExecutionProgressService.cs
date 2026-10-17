using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public sealed class ContractExecutionProgressService(AppDbContext db, IDepartmentWorkPackageService workPackages,
    IPermissionService permissions, IProjectAccessService projectAccess)
{
    // Caller must first enforce the existing Contract read scope.
    public async Task<ContractExecutionProgressResponse> GetAsync(ContractResponse contract, int userId, CancellationToken ct)
    {
        var result = new ContractExecutionProgressResponse { CalculatedAt = DateTime.UtcNow };
        if (await permissions.HasAsync(userId, "work.packages.view", ct))
            result.Work = await workPackages.GetContractProgressAsync(contract.Id, userId,
                await permissions.HasAsync(userId, "work.packages.view.all", ct), ct);

        if (contract.OperationalProjectId is int projectId &&
            await permissions.HasAsync(userId, "construction.qs.view", ct) &&
            await projectAccess.CanViewOperationalProjectAsync(userId, projectId, ct))
        {
            var direction = contract.Direction == ContractDirection.Upstream ? ConstructionQsDirection.Upstream : ConstructionQsDirection.Downstream;
            var query = db.ConstructionQsBatches.AsNoTracking().Where(item => item.ContractId == contract.Id &&
                item.OperationalProjectId == projectId && item.Direction == direction);
            var total = await query.CountAsync(ct);
            var approved = await query.CountAsync(item => item.Status == ConstructionReviewStatus.CmApproved &&
                item.RecordState == ConstructionQsRecordState.Active && item.IsActiveLineage &&
                item.AdjustmentKind != ConstructionQsAdjustmentKind.Reversal, ct);
            var pending = await query.CountAsync(item => item.Status == ConstructionReviewStatus.PendingCmReview, ct);
            var sources = await query.OrderByDescending(item => item.UpdatedAt).ThenByDescending(item => item.Id).Take(10)
                .Select(item => new { item.Id, item.Code, item.Description, item.Status }).ToListAsync(ct);
            result.Qs = new(total, approved, pending, sources.Select(item =>
                new ContractProgressSource(item.Id, item.Code, item.Description, item.Status.ToString())).ToList());
        }
        var milestones = await db.ContractPaymentMilestones.AsNoTracking().Where(item => item.ContractId == contract.Id)
            .Select(item => new { item.Status, item.PercentValue }).ToListAsync(ct);
        result.Payment = new(milestones.Count, milestones.Count(item => item.Status == PaymentMilestoneStatus.Paid),
            milestones.Count(item => item.Status == PaymentMilestoneStatus.Requested),
            milestones.Sum(item => Math.Round(contract.Value * item.PercentValue / 100m, 2)),
            milestones.Where(item => item.Status == PaymentMilestoneStatus.Paid).Sum(item => Math.Round(contract.Value * item.PercentValue / 100m, 2)));
        return result;
    }
}
