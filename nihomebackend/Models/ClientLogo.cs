namespace NihomeBackend.Models;

public enum LogoKind
{
    Client,
    Partner,
    Supplier,
    Award
}

public class ClientLogo
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    public string? Href { get; set; }
    public LogoKind Kind { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
