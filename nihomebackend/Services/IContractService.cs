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
        UpsertContractRequest req, int callerUserId, CancellationToken ct = default);

    Task<ContractResponse?> UpdateAsync(
        int id, UpsertContractRequest req, int callerUserId, bool canSeeAll, CancellationToken ct = default);

    Task<bool> DeleteAsync(int id, int callerUserId, bool canSeeAll, CancellationToken ct = default);
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
