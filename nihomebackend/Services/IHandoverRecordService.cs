using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class HandoverRecordOperationException(string message) : Exception(message)
{
}

public interface IHandoverRecordService
{
    Task<HandoverRecordListResponse> ListAsync(HandoverRecordListParams parameters, int callerUserId, bool canSeeAll, CancellationToken ct = default);
    Task<List<HandoverRecordResponse>> ExportAsync(HandoverRecordListParams parameters, int callerUserId, bool canSeeAll, CancellationToken ct = default);
    Task<HandoverRecordResponse?> GetAsync(int id, int callerUserId, bool canSeeAll, CancellationToken ct = default);
    Task<HandoverRecordResponse> CreateAsync(CreateHandoverRecordRequest request, int callerUserId, bool canSeeAll, CancellationToken ct = default);
    Task<HandoverRecordResponse?> UpdateAsync(int id, UpdateHandoverRecordRequest request, int callerUserId, bool canSeeAll, CancellationToken ct = default);
    Task<HandoverRecordResponse?> TransitionAsync(int id, TransitionHandoverStatusRequest request, int callerUserId, bool canSeeAll, CancellationToken ct = default);
    Task<HandoverRecordResponse?> CompleteAsync(int id, TransitionHandoverStatusRequest request, int callerUserId, bool canSeeAll, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, int callerUserId, bool canSeeAll, CancellationToken ct = default);
}