using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>CRUD + approval workflow for a contract's Variation Orders
/// (<see cref="ContractAppendix"/>). Owning contract's <c>CurrentValue</c>
/// updates through <see cref="ContractService.GetAsync"/>; no separate
/// write is needed because Σ Approved is computed on read.</summary>
public interface IContractAppendixService
{
    Task<List<ContractAppendixResponse>?> ListAsync(int contractId, int callerUserId, bool canSeeAll, CancellationToken ct = default);
    Task<ContractAppendixResponse?> GetAsync(int contractId, int voId, int callerUserId, bool canSeeAll, CancellationToken ct = default);
    Task<ContractAppendixResponse?> CreateAsync(int contractId, UpsertContractAppendixRequest req, int callerUserId, bool canSeeAll, CancellationToken ct = default);
    Task<ContractAppendixResponse?> UpdateAsync(int contractId, int voId, UpsertContractAppendixRequest req, int callerUserId, bool canSeeAll, CancellationToken ct = default);
    Task<ContractAppendixResponse?> SubmitAsync(int contractId, int voId, int callerUserId, bool canSeeAll, CancellationToken ct = default);
    Task<ContractAppendixResponse?> ApproveAsync(int contractId, int voId, string? note, int callerUserId, bool canSeeAll, CancellationToken ct = default);
    Task<ContractAppendixResponse?> RejectAsync(int contractId, int voId, string? note, int callerUserId, bool canSeeAll, CancellationToken ct = default);
    Task<bool> DeleteAsync(int contractId, int voId, int callerUserId, bool canSeeAll, CancellationToken ct = default);
}
