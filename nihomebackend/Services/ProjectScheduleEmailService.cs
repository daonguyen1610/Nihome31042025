using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;

namespace NihomeBackend.Services;

/// <summary>
/// Drains the durable project schedule email outbox. Failed deliveries keep
/// their queued row and are retried with exponential backoff; after the final
/// attempt the row moves to "NeedsReview" so an operator can act on it instead
/// of the message disappearing.
/// </summary>
public sealed class ProjectScheduleEmailService(AppDbContext db, IEmailService email,
    INotificationService notifications,
    ILogger<ProjectScheduleEmailService> logger)
{
    public const int MaxAttempts = 5;
    private static readonly TimeSpan StaleClaimAfter = TimeSpan.FromMinutes(15);
    private const string SafeError =
        "Không thể gửi email tiến độ. Vui lòng kiểm tra cấu hình SMTP hoặc thử lại sau.";

    public static TimeSpan Backoff(int attemptCount) =>
        TimeSpan.FromMinutes(Math.Min(60, Math.Pow(2, Math.Max(0, attemptCount - 1))));

    public async Task<int> SendPendingAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var staleBefore = now - StaleClaimAfter;
        var ids = await db.ProjectScheduleEmails.AsNoTracking()
            .Where(item =>
                (item.Status == ProjectScheduleEmail.PendingStatus &&
                    (item.NextAttemptAt == null || item.NextAttemptAt <= now)) ||
                (item.Status == ProjectScheduleEmail.SendingStatus && item.LastAttemptAt < staleBefore))
            .OrderBy(item => item.Id)
            .Select(item => item.Id)
            .Take(20)
            .ToListAsync(ct);
        var sent = 0;
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            var message = await db.ProjectScheduleEmails
                .SingleOrDefaultAsync(item => item.Id == id, ct);
            if (message is null) continue;
            if (message.Status == ProjectScheduleEmail.SendingStatus &&
                message.LastAttemptAt >= staleBefore) continue;
            if (message.Status == ProjectScheduleEmail.PendingStatus &&
                message.NextAttemptAt > now) continue;

            message.Status = ProjectScheduleEmail.SendingStatus;
            message.LastAttemptAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            try
            {
                await email.SendEmailAsync(message.ToEmail, message.Subject, message.Body);
                message.Status = ProjectScheduleEmail.SentStatus;
                message.SentAt = DateTime.UtcNow;
                message.LastError = null;
                sent++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                message.AttemptCount++;
                message.LastError = SafeError;
                if (message.AttemptCount >= MaxAttempts)
                {
                    message.Status = ProjectScheduleEmail.NeedsReviewStatus;
                    logger.LogError(exception,
                        "Project schedule email {EmailId} for project {ProjectId} requires review after {Attempts} attempts.",
                        message.Id, message.OperationalProjectId, message.AttemptCount);
                }
                else
                {
                    message.Status = ProjectScheduleEmail.PendingStatus;
                    message.NextAttemptAt = DateTime.UtcNow.Add(Backoff(message.AttemptCount));
                    logger.LogWarning(exception,
                        "Project schedule email {EmailId} for project {ProjectId} failed on attempt {Attempt}; retry scheduled.",
                        message.Id, message.OperationalProjectId, message.AttemptCount);
                }
            }
            await db.SaveChangesAsync(ct);
            if (message.Status == ProjectScheduleEmail.NeedsReviewStatus)
            {
                try
                {
                    var adminIds = await db.Users.AsNoTracking()
                        .Where(user => user.IsActive &&
                            (user.Role == UserRole.SUPER_ADMIN || user.Role == UserRole.ADMIN))
                        .Select(user => user.Id).ToListAsync(ct);
                    foreach (var adminId in adminIds)
                    {
                        await notifications.NotifyFromTemplateAsync(adminId, "project.schedule.email-needs-review",
                            new Dictionary<string, string>
                            {
                                ["projectId"] = message.OperationalProjectId.ToString(),
                                ["revisionId"] = message.RevisionId.ToString(),
                                ["emailId"] = message.Id.ToString(),
                            }, "ProjectScheduleEmail", message.Id,
                            "/admin/email-templates?tab=projectSchedule#schedule-delivery");
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(exception,
                        "Could not alert administrators for project schedule email {EmailId} requiring review.", message.Id);
                }
            }
        }
        return sent;
    }
}
