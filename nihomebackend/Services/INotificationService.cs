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

    Task<List<NotificationResponse>> GetForUserAsync(int userId, int skip = 0, int take = 20);
    Task<int> GetUnreadCountAsync(int userId);
    Task<NotificationResponse?> MarkReadAsync(long notificationId, int userId);
    Task<int> MarkAllReadAsync(int userId);
    Task<bool> DeleteAsync(long notificationId, int userId);
}
