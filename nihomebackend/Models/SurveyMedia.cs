namespace NihomeBackend.Models;

public class SurveyMedia
{
    public long Id { get; set; }
    public int SurveyId { get; set; }
    public Survey Survey { get; set; } = null!;
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public long Size { get; set; }
    public string? Note { get; set; }
    public DateTime? CapturedAt { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string RelativePath { get; set; } = string.Empty;
    public string? DriveFileId { get; set; }
    public string? DriveFolderId { get; set; }
    public string? DriveFolderLink { get; set; }
    public SurveyMediaSyncStatus SyncStatus { get; set; } = SurveyMediaSyncStatus.Pending;
    public int SyncAttemptCount { get; set; }
    public DateTime? NextSyncAttemptAt { get; set; }
    public string? SyncError { get; set; }
    public DateTime? SyncStartedAt { get; set; }
    public DateTime? LastSyncAttemptAt { get; set; }
    public DateTime? SyncedAt { get; set; }
    public Guid? ClaimToken { get; set; }
    public DateTime? ClaimExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedByUserId { get; set; }
}

public enum SurveyMediaSyncStatus
{
    Pending = 0,
    Processing = 1,
    Synced = 2,
    Failed = 3,
}

public class SurveyChecklistResult
{
    public long Id { get; set; }
    public int SurveyId { get; set; }
    public Survey Survey { get; set; } = null!;
    public string TemplateCode { get; set; } = string.Empty;
    public string TemplateTitle { get; set; } = string.Empty;
    public SurveyChecklistStatus? Status { get; set; }
    public string? Note { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedByUserId { get; set; }
}

public enum SurveyChecklistStatus
{
    Ok = 0,
    NeedsAttention = 1,
    Failed = 2,
}