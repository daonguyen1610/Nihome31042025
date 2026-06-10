using System.Text.Json;

namespace NihomeBackend.Models.DTOs.Responses;

public class ProjectResponse
{
    public int Id { get; set; }
    public string Slug { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    public string[]? Gallery { get; set; }
    public string Name { get; set; } = "";
    public string Client { get; set; } = "";
    public string Location { get; set; } = "";
    public string Scale { get; set; } = "";
    public string Scope { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Year { get; set; }
    public string? Category { get; set; }
    public int? CategoryId { get; set; }
    public string? Description { get; set; }
    public string[]? Challenges { get; set; }
    public string[]? Solutions { get; set; }
    public JsonElement? Highlights { get; set; }
}
