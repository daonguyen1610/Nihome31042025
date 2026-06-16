using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace NihomeBackend.Models.DTOs.Requests;

public class UpsertServiceRequest
{
    [Required] public string Slug { get; set; } = "";
    [Required] public string Title { get; set; } = "";
    [Required] public string ShortTitle { get; set; } = "";
    [Required] public string Tagline { get; set; } = "";
    [Required] public string Intro { get; set; } = "";
    [Required] public JsonElement Sections { get; set; }
    [Required] public string[] Highlights { get; set; } = [];
    public JsonElement IntroBlocks { get; set; }
    public int SortOrder { get; set; }
}
