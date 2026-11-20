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
}
