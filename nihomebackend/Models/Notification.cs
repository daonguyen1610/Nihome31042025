namespace NihomeBackend.Models;

public class Notification
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public string Module { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public string? LinkUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser User { get; set; } = null!;
}
