namespace NihomeBackend.Models.DTOs.Responses;

public class ProcessResponse
{
    public int Id { get; set; }
    public string GroupKey { get; set; } = "";
    public string? Code { get; set; }
    public string Title { get; set; } = "";
    public int SortOrder { get; set; }
    public List<ProcessAssetResponse> Images { get; set; } = [];
    public List<ProcessAssetResponse> Files { get; set; } = [];
}

public class ProcessAssetResponse
{
    public int Id { get; set; }
    public string Type { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Url { get; set; } = "";
    public string OriginalFileName { get; set; } = "";
    public string? ContentType { get; set; }
    public long FileSizeBytes { get; set; }
    public int SortOrder { get; set; }
}

public class LegacyProcessImportResponse
{
    public bool DryRun { get; set; }
    public int Groups { get; set; }
    public int Processes { get; set; }
    public int Images { get; set; }
    public int Files { get; set; }
    public int SkippedAssets { get; set; }
    public string Message { get; set; } = "";
}
