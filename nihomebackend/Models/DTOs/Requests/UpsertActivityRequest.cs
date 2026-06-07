using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public class UpsertActivityRequest
{
    [Required] public string Slug { get; set; } = "";
    [Required] public string Date { get; set; } = "";
    [Required] public string ImageUrl { get; set; } = "";
    public string[]? Gallery { get; set; }
    [Required] public string Category { get; set; } = "";
    public string? Author { get; set; }
    [Required] public string Title { get; set; } = "";
    [Required] public string Excerpt { get; set; } = "";
    [Required] public string[] Content { get; set; } = [];
    public int SortOrder { get; set; }
}
