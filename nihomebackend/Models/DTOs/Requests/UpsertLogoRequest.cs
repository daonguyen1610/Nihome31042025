using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public class UpsertLogoRequest
{
    [Required] public string Name { get; set; } = "";
    [Required] public string ImageUrl { get; set; } = "";
    public string? Href { get; set; }
    [Required] public string Kind { get; set; } = "Client";
    public int SortOrder { get; set; }
}
