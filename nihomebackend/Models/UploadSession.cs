namespace NihomeBackend.Models;

/// <summary>What an upload session will become once every byte has arrived.</summary>
public enum UploadSessionPurpose
{
    ContractAttachment,
    ContractAppendix,
}

/// <summary>
/// A large file sent in chunks so a dropped connection resumes instead of starting over,
/// and each request stays far below the IIS request limit. The bytes are staged outside
/// wwwroot until the owning module takes the finished file.
/// </summary>
public class UploadSession
{
    public Guid Id { get; set; }
    public int UserId { get; set; }
    public UploadSessionPurpose Purpose { get; set; }
    /// <summary>The record the file belongs to, e.g. the contract id.</summary>
    public int ContextId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public long ReceivedBytes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
