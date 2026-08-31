namespace NihomeBackend.Models.DTOs.Responses;

public sealed class ProjectDocumentCategoryResponse
{
    public string Value { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public string TranslationKey { get; set; } = string.Empty;
}

public sealed class ProjectDocumentResponse
{
    public long Id { get; set; }
    public int OperationalProjectId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string SourceModule { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string? SourceEntityType { get; set; }
    public string? SourceSlot { get; set; }
    public long? SourceRecordId { get; set; }
    public int? CustomerId { get; set; }
    public int? ContractId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public long Generation { get; set; }
    public string DesiredOperation { get; set; } = string.Empty;
    public string SyncStatus { get; set; } = string.Empty;
    public int SyncAttemptCount { get; set; }
    public int MaxSyncAttempts { get; set; }
    public string? SyncError { get; set; }
    public DateTime? NextSyncAttemptAt { get; set; }
    public string? DriveWebViewLink { get; set; }
    public DateTime? DriveModifiedAt { get; set; }
    public bool IsDownloadable { get; set; }
    public string? UnsupportedReason { get; set; }
    public string ConflictState { get; set; } = string.Empty;
    public long? ConflictWithDocumentId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
