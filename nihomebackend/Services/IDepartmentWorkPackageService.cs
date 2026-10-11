using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public sealed class DepartmentWorkPackageOperationException(string message) : Exception(message);
public sealed class DepartmentWorkPackageScopeException(string message) : Exception(message);

public interface IDepartmentWorkPackageService
{
    Task<DepartmentWorkPackageListResponse> ListAsync(DepartmentWorkPackageQuery query, int callerUserId, bool canViewAll, CancellationToken ct = default);
    Task<DepartmentWorkPackageResponse?> GetAsync(int id, int callerUserId, bool canViewAll, CancellationToken ct = default);
    Task<DepartmentWorkPackageOptionsResponse> OptionsAsync(int callerUserId, CancellationToken ct = default);
    Task<IReadOnlyList<DepartmentWorkPackageHistoryResponse>?> HistoryAsync(int id, int callerUserId, bool canViewAll, CancellationToken ct = default);
    Task<DepartmentWorkPackageResponse> CreateAsync(CreateDepartmentWorkPackageRequest request, int callerUserId, CancellationToken ct = default);
    Task<DepartmentWorkPackageResponse?> UpdateAsync(int id, UpdateDepartmentWorkPackageRequest request, int callerUserId, CancellationToken ct = default);
    Task<DepartmentWorkTaskResponse> AddTaskAsync(int packageId, UpsertDepartmentWorkTaskRequest request, int callerUserId, CancellationToken ct = default);
    Task<DepartmentWorkTaskResponse?> UpdateTaskAsync(int packageId, int taskId, UpsertDepartmentWorkTaskRequest request, int callerUserId, CancellationToken ct = default);
    Task<DepartmentWorkTaskResponse?> UpdateProgressAsync(int packageId, int taskId, UpdateDepartmentWorkTaskProgressRequest request, int callerUserId, CancellationToken ct = default);
    Task<DepartmentWorkTaskResponse?> VerifyTaskAsync(int packageId, int taskId, VerifyDepartmentWorkTaskRequest request, int callerUserId, CancellationToken ct = default);
}
