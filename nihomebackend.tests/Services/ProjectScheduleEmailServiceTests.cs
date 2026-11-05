using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NihomeBackend.Models;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public sealed class ProjectScheduleEmailServiceTests
{
    [Fact]
    public async Task SendPending_RetriesTransientFailureAndSendsOnNextRun()
    {
        await using var db = DbContextFactory.Create();
        var message = await SeedQueuedAsync(db);
        var sender = new FlakyEmailService(failuresBeforeSuccess: 1);
        var service = new ProjectScheduleEmailService(db, sender, NullLogger<ProjectScheduleEmailService>.Instance);

        Assert.Equal(0, await service.SendPendingAsync(default));

        var failed = await db.ProjectScheduleEmails.AsNoTracking().SingleAsync(item => item.Id == message.Id);
        Assert.Equal(ProjectScheduleEmail.PendingStatus, failed.Status);
        Assert.Equal(1, failed.AttemptCount);
        Assert.NotNull(failed.NextAttemptAt);
        Assert.True(failed.NextAttemptAt > DateTime.UtcNow);
        Assert.NotNull(failed.LastError);

        await MakeDueAsync(db, message.Id);
        Assert.Equal(1, await service.SendPendingAsync(default));

        var sent = await db.ProjectScheduleEmails.AsNoTracking().SingleAsync(item => item.Id == message.Id);
        Assert.Equal(ProjectScheduleEmail.SentStatus, sent.Status);
        Assert.NotNull(sent.SentAt);
        Assert.Null(sent.LastError);
        Assert.Equal(2, sender.Attempts);
    }

    [Fact]
    public async Task SendPending_StopsAfterMaxAttemptsAndKeepsMessageForReview()
    {
        await using var db = DbContextFactory.Create();
        var message = await SeedQueuedAsync(db);
        var sender = new FlakyEmailService(failuresBeforeSuccess: int.MaxValue);
        var service = new ProjectScheduleEmailService(db, sender, NullLogger<ProjectScheduleEmailService>.Instance);

        for (var attempt = 0; attempt < ProjectScheduleEmailService.MaxAttempts; attempt++)
        {
            await MakeDueAsync(db, message.Id);
            await service.SendPendingAsync(default);
        }

        var exhausted = await db.ProjectScheduleEmails.AsNoTracking().SingleAsync(item => item.Id == message.Id);
        Assert.Equal(ProjectScheduleEmail.NeedsReviewStatus, exhausted.Status);
        Assert.Equal(ProjectScheduleEmailService.MaxAttempts, exhausted.AttemptCount);
        Assert.Equal(ProjectScheduleEmailService.MaxAttempts, sender.Attempts);

        await MakeDueAsync(db, message.Id);
        Assert.Equal(0, await service.SendPendingAsync(default));
        Assert.Equal(ProjectScheduleEmailService.MaxAttempts, sender.Attempts);
    }

    private static async Task<ProjectScheduleEmail> SeedQueuedAsync(NihomeBackend.Data.AppDbContext db)
    {
        var customer = new Customer { Name = "Schedule email customer" };
        var project = new OperationalProject
        {
            Code = "PJ-EMAIL",
            Name = "Công trình gửi email tiến độ",
            Customer = customer,
        };
        db.OperationalProjects.Add(project);
        await db.SaveChangesAsync();
        var revision = new ProjectScheduleRevision
        {
            OperationalProjectId = project.Id,
            Version = 1,
            Status = ProjectScheduleRevisionStatus.PendingApproval,
            CreatedByUserId = 1,
            RowVersion = [1],
        };
        db.ProjectScheduleRevisions.Add(revision);
        await db.SaveChangesAsync();
        var message = new ProjectScheduleEmail
        {
            OperationalProjectId = project.Id,
            RevisionId = revision.Id,
            RecipientUserId = 1,
            ToEmail = "approver@nihome.test",
            Subject = "NICON · Yêu cầu duyệt tiến độ · PJ-EMAIL · v1",
            Body = "<p>Tiến độ tổng thể</p>",
            NextAttemptAt = DateTime.UtcNow.AddMinutes(-1),
        };
        db.ProjectScheduleEmails.Add(message);
        await db.SaveChangesAsync();
        return message;
    }

    private static async Task MakeDueAsync(NihomeBackend.Data.AppDbContext db, int messageId)
    {
        var message = await db.ProjectScheduleEmails.SingleAsync(item => item.Id == messageId);
        message.NextAttemptAt = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();
    }

    private sealed class FlakyEmailService(int failuresBeforeSuccess) : IEmailService
    {
        public int Attempts { get; private set; }

        public Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            Attempts++;
            if (Attempts <= failuresBeforeSuccess)
                throw new InvalidOperationException("SMTP unavailable");
            return Task.CompletedTask;
        }

        public Task SendEmailWithAttachmentAsync(
            string toEmail, string subject, string htmlBody, EmailAttachment attachment,
            CancellationToken ct = default) => SendEmailAsync(toEmail, subject, htmlBody);
    }
}
