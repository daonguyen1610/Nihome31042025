using Microsoft.AspNetCore.Http;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public interface IVendorService
{
    Task<VendorListResponse> ListAsync(int callerUserId, bool canSeeAll, string? search, VendorType? type, bool? isActive, int? ownerUserId, string? serviceGroupCode, string? sortBy, string? sortDirection, int page, int pageSize, CancellationToken ct = default);
    Task<List<VendorResponse>> ExportAsync(int callerUserId, bool canSeeAll, string? search, VendorType? type, bool? isActive, int? ownerUserId, string? serviceGroupCode, string? sortBy, string? sortDirection, CancellationToken ct = default);
    Task<List<VendorOwnerOptionResponse>> GetOwnerOptionsAsync(CancellationToken ct = default);
    Task<List<VendorProjectOptionResponse>> GetProjectOptionsAsync(CancellationToken ct = default);
    Task<VendorResponse?> GetAsync(int id, int callerUserId, bool canSeeAll, CancellationToken ct = default);
    Task<VendorResponse> CreateAsync(CreateVendorRequest request, int callerUserId, bool canSeeAll, CancellationToken ct = default);
    Task<VendorResponse?> UpdateAsync(int id, UpdateVendorRequest request, int callerUserId, bool canSeeAll, CancellationToken ct = default);
    Task<VendorDocumentResponse?> UploadDocumentAsync(int vendorId, VendorDocumentType documentType, IFormFile file, int callerUserId, bool canSeeAll, CancellationToken ct = default);
    Task<VendorDocumentDownload?> DownloadDocumentAsync(int vendorId, int documentId, int callerUserId, bool canSeeAll, CancellationToken ct = default);
    Task<bool> DeleteDocumentAsync(int vendorId, int documentId, int callerUserId, bool canSeeAll, CancellationToken ct = default);
    Task<VendorEvaluationResponse?> CreateEvaluationAsync(int vendorId, UpsertVendorEvaluationRequest request, int callerUserId, bool canSeeAll, CancellationToken ct = default);
    Task<VendorEvaluationResponse?> UpdateEvaluationAsync(int vendorId, int evaluationId, UpsertVendorEvaluationRequest request, int callerUserId, bool canSeeAll, CancellationToken ct = default);
    Task<bool> DeleteEvaluationAsync(int vendorId, int evaluationId, int callerUserId, bool canSeeAll, CancellationToken ct = default);
    Task<List<VendorAuditResponse>?> GetHistoryAsync(int vendorId, int callerUserId, bool canSeeAll, CancellationToken ct = default);
}

public sealed class VendorOperationException(string message) : Exception(message);
public sealed class VendorDocumentMissingException(string message) : Exception(message);
