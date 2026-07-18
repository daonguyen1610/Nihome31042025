using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>
/// Thrown for expected business-rule violations in the Basic Design workflow.
/// Controller converts to HTTP 400.
/// </summary>
public class BasicDesignDocOperationException(string message) : Exception(message)
{
}

/// <summary>
/// M2 Basic Design service (NIH-115). Owns document CRUD + status
/// transitions + the "3-discipline internal approval" gate that unlocks
/// the parent <see cref="Models.DesignProject"/>'s Shop Drawing stage.
///
/// Deferred to slice 2: file uploads, per-discipline default checklist
/// templates, attach-to-permit bridge with NIH-137, notification for
/// "hồ sơ chờ duyệt".
/// </summary>
public interface IBasicDesignDocService
{
    Task<BasicDesignDocListResponse> ListAsync(BasicDesignDocListParams parameters, CancellationToken ct = default);

    Task<BasicDesignDocResponse?> GetAsync(int id, CancellationToken ct = default);

    Task<BasicDesignDocResponse> CreateAsync(CreateBasicDesignDocRequest request, int callerUserId, CancellationToken ct = default);

    Task<BasicDesignDocResponse?> UpdateAsync(int id, UpdateBasicDesignDocRequest request, int callerUserId, CancellationToken ct = default);

    /// <summary>Delete a document. Only <c>InProgress</c> rows can be hard-deleted.</summary>
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);

    /// <summary>Transition to the next status, enforcing the state machine.</summary>
    Task<BasicDesignDocResponse?> TransitionStatusAsync(int id, TransitionBasicDesignDocStatusRequest request, int callerUserId, CancellationToken ct = default);

    /// <summary>
    /// Push the parent design project from BasicDesign to ShopDrawing.
    /// Blocked when the readiness gate (≥1 InternallyApproved per required
    /// discipline) is not met.
    /// </summary>
    Task<Models.DTOs.Responses.DesignProjectResponse> UnlockShopDrawingAsync(int designProjectId, int callerUserId, CancellationToken ct = default);
}
