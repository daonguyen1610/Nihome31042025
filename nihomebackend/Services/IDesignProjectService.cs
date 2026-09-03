using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>
/// Thrown for expected business-rule violations in the DesignProject
/// workflow. Controller converts to HTTP 400.
/// </summary>
public class DesignProjectOperationException(string message) : Exception(message)
{
}

/// <summary>
/// M2 DesignProject (Dự án thiết kế) service — NIH-113 overview slice.
/// Owns CRUD + the auto-create hook fired by
/// <see cref="IContractService"/> when a contract transitions to
/// <see cref="ContractStatus.InProgress"/>. Per-stage documents (Concept /
/// Basic / Shop Drawing / Revision / IFC) and the team roster ship in
/// NIH-114..118 and are layered on top of this interface.
/// </summary>
public interface IDesignProjectService
{
    Task<DesignProjectListResponse> ListAsync(DesignProjectListParams parameters, int callerUserId, CancellationToken ct = default);

    Task<DesignProjectResponse?> GetAsync(int id, CancellationToken ct = default);

    Task<DesignProjectResponse> CreateAsync(CreateDesignProjectRequest request, int callerUserId, CancellationToken ct = default);

    Task<DesignProjectResponse?> UpdateAsync(int id, UpdateDesignProjectRequest request, int callerUserId, CancellationToken ct = default);

    Task<DeletionImpactResponse?> GetDeletionImpactAsync(int id, CancellationToken ct = default);

    Task<bool> DeleteAsync(
        int id,
        ConfirmDeletionRequest request,
        int callerUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Idempotent auto-create hook. Called by <see cref="IContractService"/>
    /// when a contract moves to <see cref="ContractStatus.InProgress"/>.
    /// Skips when a design project is already linked to the contract so a
    /// double-click on the transition button does not clone the row.
    /// Returns the linked project (existing or new) so the caller can log.
    /// </summary>
    Task<DesignProjectResponse> EnsureForContractAsync(Contract contract, int? callerUserId, CancellationToken ct = default);
}
