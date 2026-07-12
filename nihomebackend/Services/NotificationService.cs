using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class NotificationService(
    AppDbContext db,
    IEmailService email,
    TranslationService translations,
    ILogger<NotificationService> logger) : INotificationService
{
    private const int MaxPageSize = 100;
    private static readonly Regex PlaceholderRegex = new(@"\{\{\s*(?<key>[a-zA-Z0-9_\-\.]+)\s*\}\}", RegexOptions.Compiled);

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

    public async Task<NotificationResponse?> NotifyFromTemplateAsync(
        int userId,
        string templateCode,
        IDictionary<string, string>? data = null,
        string? refEntityType = null,
        int? refEntityId = null,
        string? linkUrl = null,
        string languageCode = "vi")
    {
        var template = await db.NotificationTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Code == templateCode);
        if (template == null || !template.IsActive)
        {
            logger.LogWarning(
                "NotifyFromTemplateAsync skipped: template '{Code}' missing or inactive.", templateCode);
            return null;
        }

        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null || !user.IsActive)
        {
            logger.LogWarning(
                "NotifyFromTemplateAsync skipped: user {UserId} missing or inactive.", userId);
            return null;
        }

        var rendered = await RenderAsync(template, data, languageCode);
        NotificationResponse? result = null;

        if (template.Channel is NotificationChannel.InApp or NotificationChannel.Both)
        {
            var row = new Notification
            {
                UserId = userId,
                Module = template.Module,
                TemplateCode = template.Code,
                RefEntityType = refEntityType,
                RefEntityId = refEntityId,
                Title = rendered.Title,
                Body = rendered.Body,
                LinkUrl = string.IsNullOrWhiteSpace(linkUrl) ? null : linkUrl.Trim(),
            };
            db.Notifications.Add(row);
            await db.SaveChangesAsync();
            result = MapToResponse(row);
        }

        if (template.Channel is NotificationChannel.Email or NotificationChannel.Both)
        {
            if (string.IsNullOrWhiteSpace(user.Email))
            {
                logger.LogInformation(
                    "Email skipped for template {Code} → user {UserId}: no email on file.",
                    template.Code, userId);
            }
            else
            {
                try
                {
                    await email.SendEmailAsync(user.Email, rendered.Title, rendered.Body ?? rendered.Title);
                }
                catch (Exception ex)
                {
                    // Best-effort in Phase 1: log the failure but do not roll back the in-app row.
                    // A durable retry queue is scoped for a follow-up ticket.
                    logger.LogError(
                        ex,
                        "Email delivery failed for template {Code} → user {UserId}",
                        template.Code, userId);
                }
            }
        }

        return result;
    }

    public async Task<int> NotifyManyFromTemplateAsync(
        IEnumerable<int> userIds,
        string templateCode,
        IDictionary<string, string>? data = null,
        string? refEntityType = null,
        int? refEntityId = null,
        string? linkUrl = null,
        string languageCode = "vi")
    {
        var ids = userIds?.Distinct().ToList() ?? new List<int>();
        if (ids.Count == 0) return 0;

        var count = 0;
        foreach (var userId in ids)
        {
            var res = await NotifyFromTemplateAsync(
                userId, templateCode, data, refEntityType, refEntityId, linkUrl, languageCode);
            if (res != null) count++;
        }

        return count;
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
            notification.ReadAt = DateTime.UtcNow;
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

        var now = DateTime.UtcNow;
        foreach (var item in items)
        {
            item.IsRead = true;
            item.ReadAt = now;
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

    public Task<List<NotificationTemplate>> ListTemplatesAsync()
        => db.NotificationTemplates
            .AsNoTracking()
            .OrderBy(t => t.Module)
            .ThenBy(t => t.Code)
            .ToListAsync();

    public Task<NotificationTemplate?> GetTemplateAsync(string code)
        => db.NotificationTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Code == code);

    public async Task<NotificationTemplate?> UpdateTemplateAsync(string code, NotificationChannel channel, bool isActive)
    {
        var template = await db.NotificationTemplates
            .FirstOrDefaultAsync(t => t.Code == code);
        if (template == null) return null;

        template.Channel = channel;
        template.IsActive = isActive;
        template.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return template;
    }

    // ---------------- helpers ----------------

    private async Task<RenderedTemplate> RenderAsync(
        NotificationTemplate template,
        IDictionary<string, string>? data,
        string languageCode)
    {
        var normalizedLang = string.IsNullOrWhiteSpace(languageCode) ? "vi" : languageCode.Trim().ToLowerInvariant();
        var map = await translations.GetTranslationMapAsync(normalizedLang);

        var titleTemplate = map.GetValueOrDefault(template.TitleKey) ?? template.TitleKey;
        var bodyTemplate = map.GetValueOrDefault(template.BodyKey);

        return new RenderedTemplate(
            RenderPlaceholders(titleTemplate, data),
            bodyTemplate == null ? null : RenderPlaceholders(bodyTemplate, data));
    }

    private static string RenderPlaceholders(string template, IDictionary<string, string>? data)
    {
        if (string.IsNullOrEmpty(template) || data == null || data.Count == 0) return template;

        var caseInsensitive = new Dictionary<string, string>(data, StringComparer.OrdinalIgnoreCase);
        return PlaceholderRegex.Replace(template, match =>
        {
            var key = match.Groups["key"].Value;
            return caseInsensitive.TryGetValue(key, out var value) ? value : match.Value;
        });
    }

    private static NotificationResponse MapToResponse(Notification notification) => new()
    {
        Id = notification.Id,
        Module = notification.Module,
        TemplateCode = notification.TemplateCode,
        RefEntityType = notification.RefEntityType,
        RefEntityId = notification.RefEntityId,
        Title = notification.Title,
        Body = notification.Body,
        LinkUrl = notification.LinkUrl,
        IsRead = notification.IsRead,
        ReadAt = notification.ReadAt.HasValue
            ? DateTime.SpecifyKind(notification.ReadAt.Value, DateTimeKind.Utc)
            : null,
        CreatedAt = DateTime.SpecifyKind(notification.CreatedAt, DateTimeKind.Utc),
    };

    private sealed record RenderedTemplate(string Title, string? Body);
}
