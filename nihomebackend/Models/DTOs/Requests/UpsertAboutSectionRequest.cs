using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public class UpsertAboutSectionRequest
{
    [Required] public string Slug { get; set; } = "";
    public string? ItemsJson { get; set; }
    [Required] public string Eyebrow { get; set; } = "";
    [Required] public string TitleA { get; set; } = "";
    [Required] public string TitleB { get; set; } = "";
    [Required] public string Paragraph1 { get; set; } = "";
    [Required] public string Paragraph2 { get; set; } = "";
    [Required] public string ImageUrl { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
