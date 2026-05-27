using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public class UpsertProcessRequest
{
    [Required] public string GroupKey { get; set; } = "";
    public string? Code { get; set; }
    [Required] public string Title { get; set; } = "";
    public int SortOrder { get; set; }
    public List<UpsertProcessAssetRequest>? Images { get; set; }
    public List<UpsertProcessAssetRequest>? Files { get; set; }
}

public class UpsertProcessAssetRequest
{
    public int? Id { get; set; }
    [Required] public string DisplayName { get; set; } = "";
    [Required] public string Url { get; set; } = "";
    public string? OriginalFileName { get; set; }
    public string? ContentType { get; set; }
    public long FileSizeBytes { get; set; }
    public int SortOrder { get; set; }
}
