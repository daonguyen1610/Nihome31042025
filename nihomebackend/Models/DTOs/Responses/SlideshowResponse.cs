namespace NihomeBackend.Models.DTOs.Responses;

public class SlideshowResponse
{
    public int Id { get; set; }
    public string Slug { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Subtitle { get; set; }
    public string? LinkUrl { get; set; }
    public string? LinkText { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}
