using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public interface INotificationService
{
    Task<NotificationResponse> CreateAsync(
        int userId,
        string module,
        string title,
        string? body = null,
        string? linkUrl = null);

    Task<int> CreateForAdminsAsync(
        string module,
        string title,
        string? body = null,
        string? linkUrl = null);

    /// <summary>
    /// Renders a stored template and delivers it through the template's
    /// configured channel (in-app row, email, or both). Placeholders in
    /// the template body use the <c>{{token}}</c> syntax and are filled
    /// from <paramref name="data"/> (case-insensitive keys, missing tokens
    /// are left in place so the failure is visible).
    ///
    /// Returns the created in-app <see cref="NotificationResponse"/> when
    /// the channel is <c>InApp</c> or <c>Both</c>; returns <c>null</c>
    /// for <c>Email</c>-only templates. If the template is missing or
    /// inactive the call is a no-op and returns <c>null</c>.
    /// </summary>
    Task<NotificationResponse?> NotifyFromTemplateAsync(
        int userId,
        string templateCode,
        IDictionary<string, string>? data = null,
        string? refEntityType = null,
        int? refEntityId = null,
        string? linkUrl = null,
        string languageCode = "vi");

    /// <summary>
    /// Fan-out variant of <see cref="NotifyFromTemplateAsync"/> — renders
    /// once per user and returns the count of in-app rows created.
    /// </summary>
    Task<int> NotifyManyFromTemplateAsync(
        IEnumerable<int> userIds,
        string templateCode,
        IDictionary<string, string>? data = null,
        string? refEntityType = null,
        int? refEntityId = null,
        string? linkUrl = null,
        string languageCode = "vi");

    Task<List<NotificationResponse>> GetForUserAsync(int userId, int skip = 0, int take = 20);
    Task<int> GetUnreadCountAsync(int userId);
    Task<NotificationResponse?> MarkReadAsync(long notificationId, int userId);
    Task<int> MarkAllReadAsync(int userId);
    Task<bool> DeleteAsync(long notificationId, int userId);

    // Template administration
    Task<List<NotificationTemplate>> ListTemplatesAsync();
    Task<NotificationTemplate?> GetTemplateAsync(string code);
    Task<NotificationTemplate?> UpdateTemplateAsync(string code, NotificationChannel channel, bool isActive);
}
