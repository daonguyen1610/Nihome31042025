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
/// Survey (Phiếu khảo sát) service. NIH-99 ships the list + get slice —
/// create / update / delete land with NIH-100, detail-page workflow with
/// NIH-101.
/// </summary>
public interface ISurveyService
{
    Task<SurveyListResponse> ListAsync(SurveyListParams parameters, CancellationToken ct = default);

    Task<SurveyResponse?> GetAsync(int id, CancellationToken ct = default);

    Task<SurveyResponse> CreateAsync(CreateSurveyRequest request, int callerUserId, CancellationToken ct = default);
}
