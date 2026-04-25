using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public class UpsertProcessRequest
{
    [Required] public string GroupKey { get; set; } = "";
    public string? Code { get; set; }
    [Required] public string Title { get; set; } = "";
    public int SortOrder { get; set; }
}
