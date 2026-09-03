namespace NihomeBackend.Models;

public sealed class SeededRootDeletion
{
    public int Id { get; set; }
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceKey { get; set; } = string.Empty;
    public DateTime DeletedAt { get; set; } = DateTime.UtcNow;
    public int? DeletedByUserId { get; set; }
}