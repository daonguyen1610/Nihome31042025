using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public interface IHseViolationService
{
    Task<HseViolationListResponse> ListAsync(int projectId, HseViolationListParams parameters, bool includeSensitive, CancellationToken ct = default);
    Task<HseViolationResponse?> GetAsync(int projectId, int id, bool includeSensitive, CancellationToken ct = default);
    Task<HseViolationResponse> CreateAsync(int projectId, CreateHseViolationRequest request, int callerUserId, CancellationToken ct = default);
    Task<HseViolationResponse?> UpdateAsync(int projectId, int id, UpdateHseViolationRequest request, int callerUserId, CancellationToken ct = default);
    Task<HseViolationResponse?> TransitionAsync(int projectId, int id, HseViolationStatus next, TransitionHseViolationRequest request, int callerUserId, CancellationToken ct = default);
    Task<HseViolationResponse?> CorrectAsync(int projectId, int id, CorrectHseViolationRequest request, int callerUserId, CancellationToken ct = default);
}

public sealed class HseViolationOperationException(string message) : Exception(message);