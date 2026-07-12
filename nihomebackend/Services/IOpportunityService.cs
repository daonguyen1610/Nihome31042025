using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>
/// Owns lifecycle of CRM Opportunities: CRUD, stage transitions with
/// forward-only progression once terminal (Won/Lost), timeline entries,
/// and pipeline aggregation for the Kanban board.
///
/// Access scoping (own vs. all opportunities) is decided per call using
/// the <c>crm.opportunities.view.all</c> permission; callers pass the
/// caller id + whether they can see all opportunities. The service never
/// trusts role names.
/// </summary>
public interface IOpportunityService
{
    Task<OpportunityListResponse> ListAsync(
        int callerUserId,
        bool canSeeAll,
        OpportunityStage? stage = null,
        int? customerId = null,
        int? ownerUserId = null,
        DateTime? expectedCloseFrom = null,
        DateTime? expectedCloseTo = null,
        decimal? minValue = null,
        decimal? maxValue = null,
        string? search = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default);

    Task<OpportunityPipelineResponse> GetPipelineAsync(
        int callerUserId,
        bool canSeeAll,
        int? ownerUserId = null,
        int? customerId = null,
        DateTime? expectedCloseFrom = null,
        DateTime? expectedCloseTo = null,
        decimal? minValue = null,
        decimal? maxValue = null,
        CancellationToken ct = default);

    Task<OpportunityResponse?> GetAsync(int id, int callerUserId, bool canSeeAll, CancellationToken ct = default);

    Task<OpportunityResponse> CreateAsync(
        CreateOpportunityRequest request,
        int callerUserId,
        bool canManage,
        CancellationToken ct = default);

    Task<OpportunityResponse?> UpdateAsync(
        int id,
        UpdateOpportunityRequest request,
        int callerUserId,
        bool canManage,
        bool canSeeAll,
        CancellationToken ct = default);

    Task<OpportunityResponse?> ChangeStageAsync(
        int id,
        ChangeOpportunityStageRequest request,
        int callerUserId,
        bool canManage,
        bool canSeeAll,
        CancellationToken ct = default);

    Task<bool> DeleteAsync(
        int id,
        int callerUserId,
        bool canManage,
        bool canSeeAll,
        CancellationToken ct = default);

    Task<OpportunityActivityResponse?> AddActivityAsync(
        int opportunityId,
        AddOpportunityActivityRequest request,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default);
}

/// <summary>Thrown when a stage transition or business rule is violated.</summary>
public sealed class OpportunityOperationException(string message) : InvalidOperationException(message);
