namespace NihomeBackend.Models.DTOs.Responses;

public class NotificationResponse
{
    public long Id { get; set; }
    public string Module { get; set; } = string.Empty;

    /// <summary>
    /// Code of the template that produced this notification (nullable —
    /// legacy notifications created with the free-text overload have this
    /// left empty).
    /// </summary>
    public string? TemplateCode { get; set; }

    /// <summary>Entity type this notification refers to, e.g. "Quote", "Contract".</summary>
    public string? RefEntityType { get; set; }

    /// <summary>Entity id this notification refers to.</summary>
    public int? RefEntityId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public string? LinkUrl { get; set; }
    public bool IsRead { get; set; }

    /// <summary>Timestamp of first read; null when still unread.</summary>
    public DateTime? ReadAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
