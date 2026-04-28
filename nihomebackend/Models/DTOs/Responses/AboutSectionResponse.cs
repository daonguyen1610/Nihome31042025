namespace NihomeBackend.Models.DTOs.Responses;

public class AboutSectionResponse
{
    public int Id { get; set; }
    public string Slug { get; set; } = "";
    public string? ItemsJson { get; set; }
    public string Eyebrow { get; set; } = "";
    public string TitleA { get; set; } = "";
    public string TitleB { get; set; } = "";
    public string Paragraph1 { get; set; } = "";
    public string Paragraph2 { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}
