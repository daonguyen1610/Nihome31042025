using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>
/// Thrown for expected business-rule violations in the Tender workflow —
/// deadline in the past, editing locked fields on a submitted tender, etc.
/// Controller converts to HTTP 400.
/// </summary>
public class TenderOperationException(string message) : Exception(message)
{
}

/// <summary>
/// Tender (Gói thầu) service — CRUD, auto-generated preparation checklist
/// on create, and status-aware edit rules. Result-transition workflow
/// (Mark Won / Mark Lost + auto-create Contract) ships with the detail
/// slice (NIH-97).
/// </summary>
public interface ITenderService
{
    Task<TenderListResponse> ListAsync(TenderListParams parameters, CancellationToken ct = default);

    Task<TenderResponse?> GetAsync(int id, CancellationToken ct = default);

    Task<TenderResponse> CreateAsync(CreateTenderRequest request, int callerUserId, CancellationToken ct = default);

    Task<TenderResponse?> UpdateAsync(int id, UpdateTenderRequest request, int callerUserId, CancellationToken ct = default);

    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}
