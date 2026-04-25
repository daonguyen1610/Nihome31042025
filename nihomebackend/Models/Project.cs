namespace NihomeBackend.Models;

public class Project
{
    public int Id { get; set; }
    public string Slug { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    /// <summary>JSON array of gallery image URLs.</summary>
    public string? GalleryJson { get; set; }
    public string Name { get; set; } = "";
    public string Client { get; set; } = "";
    public string Location { get; set; } = "";
    public string Scale { get; set; } = "";
    public string Scope { get; set; } = "";
    public string Status { get; set; } = "ongoing";
    public string? Year { get; set; }
    public string? Category { get; set; }
    public string? Description { get; set; }
    /// <summary>JSON array of challenge strings.</summary>
    public string? ChallengesJson { get; set; }
    /// <summary>JSON array of solution strings.</summary>
    public string? SolutionsJson { get; set; }
    /// <summary>JSON array of { label, value } objects.</summary>
    public string? HighlightsJson { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
