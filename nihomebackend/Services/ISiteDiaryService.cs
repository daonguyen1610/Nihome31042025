using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>
/// M4 Site Diary (Nhật ký công trình / NIH-142) service — owns CRUD,
/// the Draft → Submitted → Confirmed lifecycle, and bulk delete.
/// </summary>
public interface ISiteDiaryService
{
    Task<SiteDiaryListResponse> ListAsync(SiteDiaryListParams parameters, CancellationToken ct = default);
    Task<SiteDiaryResponse?> GetAsync(int id, CancellationToken ct = default);
    Task<SiteDiaryResponse> CreateAsync(CreateSiteDiaryRequest request, int callerUserId, CancellationToken ct = default);
    Task<SiteDiaryResponse?> UpdateAsync(int id, UpdateSiteDiaryRequest request, int callerUserId, CancellationToken ct = default);
    Task<SiteDiaryResponse> SubmitAsync(int id, int callerUserId, CancellationToken ct = default);
    Task<SiteDiaryResponse> ConfirmAsync(int id, int callerUserId, CancellationToken ct = default);
    Task<SiteDiaryResponse> ReopenAsync(int id, int callerUserId, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
    Task<SiteDiaryBulkDeleteResponse> BulkDeleteAsync(IReadOnlyList<int> ids, CancellationToken ct = default);
}

public class SiteDiaryOperationException(string message) : Exception(message);
