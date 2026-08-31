using System.Data;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services.GoogleDrive;

namespace NihomeBackend.Services;

public sealed class SurveyMediaService(
    AppDbContext db,
    ISurveyMediaStorageService storage,
    IGoogleDriveAdapter drive,
    IProjectDocumentStagingService projectDocuments,
    TranslationService translations,
    ILogger<SurveyMediaService>? logger = null) : ISurveyMediaService
{
    public const int MaxSyncAttempts = 3;
    internal const string ProjectDocumentSlot = "media";
    private static readonly HashSet<string> SupportedLanguages = ["vi", "en", "zh", "ja"];

    public async Task<SurveyMediaResponse?> AddAsync(
        int surveyId, SurveyMediaUploadRequest request, int userId, CancellationToken ct = default)
    {
        if (request.Latitude.HasValue != request.Longitude.HasValue)
        {
            throw new SurveyMediaValidationException("Vĩ độ và kinh độ phải được cung cấp cùng nhau, ví dụ 10.776900 và 106.700900.");
        }
        if (request.File is null || request.File.Length == 0)
        {
            throw new SurveyMediaValidationException("Tệp khảo sát là bắt buộc và không được để trống.");
        }

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
            : null;
        var linkedProject = await db.Surveys.AsNoTracking()
            .Where(s => s.Id == surveyId)
            .Select(s => new
            {
                OperationalProjectId = s.LinkedOpportunity != null
                    ? s.LinkedOpportunity.OperationalProjectId
                    : null,
                CustomerId = s.LinkedOpportunity != null
                    ? (int?)s.LinkedOpportunity.CustomerId
                    : null,
            })
            .FirstOrDefaultAsync(ct);
        if (linkedProject is null) return null;
        var currentSize = await db.SurveyMedia
            .Where(m => m.SurveyId == surveyId)
            .SumAsync(m => (long?)m.Size, ct) ?? 0;
        if (request.File.Length > SurveyMediaStorageService.MaxSurveySize - currentSize)
        {
            throw new SurveyMediaValidationException(
                "Tổng dung lượng tệp đang lưu của phiếu khảo sát không được vượt quá 2 GiB. Vui lòng xoá bớt tệp trước khi tải lên.");
        }

        StoredSurveyMedia? stored = null;
        try
        {
            stored = await storage.StoreAsync(surveyId, request.File, ct);
            var now = DateTime.UtcNow;
            var entity = new SurveyMedia
            {
                SurveyId = surveyId,
                OriginalFileName = stored.OriginalFileName,
                StoredFileName = stored.StoredFileName,
                ContentType = stored.ContentType,
                Extension = stored.Extension,
                Size = stored.Size,
                Note = TrimOrNull(request.Note),
                CapturedAt = request.CapturedAt,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                RelativePath = stored.RelativePath,
                SyncStatus = SurveyMediaSyncStatus.Pending,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedByUserId = userId,
                UpdatedByUserId = userId,
            };
            db.SurveyMedia.Add(entity);
            await db.SaveChangesAsync(ct);
            if (linkedProject.OperationalProjectId.HasValue)
            {
                await projectDocuments.StageExistingManagedFileAsync(
                    linkedProject.OperationalProjectId.Value,
                    ProjectDocumentCategory.CrmPreDesign,
                    ProjectDocumentSourceModule.Survey,
                    EntityTypes.SurveyMedia,
                    ProjectDocumentSlot,
                    entity.Id,
                    entity.RelativePath,
                    entity.OriginalFileName,
                    linkedProject.CustomerId,
                    null,
                    userId,
                    ct);
            }
            await RecalculateAggregateAsync(surveyId, ct);
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return Map(entity);
        }
        catch
        {
            if (stored is not null) storage.Delete(surveyId, stored.RelativePath);
            throw;
        }
    }

    public async Task<ManagedDocumentContent?> GetContentAsync(int surveyId, long mediaId, CancellationToken ct = default)
    {
        var media = await db.SurveyMedia.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == mediaId && m.SurveyId == surveyId, ct);
        return media is null
            ? null
            : storage.GetContent(surveyId, media.RelativePath, media.OriginalFileName, media.ContentType);
    }

    public async Task<bool> DeleteAsync(int surveyId, long mediaId, CancellationToken ct = default)
    {
        var media = await db.SurveyMedia
            .Include(m => m.Survey).ThenInclude(s => s.LinkedOpportunity)
            .FirstOrDefaultAsync(m => m.Id == mediaId && m.SurveyId == surveyId, ct);
        if (media is null) return false;
        var operationalProjectId = media.Survey.LinkedOpportunity?.OperationalProjectId;

        if (media.SyncStatus == SurveyMediaSyncStatus.Processing)
        {
            throw new SurveyMediaConflictException(
                "Tệp đang được đồng bộ. Vui lòng chờ lần xử lý hiện tại hoàn tất trước khi xoá.");
        }

        await using var transaction = operationalProjectId.HasValue && db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
            : null;
        if (operationalProjectId.HasValue)
        {
            bool staged;
            try
            {
                staged = await projectDocuments.StageExistingManagedFileDeleteAsync(
                    operationalProjectId.Value,
                    ProjectDocumentSourceModule.Survey,
                    EntityTypes.SurveyMedia,
                    ProjectDocumentSlot,
                    media.Id,
                    media.RelativePath,
                    media.UpdatedByUserId,
                    ct);
            }
            catch (ProjectDocumentConflictException exception)
            {
                throw new SurveyMediaConflictException(exception.Message);
            }
            if (!staged)
            {
                throw new SurveyMediaValidationException(
                    "Không tìm thấy hàng đợi đồng bộ dự án của tệp khảo sát; dữ liệu được giữ nguyên để tránh bỏ sót tệp trên Google Drive.");
            }
        }
        else if (media.SyncStatus == SurveyMediaSyncStatus.Synced && !string.IsNullOrWhiteSpace(media.DriveFileId))
        {
            try
            {
                await drive.DeleteAsync(media.DriveFileId, ct);
            }
            catch (Exception exception)
            {
                throw new SurveyMediaValidationException(
                    $"Không thể xoá tệp trên Google Drive; dữ liệu được giữ nguyên để tránh mất dấu vết. Chi tiết: {exception.Message}");
            }
        }

        if (!operationalProjectId.HasValue)
        {
            try
            {
                storage.Delete(surveyId, media.RelativePath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                throw new SurveyMediaValidationException(
                    "Không thể xoá tệp khỏi vùng lưu trữ riêng tư; dữ liệu được giữ nguyên để quản trị viên có thể xử lý lại.");
            }
        }

        db.SurveyMedia.Remove(media);
        await db.SaveChangesAsync(ct);
        if (await db.SurveyMedia.AnyAsync(m => m.SurveyId == surveyId, ct))
        {
            await RecalculateAggregateAsync(surveyId, ct);
        }
        else
        {
            var survey = await db.Surveys.FirstAsync(s => s.Id == surveyId, ct);
            survey.DriveSyncStatus = SurveyDriveSyncStatus.NotSynced;
            survey.DriveSyncError = null;
            survey.LastSyncedAt = null;
            survey.DriveFolderId = null;
            survey.DriveFolderLink = null;
            survey.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        if (operationalProjectId.HasValue)
        {
            try
            {
                storage.Delete(surveyId, media.RelativePath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger?.LogWarning(exception,
                    "Survey media {MediaId} was deleted from SQL but local cleanup failed for survey {SurveyId}",
                    mediaId, surveyId);
            }
        }
        return true;
    }

    public async Task<SurveyMediaResponse?> RetryAsync(
        int surveyId, long mediaId, int userId, CancellationToken ct = default)
    {
        var media = await db.SurveyMedia
            .Include(m => m.Survey).ThenInclude(s => s.LinkedOpportunity)
            .FirstOrDefaultAsync(m => m.Id == mediaId && m.SurveyId == surveyId, ct);
        if (media is null) return null;
        if (media.SyncStatus == SurveyMediaSyncStatus.Processing)
        {
            throw new SurveyMediaValidationException(
                "Tệp đang được đồng bộ. Vui lòng chờ lần xử lý hiện tại hoàn tất trước khi thử lại.");
        }
        if (media.SyncStatus == SurveyMediaSyncStatus.Synced)
        {
            throw new SurveyMediaValidationException("Tệp đã đồng bộ thành công và không cần thử lại.");
        }
        if (media.SyncAttemptCount >= MaxSyncAttempts)
        {
            throw new SurveyMediaValidationException("Tệp đã dùng hết 3 lần đồng bộ. Vui lòng kiểm tra cấu hình Drive trước khi xử lý tiếp.");
        }

        var operationalProjectId = media.Survey.LinkedOpportunity?.OperationalProjectId;
        if (operationalProjectId.HasValue)
        {
            try
            {
                if (!await projectDocuments.RetryExistingManagedFileAsync(
                        operationalProjectId.Value,
                        ProjectDocumentSourceModule.Survey,
                        EntityTypes.SurveyMedia,
                        ProjectDocumentSlot,
                        media.Id,
                        media.RelativePath,
                        userId,
                        ct))
                {
                    throw new SurveyMediaValidationException(
                        "Không tìm thấy hàng đợi đồng bộ dự án của tệp khảo sát.");
                }
            }
            catch (ProjectDocumentConflictException exception)
            {
                throw new SurveyMediaValidationException(exception.Message);
            }
            catch (ProjectDocumentValidationException exception)
            {
                throw new SurveyMediaValidationException(exception.Message);
            }
        }

        media.SyncStatus = SurveyMediaSyncStatus.Pending;
        media.NextSyncAttemptAt = DateTime.UtcNow;
        media.SyncError = null;
        media.ClaimToken = null;
        media.ClaimExpiresAt = null;
        media.UpdatedAt = DateTime.UtcNow;
        media.UpdatedByUserId = userId;
        await RecalculateAggregateAsync(surveyId, ct);
        await db.SaveChangesAsync(ct);
        return Map(media);
    }

    public async Task<SurveyChecklistResultResponse?> UpdateChecklistAsync(
        int surveyId, long resultId, UpdateSurveyChecklistResultRequest request, int userId,
        CancellationToken ct = default)
    {
        var result = await db.SurveyChecklistResults
            .FirstOrDefaultAsync(r => r.Id == resultId && r.SurveyId == surveyId, ct);
        if (result is null) return null;
        if (!request.Status.HasValue)
        {
            throw new SurveyMediaValidationException(
                "Trạng thái checklist là bắt buộc. Chọn OK, Cần chú ý hoặc Không đạt.");
        }

        result.Status = request.Status;
        result.Note = TrimOrNull(request.Note);
        result.SortOrder = request.SortOrder;
        result.UpdatedAt = DateTime.UtcNow;
        result.UpdatedByUserId = userId;
        await db.SaveChangesAsync(ct);
        return Map(result);
    }

    public async Task<List<SurveySyncLogResponse>?> GetSyncLogAsync(int surveyId, CancellationToken ct = default)
    {
        if (!await db.Surveys.AsNoTracking().AnyAsync(s => s.Id == surveyId, ct)) return null;
        return await db.SurveyMedia.AsNoTracking()
            .Where(m => m.SurveyId == surveyId)
            .OrderByDescending(m => m.LastSyncAttemptAt ?? m.CreatedAt)
            .Select(m => new SurveySyncLogResponse
            {
                MediaId = m.Id,
                FileName = m.OriginalFileName,
                Status = m.SyncStatus.ToString(),
                AttemptCount = m.SyncAttemptCount,
                MaxAttempts = MaxSyncAttempts,
                Error = m.SyncError,
                LastAttemptAt = m.LastSyncAttemptAt,
                NextAttemptAt = m.NextSyncAttemptAt,
                SyncedAt = m.SyncedAt,
            })
            .ToListAsync(ct);
    }

    public async Task<SurveyDriveConnectionStatusResponse> GetDriveConnectionStatusAsync(CancellationToken ct = default)
    {
        try
        {
            var connection = await drive.CheckConnectionAsync(ct);
            var status = !connection.IsFolder || connection.IsTrashed
                ? "InvalidRoot"
                : connection.CanAddChildren ? "Connected" : "ReadOnly";
            return new SurveyDriveConnectionStatusResponse
            {
                Status = status,
                AccountEmail = connection.AccountEmail,
                StorageType = connection.IsSharedDrive ? "SharedDrive" : "MyDrive",
                RootFolderName = connection.FolderName,
                RootFolderLink = connection.FolderLink,
                Error = status switch
                {
                    "InvalidRoot" => "RootFolderId phải trỏ đến một thư mục Google Drive chưa bị chuyển vào thùng rác.",
                    _ => null,
                },
            };
        }
        catch (Exception exception)
        {
            logger?.LogWarning(
                "Google Drive connection validation failed ({ExceptionType}). Check OAuth settings, RootFolderId, Drive API access, and folder permissions.",
                exception.GetType().Name);
            return new SurveyDriveConnectionStatusResponse
            {
                Status = "Unavailable",
                Error = "Không thể xác thực hoặc truy cập thư mục Google Drive đã cấu hình.",
            };
        }
    }

    public async Task<byte[]?> ExportPdfAsync(
        int surveyId, string languageCode, CancellationToken ct = default)
    {
        var language = NormalizeLanguage(languageCode);
        var survey = await db.Surveys.AsNoTracking()
            .Include(s => s.Surveyor)
            .Include(s => s.LinkedProject)
            .Include(s => s.LinkedOpportunity)
            .Include(s => s.ChecklistResults)
            .Include(s => s.Media)
            .FirstOrDefaultAsync(s => s.Id == surveyId, ct);
        if (survey is null) return null;

        var text = await translations.GetTranslationMapAsync(language);
        string Translate(string key, string fallback) => text.GetValueOrDefault(key, fallback);
        string ChecklistTitle(SurveyChecklistResult result) => Translate(
            $"masterData.survey_checklist_default.{result.TemplateCode}.label",
            result.TemplateTitle);
        string ChecklistStatus(SurveyChecklistStatus? status) => status switch
        {
            SurveyChecklistStatus.Ok => Translate("surveys.checklist.status.Ok", "Đạt"),
            SurveyChecklistStatus.NeedsAttention => Translate("surveys.checklist.status.NeedsAttention", "Cần chú ý"),
            SurveyChecklistStatus.Failed => Translate("surveys.checklist.status.Failed", "Không đạt"),
            _ => Translate("surveys.pdf.notAssessed", "Chưa đánh giá"),
        };
        string SyncStatus(SurveyMediaSyncStatus status) =>
            Translate($"surveys.media.syncStatus.{status}", status.ToString());
        var notAvailable = Translate("surveys.pdf.notAvailable", "-");

        var lines = new List<string>
        {
            Translate("surveys.pdf.title", "BÁO CÁO KHẢO SÁT"),
            $"{Translate("surveys.pdf.surveyCode", "Mã phiếu")}: {survey.Code}",
            $"{Translate("surveys.pdf.location", "Địa điểm")}: {survey.Location}",
            $"{Translate("surveys.pdf.surveyDate", "Ngày khảo sát")}: {survey.SurveyDate:dd/MM/yyyy}",
            $"{Translate("surveys.pdf.surveyor", "Người khảo sát")}: {survey.Surveyor?.FullName ?? notAvailable}",
            $"{Translate("surveys.pdf.projectOpportunity", "Dự án/Cơ hội")}: {survey.LinkedProject?.Name ?? survey.LinkedOpportunity?.Name ?? notAvailable}",
            $"{Translate("surveys.pdf.note", "Ghi chú")}: {survey.Note ?? notAvailable}",
            "",
            Translate("surveys.pdf.checklist", "CHECKLIST"),
        };
        lines.AddRange(survey.ChecklistResults.OrderBy(r => r.SortOrder).Select(r =>
            $"- {ChecklistTitle(r)}: {ChecklistStatus(r.Status)}{(string.IsNullOrWhiteSpace(r.Note) ? "" : $" - {r.Note}")}"));
        lines.Add("");
        lines.Add(Translate("surveys.pdf.media", "MEDIA"));
        lines.AddRange(survey.Media.OrderBy(m => m.CreatedAt).Select(m =>
            $"- {m.OriginalFileName} ({m.Size} bytes), {Translate("surveys.pdf.syncStatus", "Đồng bộ")}: {SyncStatus(m.SyncStatus)}{(string.IsNullOrWhiteSpace(m.Note) ? "" : $" - {m.Note}")}"));
        return SimplePdfWriter.Create(lines, language);
    }

    public async Task RecalculateAggregateAsync(int surveyId, CancellationToken ct = default)
    {
        var survey = await db.Surveys.FirstOrDefaultAsync(s => s.Id == surveyId, ct);
        if (survey is null) return;
        var media = await db.SurveyMedia.Where(m => m.SurveyId == surveyId).ToListAsync(ct);
        if (media.Count == 0) return;

        survey.DriveSyncStatus = media.Any(m => m.SyncStatus == SurveyMediaSyncStatus.Processing)
            ? SurveyDriveSyncStatus.Syncing
            : media.Any(m => m.SyncStatus == SurveyMediaSyncStatus.Failed)
                ? SurveyDriveSyncStatus.Failed
                : media.All(m => m.SyncStatus == SurveyMediaSyncStatus.Synced)
                    ? SurveyDriveSyncStatus.Synced
                    : SurveyDriveSyncStatus.NotSynced;
        survey.DriveSyncError = media.Where(m => m.SyncStatus == SurveyMediaSyncStatus.Failed)
            .OrderByDescending(m => m.LastSyncAttemptAt)
            .Select(m => m.SyncError)
            .FirstOrDefault();
        survey.LastSyncedAt = media.Where(m => m.SyncedAt.HasValue).Max(m => m.SyncedAt);
        survey.UpdatedAt = DateTime.UtcNow;
    }

    public static SurveyMediaResponse Map(SurveyMedia media) => new()
    {
        Id = media.Id,
        OriginalFileName = media.OriginalFileName,
        ContentType = media.ContentType,
        Extension = media.Extension,
        Size = media.Size,
        Note = media.Note,
        CapturedAt = media.CapturedAt,
        Latitude = media.Latitude,
        Longitude = media.Longitude,
        SyncStatus = media.SyncStatus.ToString(),
        SyncAttemptCount = media.SyncAttemptCount,
        MaxSyncAttempts = MaxSyncAttempts,
        SyncError = media.SyncError,
        NextSyncAttemptAt = media.NextSyncAttemptAt,
        LastSyncAttemptAt = media.LastSyncAttemptAt,
        SyncedAt = media.SyncedAt,
        CreatedAt = media.CreatedAt,
        ContentUrl = $"/api/surveys/{media.SurveyId}/media/{media.Id}/content",
    };

    public static SurveyChecklistResultResponse Map(SurveyChecklistResult result) => new()
    {
        Id = result.Id,
        TemplateCode = result.TemplateCode,
        TemplateTitle = result.TemplateTitle,
        Status = result.Status?.ToString(),
        Note = result.Note,
        SortOrder = result.SortOrder,
        UpdatedAt = result.UpdatedAt,
    };

    private static string NormalizeLanguage(string languageCode)
    {
        var normalized = languageCode.Trim().ToLowerInvariant();
        if (!SupportedLanguages.Contains(normalized))
        {
            throw new SurveyMediaValidationException(
                "Ngôn ngữ xuất PDF không hợp lệ. Chỉ chấp nhận vi, en, zh hoặc ja.");
        }
        return normalized;
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}