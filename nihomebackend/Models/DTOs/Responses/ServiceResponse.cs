using System.Text.Json;

namespace NihomeBackend.Models.DTOs.Responses;

public class ServiceResponse
{
    public int Id { get; set; }
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string ShortTitle { get; set; } = "";
    public string Tagline { get; set; } = "";
    public string Intro { get; set; } = "";
    public JsonElement Sections { get; set; }
    public string[] Highlights { get; set; } = [];
}
