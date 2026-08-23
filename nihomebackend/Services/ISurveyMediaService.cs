using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public interface ISurveyMediaService
{
    Task<SurveyMediaResponse?> AddAsync(int surveyId, SurveyMediaUploadRequest request, int userId, CancellationToken ct = default);
    Task<ManagedDocumentContent?> GetContentAsync(int surveyId, long mediaId, CancellationToken ct = default);
    Task<bool> DeleteAsync(int surveyId, long mediaId, CancellationToken ct = default);
    Task<SurveyMediaResponse?> RetryAsync(int surveyId, long mediaId, int userId, CancellationToken ct = default);
    Task<SurveyChecklistResultResponse?> UpdateChecklistAsync(int surveyId, long resultId, UpdateSurveyChecklistResultRequest request, int userId, CancellationToken ct = default);
    Task<List<SurveySyncLogResponse>?> GetSyncLogAsync(int surveyId, CancellationToken ct = default);
    Task<SurveyDriveConnectionStatusResponse> GetDriveConnectionStatusAsync(CancellationToken ct = default);
    Task<byte[]?> ExportPdfAsync(int surveyId, string languageCode, CancellationToken ct = default);
    Task RecalculateAggregateAsync(int surveyId, CancellationToken ct = default);
}