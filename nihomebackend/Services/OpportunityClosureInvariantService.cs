using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;

namespace NihomeBackend.Services;

public interface IOpportunityClosureInvariantService
{
    Task<bool> HasQualifyingContractAsync(int opportunityId, int customerId, int? excludingContractId = null, CancellationToken ct = default);
    Task EnsureContractMutationPreservesWonAsync(Contract current, int? newOpportunityId, int newCustomerId, ContractStatus newStatus, DateTime? newSignedDate, bool deleting, CancellationToken ct = default);
}

public sealed class OpportunityClosureInvariantService(AppDbContext db) : IOpportunityClosureInvariantService
{
    public Task<bool> HasQualifyingContractAsync(
        int opportunityId,
        int customerId,
        int? excludingContractId = null,
        CancellationToken ct = default) => db.Contracts.AsNoTracking().AnyAsync(contract =>
            contract.OpportunityId == opportunityId &&
            contract.CustomerId == customerId &&
            contract.SignedDate.HasValue &&
            contract.Status != ContractStatus.Draft &&
            contract.Status != ContractStatus.Cancelled &&
            (!excludingContractId.HasValue || contract.Id != excludingContractId.Value), ct);

    public async Task EnsureContractMutationPreservesWonAsync(
        Contract current,
        int? newOpportunityId,
        int newCustomerId,
        ContractStatus newStatus,
        DateTime? newSignedDate,
        bool deleting,
        CancellationToken ct = default)
    {
        if (!current.OpportunityId.HasValue) return;
        var wasQualifying = current.SignedDate.HasValue &&
            current.Status is not ContractStatus.Draft and not ContractStatus.Cancelled;
        if (!wasQualifying) return;

        var opportunity = await db.Opportunities.AsNoTracking()
            .Where(item => item.Id == current.OpportunityId.Value)
            .Select(item => new { item.Id, item.CustomerId, item.Stage })
            .SingleOrDefaultAsync(ct);
        if (opportunity is null || opportunity.Stage != OpportunityStage.Won || opportunity.CustomerId != current.CustomerId) return;

        var remainsQualifying = !deleting &&
            newOpportunityId == opportunity.Id &&
            newCustomerId == opportunity.CustomerId &&
            newSignedDate.HasValue &&
            newStatus is not ContractStatus.Draft and not ContractStatus.Cancelled;
        if (remainsQualifying) return;
        if (await HasQualifyingContractAsync(opportunity.Id, opportunity.CustomerId, current.Id, ct)) return;

        throw new ContractValidationException(
            "Không thể thay đổi hợp đồng hợp lệ cuối cùng của cơ hội Đã thắng. Hãy duy trì ít nhất một hợp đồng cùng khách hàng, đã ký và không ở trạng thái Nháp/Đã hủy.");
    }
}