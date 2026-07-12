using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;

namespace nihomebackend.tests.Helpers;

/// <summary>No-op INotificationService for controller unit tests.</summary>
public sealed class NoOpNotificationService : INotificationService
{
    public Task<NotificationResponse> CreateAsync(
        int userId, string module, string title, string? body = null, string? linkUrl = null)
        => Task.FromResult(new NotificationResponse());

    public Task<int> CreateForAdminsAsync(
        string module, string title, string? body = null, string? linkUrl = null)
        => Task.FromResult(0);

    public Task<NotificationResponse?> NotifyFromTemplateAsync(
        int userId, string templateCode, IDictionary<string, string>? data = null,
        string? refEntityType = null, int? refEntityId = null, string? linkUrl = null,
        string languageCode = "vi")
        => Task.FromResult<NotificationResponse?>(null);

    public Task<int> NotifyManyFromTemplateAsync(
        IEnumerable<int> userIds, string templateCode, IDictionary<string, string>? data = null,
        string? refEntityType = null, int? refEntityId = null, string? linkUrl = null,
        string languageCode = "vi")
        => Task.FromResult(0);

    public Task<List<NotificationResponse>> GetForUserAsync(int userId, int skip = 0, int take = 20)
        => Task.FromResult(new List<NotificationResponse>());

    public Task<int> GetUnreadCountAsync(int userId) => Task.FromResult(0);

    public Task<NotificationResponse?> MarkReadAsync(long notificationId, int userId)
        => Task.FromResult<NotificationResponse?>(null);

    public Task<int> MarkAllReadAsync(int userId) => Task.FromResult(0);

    public Task<bool> DeleteAsync(long notificationId, int userId) => Task.FromResult(false);

    public Task<List<NotificationTemplate>> ListTemplatesAsync()
        => Task.FromResult(new List<NotificationTemplate>());

    public Task<NotificationTemplate?> GetTemplateAsync(string code)
        => Task.FromResult<NotificationTemplate?>(null);

    public Task<NotificationTemplate?> UpdateTemplateAsync(string code, NotificationChannel channel, bool isActive)
        => Task.FromResult<NotificationTemplate?>(null);
}
