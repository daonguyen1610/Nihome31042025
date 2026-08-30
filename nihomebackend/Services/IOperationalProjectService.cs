using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public sealed class OperationalProjectOperationException(string message) : Exception(message);

public interface IOperationalProjectService
{
    Task<OperationalProjectListResponse> ListAsync(
        OperationalProjectListParams parameters,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default);

    Task<OperationalProjectResponse?> GetAsync(
        int id,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default);

    Task<IReadOnlyList<OperationalProjectTimelineItemResponse>?> GetTimelineAsync(
        int id,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default);

    Task<OperationalProjectResponse> CreateAsync(
        CreateOperationalProjectRequest request,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default);

    Task<OperationalProjectResponse?> UpdateAsync(
        int id,
        UpdateOperationalProjectRequest request,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default);

    Task<bool> DeleteAsync(
        int id,
        int callerUserId,
        bool canSeeAll,
        string? rowVersion,
        CancellationToken ct = default);
}
