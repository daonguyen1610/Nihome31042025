using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public interface IContractService
{
    Task<ContractListResponse> ListAsync(
        int callerUserId,
        bool canSeeAll,
        ContractStatus? status = null,
        int? ownerUserId = null,
        int? customerId = null,
        string? search = null,
        DateTime? signedFrom = null,
        DateTime? signedTo = null,
        decimal? valueMin = null,
        decimal? valueMax = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default);

    Task<ContractResponse?> GetAsync(int id, int callerUserId, bool canSeeAll, CancellationToken ct = default);

    Task<ContractResponse> CreateAsync(
        UpsertContractRequest req, int callerUserId, bool canReassignOwner, CancellationToken ct = default);

    Task<ContractResponse?> UpdateAsync(
        int id, UpsertContractRequest req, int callerUserId, bool canSeeAll, bool canReassignOwner, CancellationToken ct = default);

    Task<bool> DeleteAsync(int id, int callerUserId, bool canSeeAll, CancellationToken ct = default);

    /// <summary>Transition the contract to <paramref name="newStatus"/>.
    /// Rejects illegal transitions and pre-conditions (e.g. missing signed
    /// scan when moving Signed → InProgress, unpaid milestones when
    /// closing to Completed).</summary>
    Task<ContractResponse?> TransitionStatusAsync(
        int id, ContractStatus newStatus, int callerUserId, bool canSeeAll, CancellationToken ct = default);

    /// <summary>Update a single milestone's status (Pending / Requested /
    /// Paid). Returns the refreshed contract response, or <c>null</c> if
    /// the contract or milestone is not found / not owned.</summary>
    Task<ContractResponse?> UpdateMilestoneStatusAsync(
        int contractId, int milestoneId, PaymentMilestoneStatus newStatus,
        int callerUserId, bool canSeeAll, CancellationToken ct = default);
}

/// <summary>Thrown when the caller submits a contract number that already
/// exists on a different row.</summary>
public class ContractDuplicateNumberException(string contractNumber)
    : InvalidOperationException($"Contract number '{contractNumber}' already exists.")
{
    public string ContractNumber { get; } = contractNumber;
}

public class ContractValidationException(string message) : InvalidOperationException(message)
{
}
