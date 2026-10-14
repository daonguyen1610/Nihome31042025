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

public class PermitChecklistDuplicateException(string message) : Exception(message)
{
}

public enum PermitDocumentKind
{
    SubmittedPackage,
    IssuedPermit,
}

/// <summary>
/// Permitting service for authority status, documents and portfolio risk.
/// OperationalProject owns each permit; the linked DepartmentWorkTask owns
/// internal assignment, schedule, progress and approval.
/// </summary>
public interface IPermitChecklistService
{
    Task<PermitChecklistListResponse> ListAsync(
        PermitChecklistListParams parameters,
        CancellationToken ct = default,
        IReadOnlySet<int>? accessibleOperationalProjectIds = null);

    Task<PermitChecklistItemResponse?> GetAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<PermitChecklistHistoryResponse>> HistoryAsync(int id, CancellationToken ct = default);

    Task<PermitChecklistItemResponse> CreateAsync(CreatePermitChecklistItemRequest request, int callerUserId, CancellationToken ct = default);

    /// <summary>
    /// Patch a single checklist row. Returns <c>null</c> when the row does not
    /// exist. Throws <see cref="PermitChecklistOperationException"/> when the
    /// caller sends an invalid status or violates the "Issued needs IssuedAt"
    /// rule.
    /// </summary>
    Task<PermitChecklistItemResponse?> UpdateAsync(int id, UpdatePermitChecklistItemRequest request, int callerUserId, CancellationToken ct = default);

    Task<PermitChecklistItemResponse?> UploadDocumentAsync(
        int id,
        PermitDocumentKind kind,
        IFormFile? file,
        int callerUserId,
        CancellationToken ct = default);

    Task<PermitChecklistItemResponse?> DeleteAsync(int id, CancellationToken ct = default);
}
