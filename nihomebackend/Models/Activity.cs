namespace NihomeBackend.Models;

public class Activity
{
    public int Id { get; set; }
    public string Slug { get; set; } = "";
    public string Date { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    /// <summary>JSON array of additional gallery image URLs.</summary>
    public string? GalleryJson { get; set; }
    public string Category { get; set; } = "";
    public string? Author { get; set; }
    public string Title { get; set; } = "";
    public string Excerpt { get; set; } = "";
    /// <summary>JSON array of paragraphs.</summary>
    public string ContentJson { get; set; } = "[]";
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
