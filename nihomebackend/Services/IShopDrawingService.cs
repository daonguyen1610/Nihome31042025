using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>
/// M2 Shop Drawing service (NIH-116 slice 1). Owns CRUD + state-machine
/// enforcement + per-discipline auto document-code allocation + bulk
/// delete for drafts. IFC bundle bridge lands in NIH-118.
/// </summary>
public interface IShopDrawingService
{
    Task<ShopDrawingListResponse> ListAsync(ShopDrawingListParams parameters, CancellationToken ct = default);
    Task<ShopDrawingResponse?> GetAsync(int id, CancellationToken ct = default);
    Task<ShopDrawingResponse> CreateAsync(CreateShopDrawingRequest request, int callerUserId, CancellationToken ct = default);
    Task<ShopDrawingResponse?> UpdateAsync(int id, UpdateShopDrawingRequest request, int callerUserId, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
    Task<ShopDrawingBulkDeleteResponse> BulkDeleteAsync(IReadOnlyList<int> ids, CancellationToken ct = default);
    Task<ShopDrawingResponse?> TransitionStatusAsync(int id, TransitionShopDrawingStatusRequest request, int callerUserId, CancellationToken ct = default);
}

/// <summary>
/// Domain-level operation failure — controllers translate to 400.
/// Mirrors the pattern from <see cref="BasicDesignDocOperationException"/>.
/// </summary>
public class ShopDrawingOperationException(string message) : Exception(message);
