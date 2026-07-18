using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>
/// Thrown for expected business-rule violations in the Concept workflow.
/// Controller converts to HTTP 400.
/// </summary>
public class ConceptOptionOperationException(string message) : Exception(message)
{
}

/// <summary>
/// M2 Concept option service (NIH-114). Owns the option lifecycle + the
/// finalize workflow that flips one option to Finalized, discards the
/// remaining active options, and unlocks the parent
/// <see cref="Models.DesignProject"/>'s Basic Design stage.
///
/// Deferred to slice 2: media uploads, feedback threads, PDF export.
/// </summary>
public interface IConceptOptionService
{
    Task<ConceptOptionListResponse> ListAsync(ConceptOptionListParams parameters, CancellationToken ct = default);

    Task<ConceptOptionResponse?> GetAsync(int id, CancellationToken ct = default);

    Task<ConceptOptionResponse> CreateAsync(CreateConceptOptionRequest request, int callerUserId, CancellationToken ct = default);

    Task<ConceptOptionResponse?> UpdateAsync(int id, UpdateConceptOptionRequest request, int callerUserId, CancellationToken ct = default);

    /// <summary>
    /// Delete a concept option. Only Drafting rows can be hard-deleted; once
    /// a row has been PresentedToClient the operator must Discard it instead
    /// so the audit trail is preserved. Returns <c>false</c> when the row
    /// does not exist.
    /// </summary>
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Transition the option to a new status. Enforces the state machine +
    /// the "at most one Finalized per project" invariant. When the target
    /// is <c>Finalized</c>, the sibling options are auto-Discarded and the
    /// parent design project moves to <c>BasicDesign</c>.
    /// </summary>
    Task<ConceptOptionResponse?> TransitionStatusAsync(int id, TransitionConceptOptionStatusRequest request, int callerUserId, CancellationToken ct = default);
}
