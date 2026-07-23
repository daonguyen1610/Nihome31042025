using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>Thrown for expected business-rule violations in the as-built workflow (HTTP 400).</summary>
public class AsBuiltDocumentOperationException(string message) : Exception(message)
{
}

/// <summary>
/// NIH-145 M4 as-built dossier service. Owns
/// <see cref="Models.AsBuiltDocument"/> lifecycle:
/// Draft → Submitted → Approved → Archived (with Cancelled as
/// soft-remove and Draft ↔ Submitted revise loop).
///
/// Approval is a permission-gated action (see controller), so the
/// service exposes <see cref="TransitionAsync"/> for non-approving
/// transitions and <see cref="ApproveAsync"/> for the approve gate.
/// Archive is only allowed from Approved.
/// </summary>
public interface IAsBuiltDocumentService
{
    Task<AsBuiltDocumentListResponse> ListAsync(AsBuiltDocumentListParams parameters, CancellationToken ct = default);

    Task<AsBuiltDocumentResponse?> GetAsync(int id, CancellationToken ct = default);

    Task<AsBuiltDocumentResponse> CreateAsync(CreateAsBuiltDocumentRequest request, int callerUserId, CancellationToken ct = default);

    Task<AsBuiltDocumentResponse?> UpdateAsync(int id, UpdateAsBuiltDocumentRequest request, int callerUserId, CancellationToken ct = default);

    /// <summary>Non-approving transitions: Submit / Revise / Archive / Cancel. Refuses Approve.</summary>
    Task<AsBuiltDocumentResponse?> TransitionAsync(int id, TransitionAsBuiltStatusRequest request, int callerUserId, CancellationToken ct = default);

    /// <summary>Dedicated approve endpoint — separate permission gate.</summary>
    Task<AsBuiltDocumentResponse?> ApproveAsync(int id, TransitionAsBuiltStatusRequest request, int callerUserId, CancellationToken ct = default);

    Task<bool> DeleteAsync(int id, CancellationToken ct = default);

    Task<AsBuiltDocumentBulkDeleteResponse> BulkDeleteAsync(BulkDeleteAsBuiltDocumentsRequest request, CancellationToken ct = default);
}
