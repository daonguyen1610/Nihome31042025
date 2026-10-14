using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public interface ILegalActivationService
{
    Task<LegalActivationPreviewResponse?> PreviewContractAsync(int contractId, int callerUserId, CancellationToken ct = default);
    Task<LegalActivationResultResponse?> ActivateContractAsync(
        int contractId, ActivateContractWithLegalRequest request, int callerUserId, CancellationToken ct = default);
    Task<LegalActivationPreviewResponse?> PreviewProjectAsync(int projectId, int callerUserId, CancellationToken ct = default);
    Task<LegalActivationResultResponse?> SetupProjectAsync(
        int projectId, SetupProjectLegalRequest request, int callerUserId, CancellationToken ct = default);
}

public sealed class LegalActivationException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
