using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public interface IContractAttachmentService
{
    Task<List<ContractAttachmentResponse>?> ListAsync(
        int contractId, int callerUserId, bool canSeeAll, CancellationToken ct = default);
    /// <param name="keepFileOnFailure">
    /// Leave the stored file in place when registering fails, for a caller that puts it back
    /// (a finished chunked upload) instead of losing it.
    /// </param>
    Task<ContractAttachmentResponse?> CreateAsync(
        int contractId, CreateContractAttachmentRequest req, int callerUserId, bool canSeeAll,
        CancellationToken ct = default, bool keepFileOnFailure = false);
    Task<ManagedDocumentContent?> GetContentAsync(
        int contractId, int attachmentId, int callerUserId, bool canSeeAll, CancellationToken ct = default);
    Task<bool> DeleteAsync(
        int contractId, int attachmentId, int callerUserId, bool canSeeAll, CancellationToken ct = default);
}
