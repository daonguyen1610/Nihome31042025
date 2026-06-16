namespace NihomeBackend.Models;

public class ServiceItem
{
    public int Id { get; set; }
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string ShortTitle { get; set; } = "";
    public string Tagline { get; set; } = "";
    public string Intro { get; set; } = "";
    /// <summary>JSON array of { heading, body: string[] } objects.</summary>
    public string SectionsJson { get; set; } = "[]";
    /// <summary>JSON array of highlight strings.</summary>
    public string HighlightsJson { get; set; } = "[]";
    /// <summary>JSON array of { text: string, imageUrl?: string } blocks for rich intro content.</summary>
    public string IntroBlocksJson { get; set; } = "[]";
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
