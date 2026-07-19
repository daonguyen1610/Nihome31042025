using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>
/// M2 Drawing Revision service (NIH-117 slice 1). Owns:
/// <list type="bullet">
///   <item>List revision history for a single drawing target.</item>
///   <item>Create a new revision (auto-numbered, flips previous to superseded).</item>
///   <item>Metadata-only diff between two revisions of the same target.</item>
/// </list>
/// Revisions are append-only — no update / no delete endpoints exist by
/// design.
/// </summary>
public interface IDrawingRevisionService
{
    Task<DrawingRevisionListResponse> ListAsync(DrawingRevisionListParams parameters, CancellationToken ct = default);
    Task<DrawingRevisionResponse?> GetAsync(int id, CancellationToken ct = default);
    Task<DrawingRevisionResponse> CreateAsync(CreateDrawingRevisionRequest request, int callerUserId, CancellationToken ct = default);
    Task<DrawingRevisionDiffResponse?> DiffAsync(int fromId, int toId, CancellationToken ct = default);
}

/// <summary>Domain-level operation failure — controllers translate to 400.</summary>
public class DrawingRevisionOperationException(string message) : Exception(message);
