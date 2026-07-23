using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>
/// M4 Punch List (Danh mục lỗi tồn đọng / NIH-146) service — CRUD +
/// status transitions (Open → InProgress → Fixed → Verified, plus
/// Cancelled + reopen back to Open) + bulk delete.
/// </summary>
public interface IPunchItemService
{
    Task<PunchItemListResponse> ListAsync(PunchItemListParams parameters, CancellationToken ct = default);
    Task<PunchItemResponse?> GetAsync(int id, CancellationToken ct = default);
    Task<PunchItemResponse> CreateAsync(CreatePunchItemRequest request, int callerUserId, CancellationToken ct = default);
    Task<PunchItemResponse?> UpdateAsync(int id, UpdatePunchItemRequest request, int callerUserId, CancellationToken ct = default);
    Task<PunchItemResponse?> TransitionStatusAsync(int id, TransitionPunchStatusRequest request, int callerUserId, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
    Task<PunchItemBulkDeleteResponse> BulkDeleteAsync(IReadOnlyList<int> ids, CancellationToken ct = default);
}

public class PunchItemOperationException(string message) : Exception(message);
