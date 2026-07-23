using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>Thrown for expected business-rule violations in the acceptance workflow (converted to HTTP 400).</summary>
public class AcceptanceRecordOperationException(string message) : Exception(message)
{
}

/// <summary>
/// NIH-143 M4 partial acceptance service. Owns the
/// <see cref="Models.AcceptanceRecord"/> lifecycle:
/// Draft → Submitted → Approved / Rejected → Draft (revision) → Cancelled.
///
/// Approval is a permission-gated action (see
/// <see cref="Controllers.AcceptanceRecordsController.Approve"/>) so
/// the service just enforces state transitions and records the caller.
/// </summary>
public interface IAcceptanceRecordService
{
    Task<AcceptanceRecordListResponse> ListAsync(AcceptanceRecordListParams parameters, CancellationToken ct = default);

    Task<AcceptanceRecordResponse?> GetAsync(int id, CancellationToken ct = default);

    Task<AcceptanceRecordResponse> CreateAsync(CreateAcceptanceRecordRequest request, int callerUserId, CancellationToken ct = default);

    Task<AcceptanceRecordResponse?> UpdateAsync(int id, UpdateAcceptanceRecordRequest request, int callerUserId, CancellationToken ct = default);

    /// <summary>Non-approving transitions: Submit / Reject / Revise / Cancel. Refuses Approve.</summary>
    Task<AcceptanceRecordResponse?> TransitionAsync(int id, TransitionAcceptanceStatusRequest request, int callerUserId, CancellationToken ct = default);

    /// <summary>Dedicated approve endpoint — separate permission gate.</summary>
    Task<AcceptanceRecordResponse?> ApproveAsync(int id, TransitionAcceptanceStatusRequest request, int callerUserId, CancellationToken ct = default);

    Task<bool> DeleteAsync(int id, CancellationToken ct = default);

    Task<AcceptanceRecordBulkDeleteResponse> BulkDeleteAsync(BulkDeleteAcceptanceRecordsRequest request, CancellationToken ct = default);
}
