using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>
/// M2 IFC Release service (NIH-118 slice 1). Owns the release aggregate:
/// header CRUD, item/recipient management, per-recipient acknowledgement,
/// and the atomic <see cref="ReleaseAsync"/> action that flips every
/// bundled <see cref="Models.ShopDrawing"/> to
/// <see cref="Models.ShopDrawingStatus.Released"/> — the only writer for
/// that state.
/// </summary>
public interface IIfcReleaseService
{
    Task<IfcReleaseListResponse> ListAsync(IfcReleaseListParams parameters, CancellationToken ct = default);
    Task<IfcReleaseResponse?> GetAsync(int id, CancellationToken ct = default);
    Task<IfcReleaseResponse> CreateAsync(CreateIfcReleaseRequest request, int callerUserId, CancellationToken ct = default);
    Task<IfcReleaseResponse?> UpdateAsync(int id, UpdateIfcReleaseRequest request, int callerUserId, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
    Task<IfcReleaseResponse> AddItemsAsync(int id, AddIfcReleaseItemsRequest request, int callerUserId, CancellationToken ct = default);
    Task<IfcReleaseResponse> RemoveItemAsync(int id, int itemId, int callerUserId, CancellationToken ct = default);
    Task<IfcReleaseResponse> AddRecipientAsync(int id, AddIfcReleaseRecipientRequest request, int callerUserId, CancellationToken ct = default);
    Task<IfcReleaseResponse> RemoveRecipientAsync(int id, int recipientId, int callerUserId, CancellationToken ct = default);
    Task<IfcReleaseResponse> AcknowledgeRecipientAsync(int id, int recipientId, AcknowledgeIfcReleaseRecipientRequest request, int callerUserId, CancellationToken ct = default);
    Task<IfcReleaseResponse> ReleaseAsync(int id, int callerUserId, CancellationToken ct = default);
    Task<IfcReleaseResponse> CancelAsync(int id, int callerUserId, CancellationToken ct = default);
}

public class IfcReleaseOperationException(string message) : Exception(message);
