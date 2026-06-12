using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public class UpsertProcessRequest
{
    [Required] public string GroupKey { get; set; } = "";
    public string? Code { get; set; }
    [Required] public string Title { get; set; } = "";
    public int SortOrder { get; set; }
    public List<ProcessAssetInput> Images { get; set; } = [];
    public List<ProcessAssetInput> Files { get; set; } = [];
}

public class ProcessAssetInput
{
    public string DisplayName { get; set; } = "";
    public string Url { get; set; } = "";
    public string OriginalFileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public int SortOrder { get; set; }
}
