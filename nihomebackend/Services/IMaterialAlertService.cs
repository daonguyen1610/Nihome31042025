using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public interface IMaterialAlertService
{
    Task<MaterialAlertListResponse> ListAsync(int projectId, MaterialAlertListParams parameters, CancellationToken ct = default);
    Task<MaterialAlertResponse?> GetAsync(int projectId, int id, CancellationToken ct = default);
    Task<IReadOnlyList<MaterialAlertResponse>> EvaluateProjectAsync(int projectId, int actorUserId, CancellationToken ct = default);
    Task<MaterialAlertResponse?> AcknowledgeAsync(int projectId, int id, AcknowledgeMaterialAlertRequest request, int actorUserId, CancellationToken ct = default);
}

public sealed class MaterialAlertOperationException(string message) : Exception(message);
