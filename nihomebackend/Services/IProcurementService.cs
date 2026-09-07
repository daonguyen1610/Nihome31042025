using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public interface IProcurementService
{
    Task<ProcurementWorkspaceResponse> GetWorkspaceAsync(int projectId, CancellationToken ct = default);
    Task<MaterialRequestListResponse> ListMaterialRequestsAsync(int projectId, MaterialRequestListParams parameters, CancellationToken ct = default);
    Task<ProjectBoqRevisionListResponse> ListBoqRevisionsAsync(int projectId, ProjectBoqRevisionListQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<ProjectBoqRevisionListItemResponse>> ExportBoqRevisionsAsync(int projectId, ProjectBoqRevisionListQuery query, CancellationToken ct = default);
    Task<ProjectBoqRevisionResponse> CreateBoqRevisionAsync(int projectId, ProjectBoqRevisionRequest request, int userId, CancellationToken ct = default);
    Task<ProjectBoqRevisionResponse?> UpdateBoqRevisionAsync(int projectId, int id, ProjectBoqRevisionRequest request, int userId, CancellationToken ct = default);
    Task<ProjectBoqRevisionResponse?> SubmitBoqRevisionAsync(int projectId, int id, ProcurementTransitionRequest request, int userId, CancellationToken ct = default);
    Task<ProjectBoqRevisionResponse?> DecideBoqRevisionAsync(int projectId, int id, ProcurementDecisionRequest request, int userId, CancellationToken ct = default);
    Task<MaterialRequestResponse> CreateMaterialRequestAsync(int projectId, MaterialRequestUpsertRequest request, int userId, CancellationToken ct = default);
    Task<MaterialRequestResponse?> UpdateMaterialRequestAsync(int projectId, int id, MaterialRequestUpsertRequest request, int userId, CancellationToken ct = default);
    Task<MaterialRequestResponse?> SubmitMaterialRequestAsync(int projectId, int id, ProcurementTransitionRequest request, int userId, CancellationToken ct = default);
    Task<MaterialRequestResponse?> DecideMaterialRequestAsync(int projectId, int id, ProcurementDecisionRequest request, int userId, CancellationToken ct = default);
    Task<MaterialRequestResponse?> CancelMaterialRequestAsync(int projectId, int id, ProcurementTransitionRequest request, int userId, CancellationToken ct = default);
    Task<ContractLineResponse> CreateContractLineAsync(int projectId, ContractLineUpsertRequest request, CancellationToken ct = default);
    Task<ContractLineResponse?> UpdateContractLineAsync(int projectId, int id, ContractLineUpsertRequest request, CancellationToken ct = default);
    Task<WarehouseReceiptResponse> CreateReceiptAsync(int projectId, WarehouseReceiptCreateRequest request, int userId, CancellationToken ct = default);
    Task<WarehouseReceiptResponse?> PostReceiptAsync(int projectId, int id, ProcurementTransitionRequest request, int userId, CancellationToken ct = default);
    Task<WarehouseReceiptResponse?> ReverseReceiptAsync(int projectId, int id, WarehouseReversalRequest request, int userId, CancellationToken ct = default);
    Task<WarehouseIssueResponse> CreateIssueAsync(int projectId, WarehouseIssueCreateRequest request, int userId, CancellationToken ct = default);
    Task<WarehouseIssueResponse?> PostIssueAsync(int projectId, int id, ProcurementTransitionRequest request, int userId, CancellationToken ct = default);
    Task<WarehouseIssueResponse?> ReverseIssueAsync(int projectId, int id, WarehouseReversalRequest request, int userId, CancellationToken ct = default);
    Task<VendorRatingResponse> CreateVendorRatingAsync(int projectId, VendorRatingUpsertRequest request, int userId, CancellationToken ct = default);
    Task<VendorRatingResponse?> UpdateVendorRatingAsync(int projectId, int id, VendorRatingUpsertRequest request, CancellationToken ct = default);
    Task<VendorRatingResponse?> SubmitVendorRatingAsync(int projectId, int id, ProcurementTransitionRequest request, CancellationToken ct = default);
    Task<VendorRatingResponse?> DecideVendorRatingAsync(int projectId, int id, ProcurementDecisionRequest request, int userId, CancellationToken ct = default);
}

public sealed class ProcurementOperationException(string message) : Exception(message);