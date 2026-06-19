namespace NihomeBackend.Models.DTOs.Responses;

public class NewsResponse
{
    public int Id { get; set; }
    public string Slug { get; set; } = "";
    public string Date { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    public string[]? Gallery { get; set; }
    public string Category { get; set; } = "";
    public int? NewsCategoryId { get; set; }
    public string Title { get; set; } = "";
    public string Excerpt { get; set; } = "";
    public object[] Content { get; set; } = [];
}
