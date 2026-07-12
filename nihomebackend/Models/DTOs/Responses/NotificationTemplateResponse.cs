namespace NihomeBackend.Models.DTOs.Responses;

/// <summary>
/// Admin-facing shape of a notification template. Titles / bodies are
/// referenced by translation key so the same template can render in any
/// language supported by the platform.
/// </summary>
public class NotificationTemplateResponse
{
    public string Code { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string TitleKey { get; set; } = string.Empty;
    public string BodyKey { get; set; } = string.Empty;

    /// <summary>Serialised as the enum name (e.g. "InApp"/"Email"/"Both").</summary>
    public string Channel { get; set; } = string.Empty;

    public bool IsActive { get; set; }
    public string? AdminDescription { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
