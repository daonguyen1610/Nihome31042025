namespace NihomeBackend.Models;

public class Notification
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public string Module { get; set; } = string.Empty;

    /// <summary>
    /// Stable template code the notification was rendered from
    /// (matches <see cref="NotificationTemplate.Code"/>). Nullable for
    /// legacy free-text notifications created before templates existed.
    /// </summary>
    public string? TemplateCode { get; set; }

    /// <summary>Entity type of the record this notification refers to (e.g. "Quote", "Contract").</summary>
    public string? RefEntityType { get; set; }

    /// <summary>Entity id of the record this notification refers to.</summary>
    public int? RefEntityId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public string? LinkUrl { get; set; }
    public bool IsRead { get; set; }

    /// <summary>Timestamp of the first time the user marked this notification read.</summary>
    public DateTime? ReadAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser User { get; set; } = null!;
}
