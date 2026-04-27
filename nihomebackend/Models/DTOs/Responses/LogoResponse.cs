namespace NihomeBackend.Models.DTOs.Responses;

public class LogoResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    public string? Href { get; set; }
    public string Kind { get; set; } = "";
    public int SortOrder { get; set; }
}

public class LogosGroupedResponse
{
    public LogoResponse[] Clients { get; set; } = [];
    public LogoResponse[] Partners { get; set; } = [];
    public LogoResponse[] Suppliers { get; set; } = [];
    public LogoResponse[] Awards { get; set; } = [];
}
