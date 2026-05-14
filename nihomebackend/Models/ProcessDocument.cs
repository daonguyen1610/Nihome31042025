namespace NihomeBackend.Models;

public class ProcessDocument
{
    public int Id { get; set; }
    public string GroupKey { get; set; } = "";
    public string? Code { get; set; }
    public string Title { get; set; } = "";
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<ProcessAsset> Assets { get; set; } = [];
}
