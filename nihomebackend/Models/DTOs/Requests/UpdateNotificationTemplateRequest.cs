using NihomeBackend.Models;

namespace NihomeBackend.Models.DTOs.Requests;

/// <summary>
/// Admin payload used to toggle a notification template's delivery channel
/// and enabled state. Title/body content is edited through the standard
/// translation admin because it lives in the shared translation table
/// (keys: <c>notification.&lt;code&gt;.title</c> / <c>...body</c>).
/// </summary>
public class UpdateNotificationTemplateRequest
{
    public NotificationChannel Channel { get; set; } = NotificationChannel.InApp;
    public bool IsActive { get; set; } = true;
}
