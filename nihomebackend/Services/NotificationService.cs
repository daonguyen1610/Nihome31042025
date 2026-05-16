using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class NotificationService(AppDbContext db) : INotificationService
{
    private const int MaxPageSize = 100;

    public async Task<NotificationResponse> CreateAsync(
        int userId,
        string module,
        string title,
        string? body = null,
        string? linkUrl = null)
    {
        var notification = new Notification
        {
            UserId = userId,
            Module = module.Trim(),
            Title = title.Trim(),
            Body = string.IsNullOrWhiteSpace(body) ? null : body.Trim(),
            LinkUrl = string.IsNullOrWhiteSpace(linkUrl) ? null : linkUrl.Trim(),
        };

        db.Notifications.Add(notification);
        await db.SaveChangesAsync();

        return MapToResponse(notification);
    }

    public async Task<int> CreateForAdminsAsync(
        string module,
        string title,
        string? body = null,
        string? linkUrl = null)
    {
        var adminIds = await db.Users
            .AsNoTracking()
            .Where(u => u.IsActive && (u.Role == UserRole.SUPER_ADMIN || u.Role == UserRole.ADMIN))
            .Select(u => u.Id)
            .ToListAsync();

        if (adminIds.Count == 0) return 0;

        var now = DateTime.UtcNow;
        var notifications = adminIds.Select(userId => new Notification
        {
            UserId = userId,
            Module = module.Trim(),
            Title = title.Trim(),
            Body = string.IsNullOrWhiteSpace(body) ? null : body.Trim(),
            LinkUrl = string.IsNullOrWhiteSpace(linkUrl) ? null : linkUrl.Trim(),
            CreatedAt = now,
        });

        db.Notifications.AddRange(notifications);
        return await db.SaveChangesAsync();
    }

    public async Task<List<NotificationResponse>> GetForUserAsync(int userId, int skip = 0, int take = 20)
    {
        var safeSkip = Math.Max(0, skip);
        var safeTake = Math.Clamp(take, 1, MaxPageSize);

        var items = await db.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Skip(safeSkip)
            .Take(safeTake)
            .ToListAsync();

        return items.Select(MapToResponse).ToList();
    }

    public Task<int> GetUnreadCountAsync(int userId)
        => db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);

    public async Task<NotificationResponse?> MarkReadAsync(long notificationId, int userId)
    {
        var notification = await db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

        if (notification == null) return null;

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            await db.SaveChangesAsync();
        }

        return MapToResponse(notification);
    }

    public async Task<int> MarkAllReadAsync(int userId)
    {
        var items = await db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        if (items.Count == 0) return 0;

        foreach (var item in items)
        {
            item.IsRead = true;
        }

        await db.SaveChangesAsync();
        return items.Count;
    }

    public async Task<bool> DeleteAsync(long notificationId, int userId)
    {
        var notification = await db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

        if (notification == null) return false;

        db.Notifications.Remove(notification);
        await db.SaveChangesAsync();
        return true;
    }

    private static NotificationResponse MapToResponse(Notification notification) => new()
    {
        Id = notification.Id,
        Module = notification.Module,
        Title = notification.Title,
        Body = notification.Body,
        LinkUrl = notification.LinkUrl,
        IsRead = notification.IsRead,
        CreatedAt = notification.CreatedAt,
    };
}
