using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public interface IQuoteService
{
    Task<QuoteListResponse> ListAsync(
        int callerUserId,
        bool canSeeAll,
        QuoteStatus? status = null,
        int? opportunityId = null,
        int? customerId = null,
        int? ownerUserId = null,
        DateTime? createdFrom = null,
        DateTime? createdTo = null,
        decimal? minValue = null,
        decimal? maxValue = null,
        string? search = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default);

    Task<QuoteResponse?> GetAsync(int id, int callerUserId, bool canSeeAll, CancellationToken ct = default);

    Task<QuoteResponse> CreateAsync(
        CreateQuoteRequest request,
        int callerUserId,
        bool canManage,
        CancellationToken ct = default);

    Task<QuoteResponse?> UpdateAsync(
        int id,
        UpdateQuoteRequest request,
        int callerUserId,
        bool canManage,
        bool canSeeAll,
        CancellationToken ct = default);

    Task<QuoteResponse?> SubmitAsync(int id, QuoteWorkflowRequest request, int callerUserId, bool canManage, bool canSeeAll, CancellationToken ct = default);
    Task<QuoteResponse?> ApproveAsync(int id, QuoteWorkflowRequest request, int callerUserId, bool canApprove, CancellationToken ct = default);
    Task<QuoteResponse?> RejectInternalAsync(int id, QuoteWorkflowRequest request, int callerUserId, bool canApprove, CancellationToken ct = default);
    Task<QuoteResponse?> SendToCustomerAsync(int id, QuoteWorkflowRequest request, int callerUserId, bool canSend, bool canSeeAll, CancellationToken ct = default);
    Task<QuoteResponse?> MarkCustomerApprovedAsync(int id, QuoteWorkflowRequest request, int callerUserId, bool canManage, bool canSeeAll, CancellationToken ct = default);
    Task<QuoteResponse?> MarkCustomerRejectedAsync(int id, QuoteWorkflowRequest request, int callerUserId, bool canManage, bool canSeeAll, CancellationToken ct = default);
    Task<QuoteResponse?> CancelAsync(int id, QuoteWorkflowRequest request, int callerUserId, bool canManage, bool canSeeAll, CancellationToken ct = default);
    Task<QuoteResponse?> ExtendValidityAsync(int id, ExtendQuoteValidityRequest request, int callerUserId, bool canApprove, CancellationToken ct = default);

    Task<bool> DeleteAsync(int id, int callerUserId, bool canManage, bool canSeeAll, CancellationToken ct = default);

    Task<QuoteVersionsResponse?> GetVersionsAsync(int id, int callerUserId, bool canSeeAll, CancellationToken ct = default);
}
