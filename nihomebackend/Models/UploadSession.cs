namespace NihomeBackend.Models;

/// <summary>
/// A large file sent in chunks so a dropped connection resumes instead of starting over,
/// and each request stays far below the IIS request limit. The bytes are staged outside
/// wwwroot until the upload request it was opened for succeeds; that request then sees
/// the staged file as an ordinary form file, so every module keeps its own checks.
/// </summary>
public class UploadSession
{
    public const int TargetPathMaxLength = 300;
    /// <summary>Upload responses are small JSON records; a larger one is not kept for replay.</summary>
    public const int ResultBodyMaxLength = 64 * 1024;

    public Guid Id { get; set; }
    public int UserId { get; set; }
    /// <summary>The API path of the upload request the file is for, e.g. /api/quotes/5/documents.</summary>
    public string TargetPath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public long ReceivedBytes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// When the upload request succeeded with this file. The staged bytes are gone, but the
    /// response is kept until expiry so the same request sent again (its reply was lost)
    /// gets that response instead of storing the file twice or uploading it again.
    /// </summary>
    public DateTime? UsedAt { get; set; }
    public int? ResultStatus { get; set; }
    public string? ResultContentType { get; set; }
    public string? ResultBody { get; set; }
}
