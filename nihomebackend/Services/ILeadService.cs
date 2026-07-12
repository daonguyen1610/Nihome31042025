using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>
/// Owns lifecycle of CRM Leads: creation with round-robin auto-assign,
/// nurture-status updates (with permission-gated status changes),
/// convert-to-customer/opportunity, and activity timeline entries.
///
/// Access scoping (own-only vs. all-leads) is decided per call using the
/// <c>crm.leads.view.all</c> permission; callers pass in the effective
/// caller id + whether they can see all leads. The service never trusts
/// role names.
/// </summary>
public interface ILeadService
{
    Task<LeadListResponse> ListAsync(
        int callerUserId,
        bool canSeeAll,
        LeadStatus? status = null,
        string? sourceCode = null,
        int? ownerUserId = null,
        string? search = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default);

    Task<LeadResponse?> GetAsync(int id, int callerUserId, bool canSeeAll, CancellationToken ct = default);

    Task<LeadResponse> CreateAsync(
        CreateLeadRequest request,
        int callerUserId,
        bool canManage,
        string languageCode = "vi",
        CancellationToken ct = default);

    Task<LeadResponse?> UpdateAsync(
        int id,
        UpdateLeadRequest request,
        int callerUserId,
        bool canManage,
        bool canSeeAll,
        string languageCode = "vi",
        CancellationToken ct = default);

    Task<bool> DeleteAsync(int id, int callerUserId, bool canManage, CancellationToken ct = default);

    Task<LeadResponse?> ConvertAsync(
        int id,
        ConvertLeadRequest request,
        int callerUserId,
        bool canConvert,
        CancellationToken ct = default);

    Task<LeadActivityResponse?> AddActivityAsync(
        int leadId,
        CreateLeadActivityRequest request,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default);
}

/// <summary>Thrown when a business rule is violated (e.g. converted lead is edited).</summary>
public sealed class LeadOperationException(string message) : InvalidOperationException(message);
