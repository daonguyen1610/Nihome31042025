using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public sealed class ProjectDocumentValidationException(string message) : Exception(message);
public class ProjectDocumentConflictException(string message) : Exception(message);
/// <summary>The file is no longer on Google Drive and the server holds no copy of it.</summary>
public sealed class ProjectDocumentFileMissingException() : ProjectDocumentConflictException(
    "Tệp không còn trên Google Drive và máy chủ không còn bản sao. Hãy khôi phục tệp từ thùng rác Drive " +
    "rồi bấm Kiểm tra lại, hoặc tải tệp lên lại.");
public sealed record ProjectDocumentDownload(
    string OriginalFileName,
    string ContentType,
    Func<Stream, CancellationToken, Task> WriteToAsync);

public interface IProjectDocumentService
{
    Task<IReadOnlyList<ProjectDocumentResponse>?> ListAsync(int projectId, int callerUserId, bool canSeeAll, CancellationToken ct = default);
    Task<IReadOnlyList<ProjectDocumentResponse>?> ListForDesignAsync(int projectId, int designProjectId, int callerUserId, bool canSeeAll, CancellationToken ct = default);
    Task<ProjectDocumentResponse?> UploadAsync(int projectId, ProjectDocumentUploadRequest request, int callerUserId, bool canSeeAll, CancellationToken ct = default);
    Task<ProjectDocumentDownload?> DownloadAsync(int projectId, long documentId, int callerUserId, bool canSeeAll, CancellationToken ct = default);
    Task<ProjectDocumentResponse?> ClassifyAsync(int projectId, long documentId, ClassifyProjectDocumentRequest request, int callerUserId, bool canSeeAll, CancellationToken ct = default);
    Task<ProjectDocumentResponse?> ResolveConflictAsync(int projectId, long documentId, ResolveProjectDocumentConflictRequest request, int callerUserId, bool canSeeAll, CancellationToken ct = default);
    Task<bool> DeleteAsync(int projectId, long documentId, int callerUserId, bool canSeeAll, CancellationToken ct = default);
    Task<ProjectDocumentResponse?> RetryAsync(int projectId, long documentId, int callerUserId, bool canSeeAll, CancellationToken ct = default);
}
