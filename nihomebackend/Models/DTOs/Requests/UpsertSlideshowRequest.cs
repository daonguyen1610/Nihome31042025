using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public class UpsertSlideshowRequest
{
    [Required] public string Slug { get; set; } = "";
    [Required] public string ImageUrl { get; set; } = "";
    [Required] public string Title { get; set; } = "";
    public string? Subtitle { get; set; }
    public string? LinkUrl { get; set; }
    public string? LinkText { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
