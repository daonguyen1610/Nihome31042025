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
        "Không thể gửi email tiến độ. Người quản lý dự án cần kiểm tra với người nhận trước khi gửi lại.";

    public static TimeSpan Backoff(int attemptCount) =>
        TimeSpan.FromMinutes(Math.Min(60, Math.Pow(2, Math.Max(0, attemptCount - 1))));

    public async Task<List<ProjectScheduleEmailReviewItem>> GetNeedsReviewAsync(int projectId, int callerUserId, CancellationToken ct)
    {
        if (!await IsProjectManagerAsync(projectId, callerUserId, ct))
            throw new ProjectScheduleOperationException("Chỉ người quản lý dự án được kiểm tra email của dự án.", 403);
        return await db.ProjectScheduleEmails.AsNoTracking()
            .Where(item => item.OperationalProjectId == projectId && item.Status == ProjectScheduleEmail.NeedsReviewStatus)
            .OrderByDescending(item => item.LastAttemptAt)
            .Select(item => new ProjectScheduleEmailReviewItem(item.Id, projectId, item.RevisionId,
                item.CmScheduleBaselineId.HasValue ? item.CmScheduleBaseline!.Version : item.Revision.Version, item.Revision.OperationalProject.Code, item.AttemptCount,
                item.LastAttemptAt, item.ToEmail, item.Subject, item.Body))
            .ToListAsync(ct);
    }

    public async Task ResolveAsync(int projectId, int emailId, int callerUserId, string decision, CancellationToken ct)
    {
        if (!await IsProjectManagerAsync(projectId, callerUserId, ct))
            throw new ProjectScheduleOperationException("Chỉ người quản lý dự án được xử lý email của dự án.", 403);
        if (decision is not ("ConfirmReceived" or "Retry"))
            throw new ProjectScheduleOperationException("Hãy chọn Đã nhận hoặc Gửi lại.");
        var now = DateTime.UtcNow;
        var message = await db.ProjectScheduleEmails.SingleOrDefaultAsync(item => item.Id == emailId &&
            item.OperationalProjectId == projectId && item.Status == ProjectScheduleEmail.NeedsReviewStatus, ct);
        if (message is null)
            throw new ProjectScheduleOperationException("Email đã được xử lý hoặc không thuộc dự án này. Hãy tải lại danh sách.", 409);
        message.Status = decision == "Retry" ? ProjectScheduleEmail.PendingStatus : ProjectScheduleEmail.ConfirmedReceivedStatus;
        message.ReviewedByUserId = callerUserId;
        message.ReviewedAt = now;
        message.NextAttemptAt = decision == "Retry" ? now : null;
        if (decision == "Retry") message.AttemptCount = 0;
        message.LastError = null;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            throw new ProjectScheduleOperationException("Email đã được xử lý. Hãy tải lại danh sách.", 409);
        }
        logger.LogInformation("Project schedule email {EmailId} for project {ProjectId} reviewed by manager {UserId}: {Decision}.",
            emailId, projectId, callerUserId, decision);
    }

    private Task<bool> IsProjectManagerAsync(int projectId, int callerUserId, CancellationToken ct) =>
        db.OperationalProjects.AsNoTracking().AnyAsync(project =>
            project.Id == projectId && project.ProjectManagerUserId == callerUserId, ct);

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
            if (message.Status is not (ProjectScheduleEmail.PendingStatus or ProjectScheduleEmail.SendingStatus)) continue;
            if (message.CmScheduleBaselineId.HasValue && !await db.CmScheduleBaselines.AnyAsync(x => x.Id == message.CmScheduleBaselineId && x.Status == "PendingApproval", ct))
            {
                message.Status = ProjectScheduleEmail.CancelledStatus;
                try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { }
                continue;
            }
            // A master-schedule approval notice stops being valid as soon as the
            // revision leaves PendingApproval or the designated approver changes.
            // Publication notices are queued only after the revision is Published.
            if (!message.CmScheduleBaselineId.HasValue)
            {
                var revision = await db.ProjectScheduleRevisions.AsNoTracking()
                    .Where(item => item.Id == message.RevisionId)
                    .Select(item => new { item.Status, item.ApproverUserId, item.PublishedAt })
                    .SingleOrDefaultAsync(ct);
                // An approval notice is only useful while the revision waits for that
                // approver. Once published, only notices queued at publication time
                // remain valid; an older approval notice is stale even though its
                // recipient may also be a publication recipient.
                var stillValid = revision is not null &&
                    (revision.Status == ProjectScheduleRevisionStatus.PendingApproval &&
                        message.RecipientUserId == revision.ApproverUserId ||
                     revision.Status == ProjectScheduleRevisionStatus.Published &&
                        revision.PublishedAt.HasValue && message.CreatedAt >= revision.PublishedAt.Value);
                if (!stillValid)
                {
                    message.Status = ProjectScheduleEmail.CancelledStatus;
                    try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { }
                    continue;
                }
            }
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
                    var managerId = await db.OperationalProjects.AsNoTracking()
                        .Where(project => project.Id == message.OperationalProjectId)
                        .Select(project => project.ProjectManagerUserId).SingleOrDefaultAsync(ct);
                    if (managerId.HasValue)
                    {
                        await notifications.NotifyFromTemplateAsync(managerId.Value, "project.schedule.email-needs-review",
                            new Dictionary<string, string>
                            {
                                ["projectId"] = message.OperationalProjectId.ToString(),
                                ["revisionId"] = message.RevisionId.ToString(),
                                ["emailId"] = message.Id.ToString(),
                            }, "ProjectScheduleEmail", message.Id,
                            message.CmScheduleBaselineId.HasValue ? $"/admin/construction/progress?projectId={message.OperationalProjectId}" : $"/admin/operational-projects/{message.OperationalProjectId}?tab=schedule");
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(exception,
                        "Could not alert the project manager for schedule email {EmailId} requiring review.", message.Id);
                }
            }
        }
        return sent;
    }
}
