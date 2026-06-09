using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace NihomeBackend.Models.DTOs.Requests;

public class UpsertProjectRequest
{
    [Required] public string Slug { get; set; } = "";
    [Required] public string ImageUrl { get; set; } = "";
    public string[]? Gallery { get; set; }
    [Required] public string Name { get; set; } = "";
    [Required] public string Client { get; set; } = "";
    [Required] public string Location { get; set; } = "";
    public string Scale { get; set; } = "";
    [Required] public string Scope { get; set; } = "";
    [Required] public string Status { get; set; } = "ongoing";
    public string? Year { get; set; }
    public string? Category { get; set; }
    public string? Description { get; set; }
    public string[]? Challenges { get; set; }
    public string[]? Solutions { get; set; }
    public JsonElement? Highlights { get; set; }
    public int SortOrder { get; set; }
}
