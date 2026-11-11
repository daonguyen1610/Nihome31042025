using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NihomeBackend.Data;
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
        var service = new ProjectScheduleEmailService(db, sender, Mock.Of<INotificationService>(),
            NullLogger<ProjectScheduleEmailService>.Instance);

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
        db.Users.Add(new ApplicationUser
        {
            Id = 85,
            Role = UserRole.SUPER_ADMIN,
            PhoneNumber = "0900000085",
            Email = "operator@nihome.test",
        });
        var project = await db.OperationalProjects.SingleAsync(item => item.Id == message.OperationalProjectId);
        project.ProjectManagerUserId = 85;
        await db.SaveChangesAsync();
        var notifications = new Mock<INotificationService>();
        var service = new ProjectScheduleEmailService(db, sender, notifications.Object,
            NullLogger<ProjectScheduleEmailService>.Instance);

        for (var attempt = 0; attempt < ProjectScheduleEmailService.MaxAttempts; attempt++)
        {
            await MakeDueAsync(db, message.Id);
            await service.SendPendingAsync(default);
        }

        var exhausted = await db.ProjectScheduleEmails.AsNoTracking().SingleAsync(item => item.Id == message.Id);
        Assert.Equal(ProjectScheduleEmail.NeedsReviewStatus, exhausted.Status);
        Assert.Equal(ProjectScheduleEmailService.MaxAttempts, exhausted.AttemptCount);
        Assert.Equal(ProjectScheduleEmailService.MaxAttempts, sender.Attempts);
        notifications.Verify(service => service.NotifyFromTemplateAsync(85,
            "project.schedule.email-needs-review",
            It.Is<IDictionary<string, string>>(data => data["emailId"] == message.Id.ToString()),
            "ProjectScheduleEmail", message.Id,
            $"/admin/operational-projects/{message.OperationalProjectId}?tab=schedule", "vi"), Times.Once);

        await MakeDueAsync(db, message.Id);
        Assert.Equal(0, await service.SendPendingAsync(default));
        Assert.Equal(ProjectScheduleEmailService.MaxAttempts, sender.Attempts);
        notifications.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SendPending_DoesNotRetryExhaustedEmailWhenAdministratorAlertFails()
    {
        await using var db = DbContextFactory.Create();
        var message = await SeedQueuedAsync(db);
        db.Users.Add(new ApplicationUser
        {
            Id = 86,
            Role = UserRole.SUPER_ADMIN,
            PhoneNumber = "0900000086",
            Email = "operator@nihome.test",
        });
        var project = await db.OperationalProjects.SingleAsync(item => item.Id == message.OperationalProjectId);
        project.ProjectManagerUserId = 86;
        await db.SaveChangesAsync();
        var sender = new FlakyEmailService(failuresBeforeSuccess: int.MaxValue);
        var notifications = new Mock<INotificationService>();
        notifications.Setup(service => service.NotifyFromTemplateAsync(86,
                "project.schedule.email-needs-review", It.IsAny<IDictionary<string, string>>(),
                "ProjectScheduleEmail", message.Id,
                $"/admin/operational-projects/{message.OperationalProjectId}?tab=schedule", "vi"))
            .ThrowsAsync(new InvalidOperationException("notification store unavailable"));
        var service = new ProjectScheduleEmailService(db, sender, notifications.Object,
            NullLogger<ProjectScheduleEmailService>.Instance);

        for (var attempt = 0; attempt < ProjectScheduleEmailService.MaxAttempts; attempt++)
        {
            await MakeDueAsync(db, message.Id);
            await service.SendPendingAsync(default);
        }

        var exhausted = await db.ProjectScheduleEmails.AsNoTracking().SingleAsync(item => item.Id == message.Id);
        Assert.Equal(ProjectScheduleEmail.NeedsReviewStatus, exhausted.Status);
        Assert.Equal(0, await service.SendPendingAsync(default));
        Assert.Equal(ProjectScheduleEmailService.MaxAttempts, sender.Attempts);
    }

    [Fact]
    public async Task SendPending_CancelsApprovalEmailWhenApproverChanged()
    {
        await using var db = DbContextFactory.Create();
        var message = await SeedQueuedAsync(db);
        var revision = await db.ProjectScheduleRevisions.SingleAsync(item => item.Id == message.RevisionId);
        revision.ApproverUserId = 2;
        await db.SaveChangesAsync();
        var sender = new FlakyEmailService(failuresBeforeSuccess: 0);
        var service = new ProjectScheduleEmailService(db, sender, Mock.Of<INotificationService>(),
            NullLogger<ProjectScheduleEmailService>.Instance);

        Assert.Equal(0, await service.SendPendingAsync(default));

        var cancelled = await db.ProjectScheduleEmails.AsNoTracking().SingleAsync(item => item.Id == message.Id);
        Assert.Equal(ProjectScheduleEmail.CancelledStatus, cancelled.Status);
        Assert.Equal(0, sender.Attempts);
    }

    [Fact]
    public async Task SendPending_keepsNewerStateAndContinuesWhenCancelledWhileSending()
    {
        await using var db = DbContextFactory.CreateRelational();
        var message = await SeedQueuedAsync(db);
        var second = new ProjectScheduleEmail
        {
            OperationalProjectId = message.OperationalProjectId,
            RevisionId = message.RevisionId,
            RecipientUserId = 1,
            ToEmail = "approver@nihome.test",
            Subject = "NICON · Yêu cầu duyệt tiến độ · PJ-EMAIL · v1",
            Body = "<p>Tiến độ tổng thể</p>",
            NextAttemptAt = DateTime.UtcNow.AddMinutes(-1),
        };
        db.ProjectScheduleEmails.Add(second);
        await db.SaveChangesAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(db.Database.GetDbConnection())
            .Options;
        var sender = new CancellingEmailService(async () =>
        {
            await using var racing = new SqliteAppDbContext(options);
            var cancelled = await racing.ProjectScheduleEmails.SingleAsync(item => item.Id == message.Id);
            cancelled.Status = ProjectScheduleEmail.CancelledStatus;
            await racing.SaveChangesAsync();
        });
        var service = new ProjectScheduleEmailService(db, sender, Mock.Of<INotificationService>(),
            NullLogger<ProjectScheduleEmailService>.Instance);

        Assert.Equal(1, await service.SendPendingAsync(default));

        Assert.Equal(2, sender.Attempts);
        db.ChangeTracker.Clear();
        var kept = await db.ProjectScheduleEmails.AsNoTracking().SingleAsync(item => item.Id == message.Id);
        Assert.Equal(ProjectScheduleEmail.CancelledStatus, kept.Status);
        var delivered = await db.ProjectScheduleEmails.AsNoTracking().SingleAsync(item => item.Id == second.Id);
        Assert.Equal(ProjectScheduleEmail.SentStatus, delivered.Status);
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
            ApproverUserId = 1,
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

    private sealed class CancellingEmailService(Func<Task> cancelFirstMessage) : IEmailService
    {
        public int Attempts { get; private set; }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            Attempts++;
            if (Attempts == 1) await cancelFirstMessage();
        }

        public Task SendEmailWithAttachmentAsync(
            string toEmail, string subject, string htmlBody, EmailAttachment attachment,
            CancellationToken ct = default) => SendEmailAsync(toEmail, subject, htmlBody);
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
