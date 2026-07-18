using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public interface IContractAttachmentService
{
    Task<List<ContractAttachmentResponse>?> ListAsync(
        int contractId, int callerUserId, bool canSeeAll, CancellationToken ct = default);
    Task<ContractAttachmentResponse?> CreateAsync(
        int contractId, CreateContractAttachmentRequest req, int callerUserId, bool canSeeAll, CancellationToken ct = default);
    Task<bool> DeleteAsync(
        int contractId, int attachmentId, int callerUserId, bool canSeeAll, CancellationToken ct = default);
}
