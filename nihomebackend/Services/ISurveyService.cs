using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>
/// Thrown for expected business-rule violations in the Survey workflow.
/// Controller converts to HTTP 400.
/// </summary>
public class SurveyOperationException(string message) : Exception(message)
{
}

/// <summary>
/// Survey (Phiếu khảo sát) service. NIH-99 ships the list + get slice;
/// NIH-100 layers full CRUD (this file) on top. Detail-page workflow
/// (media, drive-sync polling) lands with NIH-101.
/// </summary>
public interface ISurveyService
{
    Task<SurveyListResponse> ListAsync(SurveyListParams parameters, CancellationToken ct = default);

    Task<SurveyResponse?> GetAsync(int id, CancellationToken ct = default);

    Task<SurveyResponse> CreateAsync(CreateSurveyRequest request, int callerUserId, CancellationToken ct = default);

    /// <summary>
    /// Update a survey. Every text field on the request is applied; the
    /// caller is expected to send the full projection. Media / drive-sync
    /// fields are managed by NIH-101 endpoints, not by this write path.
    /// Returns <c>null</c> when the row does not exist.
    /// </summary>
    Task<SurveyResponse?> UpdateAsync(int id, UpdateSurveyRequest request, int callerUserId, CancellationToken ct = default);

    /// <summary>
    /// Delete a survey. Guarded once the row has hit Drive so the audit
    /// trail is preserved — the service throws
    /// <see cref="SurveyOperationException"/> when
    /// <c>DriveSyncStatus != NotSynced</c>. Returns <c>false</c> when the
    /// row does not exist.
    /// </summary>
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Ordered (newest-first) audit-log slice for the NIH-101 History tab.
    /// Returns <c>null</c> when the survey does not exist so the controller
    /// can 404 without leaking existence.
    /// </summary>
    Task<List<SurveyTimelineEvent>?> GetTimelineAsync(int id, int limit, CancellationToken ct = default);
}
