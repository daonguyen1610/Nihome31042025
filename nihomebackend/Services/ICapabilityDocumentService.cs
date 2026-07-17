using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>
/// Thrown for expected business-rule violations (missing FK master data,
/// duplicate uploads, deleting a document still referenced by an open
/// tender). Controller converts to HTTP 400.
/// </summary>
public class CapabilityDocumentOperationException(string message) : Exception(message)
{
}

/// <summary>
/// Repository of shared capability documents (hồ sơ năng lực) — see
/// <see cref="Models.CapabilityDocument"/> for the entity summary. The
/// service owns metadata persistence, tag validation against master data,
/// version snapshotting on file replace, and file cleanup on delete. The
/// controller is responsible for turning multipart uploads into paths on
/// disk; the service assumes files already exist at the given
/// <see cref="UpsertCapabilityDocumentRequest.FilePath"/>.
/// </summary>
public interface ICapabilityDocumentService
{
    Task<CapabilityDocumentListResponse> ListAsync(
        string? tagCode = null,
        int? issuedYear = null,
        string? search = null,
        string? expiryState = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default);

    Task<CapabilityDocumentDetailResponse?> GetAsync(int id, CancellationToken ct = default);

    Task<List<CapabilityDocumentResponse>> GetManyAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default);

    Task<CapabilityDocumentResponse> CreateAsync(
        UpsertCapabilityDocumentRequest request,
        int callerUserId,
        CancellationToken ct = default);

    Task<CapabilityDocumentResponse?> UpdateAsync(
        int id,
        UpsertCapabilityDocumentRequest request,
        int callerUserId,
        CancellationToken ct = default);

    Task<CapabilityDocumentResponse?> ReplaceFileAsync(
        int id,
        ReplaceCapabilityDocumentFileRequest request,
        int callerUserId,
        CancellationToken ct = default);

    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}
