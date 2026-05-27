namespace NihomeBackend.Models;

public enum ProcessAssetType
{
    Image,
    File
}

public class ProcessAsset
{
    public int Id { get; set; }
    public int ProcessDocumentId { get; set; }
    public ProcessDocument? ProcessDocument { get; set; }
    public ProcessAssetType Type { get; set; }
    public string DisplayName { get; set; } = "";
    public string Url { get; set; } = "";
    public string OriginalFileName { get; set; } = "";
    public string? ContentType { get; set; }
    public long FileSizeBytes { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
