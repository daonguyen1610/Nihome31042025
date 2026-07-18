using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>
/// Thrown for expected business-rule violations in the permit workflow.
/// Controller converts to HTTP 400.
/// </summary>
public class PermitChecklistOperationException(string message) : Exception(message)
{
}

/// <summary>
/// NIH-137 M3 Permitting service. Owns the checklist that lives against a
/// <see cref="Models.DesignProject"/>: idempotent auto-generation from the
/// master template + patch-style updates + a company-wide risk view.
///
/// Slice 2 (deferred) layers file uploads (submitted package + issued
/// permit scans), <c>PermitActivity</c> timeline entries, the daily
/// expiry cron, and the attach-from-basic-design bridge to NIH-115.
/// </summary>
public interface IPermitChecklistService
{
    /// <summary>
    /// Idempotent auto-generation: for the given design project make sure
    /// every permit type from the master-data catalogue has a corresponding
    /// checklist row. Existing rows are left untouched so operator edits
    /// (agency, owner, status …) survive a re-run.
    /// </summary>
    Task EnsureForProjectAsync(int designProjectId, int? callerUserId, CancellationToken ct = default);

    Task<PermitChecklistListResponse> ListAsync(PermitChecklistListParams parameters, CancellationToken ct = default);

    Task<PermitChecklistItemResponse?> GetAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Patch a single checklist row. Returns <c>null</c> when the row does not
    /// exist. Throws <see cref="PermitChecklistOperationException"/> when the
    /// caller sends an invalid status or violates the "Issued needs IssuedAt"
    /// rule.
    /// </summary>
    Task<PermitChecklistItemResponse?> UpdateAsync(int id, UpdatePermitChecklistItemRequest request, int callerUserId, CancellationToken ct = default);
}
