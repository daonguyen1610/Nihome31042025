using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public sealed class DepartmentWorkPackageOperationException(string message) : Exception(message);
public sealed class DepartmentWorkPackageScopeException(string message) : Exception(message);

public interface IDepartmentWorkPackageService
{
    Task<ContractWorkProgress> GetContractProgressAsync(int contractId, int callerUserId, bool canViewAll, CancellationToken ct = default);
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
    Task<InputPriceOfferResponse> AddInputPriceOfferAsync(int packageId, UpsertInputPriceOfferRequest request, int callerUserId, CancellationToken ct = default);
    Task<InputPriceOfferResponse?> UpdateInputPriceOfferAsync(int packageId, int offerId, UpsertInputPriceOfferRequest request, int callerUserId, CancellationToken ct = default);
    Task<InputPriceOfferResponse?> SubmitInputPriceOfferAsync(int packageId, int offerId, SubmitBusinessRecordRequest request, int callerUserId, CancellationToken ct = default);
    Task<InputPriceOfferResponse?> DecideInputPriceOfferAsync(int packageId, int offerId, DecideBusinessRecordRequest request, int callerUserId, CancellationToken ct = default);
    Task<FinanceReviewResponse> SaveFinanceReviewAsync(int packageId, UpsertFinanceReviewRequest request, int callerUserId, CancellationToken ct = default);
    Task<FinanceReviewResponse?> SubmitFinanceReviewAsync(int packageId, SubmitBusinessRecordRequest request, int callerUserId, CancellationToken ct = default);
    Task<FinanceReviewResponse?> DecideFinanceReviewAsync(int packageId, DecideBusinessRecordRequest request, int callerUserId, CancellationToken ct = default);
}
