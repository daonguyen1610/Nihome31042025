using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>
/// M4 Construction schedule (Tiến độ Gantt / NIH-141) service — owns
/// CRUD, predecessors, progress updates, status transitions and bulk
/// delete for <see cref="Models.ConstructionTask"/>.
/// </summary>
public interface IConstructionTaskService
{
    Task<ConstructionTaskListResponse> ListAsync(ConstructionTaskListParams parameters, CancellationToken ct = default);
    Task<ConstructionTaskResponse?> GetAsync(int id, CancellationToken ct = default);
    Task<ConstructionTaskResponse> CreateAsync(CreateConstructionTaskRequest request, int callerUserId, CancellationToken ct = default);
    Task<ConstructionTaskResponse?> UpdateAsync(int id, UpdateConstructionTaskRequest request, int callerUserId, CancellationToken ct = default);
    Task<ConstructionTaskResponse?> UpdateProgressAsync(int id, UpdateConstructionTaskProgressRequest request, int callerUserId, CancellationToken ct = default);
    Task<ConstructionTaskResponse> SetPredecessorsAsync(int id, SetConstructionTaskPredecessorsRequest request, int callerUserId, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
    Task<ConstructionTaskBulkDeleteResponse> BulkDeleteAsync(IReadOnlyList<int> ids, CancellationToken ct = default);
}

public class ConstructionTaskOperationException(string message) : Exception(message);
