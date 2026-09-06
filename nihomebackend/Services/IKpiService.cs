using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public sealed class KpiOperationException(string message) : Exception(message);

public interface IKpiService
{
    Task<IReadOnlyList<KpiUserOptionResponse>> ListEligibleUsersAsync(CancellationToken ct = default);
    Task<IReadOnlyList<KpiDefinitionResponse>> ListDefinitionsAsync(CancellationToken ct = default);
    Task<KpiDefinitionResponse?> UpdateDefinitionAsync(int id, UpdateKpiDefinitionRequest request, int callerUserId, CancellationToken ct = default);
    Task<KpiDashboardResponse> CalculateAsync(int year, int month, int userId, int callerUserId, CancellationToken ct = default);
    Task<KpiDashboardResponse?> GetDashboardAsync(int year, int month, int userId, CancellationToken ct = default);
    Task<KpiDashboardResponse?> LockAsync(int year, int month, LockKpiPeriodRequest request, int callerUserId, CancellationToken ct = default);
}
