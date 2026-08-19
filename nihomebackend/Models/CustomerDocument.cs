namespace NihomeBackend.Models;

public class CustomerDocument
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public string FilePath { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = "application/octet-stream";
    public string? Label { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? UploadedByUserId { get; set; }
    public ApplicationUser? UploadedBy { get; set; }
}