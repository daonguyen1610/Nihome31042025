namespace NihomeBackend.Models;

public sealed class DepartmentWorkTaskAttachment
{
    public long Id { get; set; }
    public int WorkTaskId { get; set; }
    public DepartmentWorkTask WorkTask { get; set; } = null!;
    public string Path { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CreatedByUserId { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime? StorageDeletedAt { get; set; }
}
