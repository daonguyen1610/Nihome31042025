namespace NihomeBackend.Models;

public class ProcessDocument
{
    public int Id { get; set; }
    public string GroupKey { get; set; } = "";
    public string? Code { get; set; }
    public string Title { get; set; } = "";
    public int SortOrder { get; set; }
    public string? ImagesJson { get; set; }
    public string? FilesJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
