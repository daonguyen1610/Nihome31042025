using System.Data.Common;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NihomeBackend.Controllers;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;

namespace NihomeBackend.IntegrationTests.Infrastructure;

public sealed class CmScheduleConcurrencyTests
{
    [SqlEvidenceFact]
    public async Task SimultaneousCaptures_ReturnOneSnapshotAndConflictInsteadOfServerError()
    {
        await using var fixture = await ScheduleDatabase.CreateAsync();
        var barrier = new BeforeSave();
        await using var left = fixture.Context(barrier);
        await using var right = fixture.Context(barrier);
        var results = await Task.WhenAll(fixture.Controller(left).Capture(fixture.ProjectId, default),
            fixture.Controller(right).Capture(fixture.ProjectId, default));
        results.Should().ContainSingle(x => x is OkObjectResult);
        results.Should().ContainSingle(x => x is ConflictObjectResult);
        await using var check = fixture.Context();
        (await check.CmProgressSnapshots.CountAsync()).Should().Be(1);
        (await check.Notifications.CountAsync()).Should().Be(1);
        (await fixture.Controller(check).Capture(fixture.ProjectId, default)).Should().BeOfType<OkObjectResult>();
        (await check.CmProgressSnapshots.CountAsync()).Should().Be(1);
    }

    [SqlEvidenceFact]
    public async Task SimultaneousActualWrites_PreserveWinnerAndAtomicSnapshot()
    {
        await using var fixture = await ScheduleDatabase.CreateAsync();
        var barrier = new BeforeSave();
        await using var left = fixture.Context(barrier);
        await using var right = fixture.Context(barrier);
        var results = await Task.WhenAll(
            fixture.Controller(left).UpdateActual(fixture.ProjectId, fixture.TaskId, new CmActualUpdateRequest { ExpectedUpdatedAt = fixture.UpdatedAt, ProgressPercent = 80 }, default),
            fixture.Controller(right).UpdateActual(fixture.ProjectId, fixture.TaskId, new CmActualUpdateRequest { ExpectedUpdatedAt = fixture.UpdatedAt, ProgressPercent = 20 }, default));
        results.Should().ContainSingle(x => x is OkObjectResult);
        results.Should().ContainSingle(x => x is ConflictObjectResult);
        await using var check = fixture.Context();
        var task = await check.ConstructionTasks.SingleAsync();
        var snapshot = await check.CmProgressSnapshots.SingleAsync();
        snapshot.ActualPercent.Should().Be(task.ProgressPercent);
        task.ProgressPercent.Should().BeOneOf(20, 80);
        await Assert.ThrowsAsync<CrmConcurrencyException>(() => fixture.Controller(check).UpdateActual(fixture.ProjectId, fixture.TaskId,
            new CmActualUpdateRequest { ExpectedUpdatedAt = fixture.UpdatedAt, ProgressPercent = 1 }, default));
        // Global middleware maps this exception in the full pipeline; direct
        // controller execution above only maps actual SQL deadlock conflicts.
    }

    [SqlEvidenceFact]
    public async Task CancellationAfterOutboxSelection_DoesNotSendObsoleteApproval()
    {
        await using var fixture = await ScheduleDatabase.CreateAsync();
        await using (var setup = fixture.Context())
        {
            var baseline = await setup.CmScheduleBaselines.SingleAsync();
            baseline.Status = "PendingApproval";
            setup.ProjectScheduleEmails.Add(new ProjectScheduleEmail { OperationalProjectId = fixture.ProjectId, RevisionId = baseline.CrmRevisionId, CmScheduleBaselineId = baseline.Id, ToEmail = "pm@example.test", Subject = "CM approval", Body = "<p>CM approval</p>" });
            await setup.SaveChangesAsync();
        }
        var intercept = new BeforeEmailReload(async () =>
        {
            await using var cancel = fixture.Context();
            (await cancel.CmScheduleBaselines.SingleAsync()).Status = "Cancelled";
            (await cancel.ProjectScheduleEmails.SingleAsync()).Status = ProjectScheduleEmail.CancelledStatus;
            await cancel.SaveChangesAsync();
        });
        await using var db = fixture.Context(intercept);
        var sink = new CountingEmail();
        var translations = new TranslationService(db, new MemoryCache(new MemoryCacheOptions()));
        var service = new ProjectScheduleEmailService(db, sink, new NotificationService(db, sink, translations, NullLogger<NotificationService>.Instance), NullLogger<ProjectScheduleEmailService>.Instance);
        (await service.SendPendingAsync(default)).Should().Be(0);
        intercept.Triggered.Should().BeTrue("the test must cancel after selecting the due email, before loading it");
        sink.Count.Should().Be(0);
        await using var verify = fixture.Context();
        (await verify.ProjectScheduleEmails.SingleAsync()).Status.Should().Be(ProjectScheduleEmail.CancelledStatus);
    }
    private sealed class BeforeEmailReload(Func<Task> cancel) : DbCommandInterceptor
    {
        public bool Triggered { get; private set; }
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (!Triggered && command.CommandText.Contains("FROM [project_schedule_emails]", StringComparison.Ordinal) && command.CommandText.Contains("WHERE [p].[Id] =", StringComparison.Ordinal))
            { Triggered = true; await cancel(); }
            return result;
        }
    }
    private sealed class CountingEmail : IEmailService
    {
        public int Count { get; private set; }
        public Task SendEmailAsync(string toEmail, string subject, string htmlBody) { Count++; return Task.CompletedTask; }
        public Task SendEmailWithCcAsync(string toEmail, IReadOnlyCollection<string> ccEmails, string subject, string htmlBody, CancellationToken ct = default) => SendEmailAsync(toEmail, subject, htmlBody);
        public Task SendEmailWithAttachmentAsync(string toEmail, string subject, string htmlBody, EmailAttachment attachment, CancellationToken ct = default) => SendEmailAsync(toEmail, subject, htmlBody);
    }

    private sealed class BeforeSave : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource bothReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int arrivals;
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries().Any(x =>
                x.Entity is CmProgressSnapshot && x.State == EntityState.Added ||
                x.Entity is ConstructionTask && x.State == EntityState.Modified))
            {
                if (Interlocked.Increment(ref arrivals) >= 2) bothReady.TrySetResult();
                await bothReady.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }
    }

    private sealed class ScheduleDatabase(string connection) : IAsyncDisposable
    {
        private const string Prefix = "NiconCmConcurrency_";
        public int ProjectId { get; private set; }
        public int TaskId { get; private set; }
        public int UserId { get; private set; }
        public DateTime UpdatedAt { get; private set; }
        public AppDbContext Context(IInterceptor? interceptor = null)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection);
            if (interceptor is not null) options.AddInterceptors(interceptor);
            return new(options.Options);
        }
        public CmScheduleController Controller(AppDbContext db)
        {
            var service = new CmScheduleService(db, new ConstructionTaskService(db, NullLogger<ConstructionTaskService>.Instance),
                new PermissionService(db), new CmScheduleMailService(db, new EphemeralDataProtectionProvider()));
            return new CmScheduleController(service, new ProjectAccessService(db, new PermissionService(db)), new TranslationService(db, new MemoryCache(new MemoryCacheOptions())), NullLogger<CmScheduleController>.Instance)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, UserId.ToString())], "test")) } },
            };
        }
        public static async Task<ScheduleDatabase> CreateAsync()
        {
            var builder = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("NICON_SQL_TEST_CONNECTION")) { InitialCatalog = Prefix + Guid.NewGuid().ToString("N"), Pooling = false };
            var fixture = new ScheduleDatabase(builder.ConnectionString);
            try
            {
                await using var db = fixture.Context();
                await db.Database.EnsureCreatedAsync();
                var user = new ApplicationUser { PhoneNumber = "0911000019", FullName = "Chỉ huy trưởng nhà máy Long An", IsActive = true };
                var customer = new Customer { Name = "Chủ đầu tư nhà máy Long An" };
                db.AddRange(user, customer); await db.SaveChangesAsync();
                var project = new OperationalProject { Code = "PJ-CM-SQL", Name = "Thi công móng nhà máy Long An", CustomerId = customer.Id, ProjectManagerUserId = user.Id };
                var design = new DesignProject { ProjectCode = "DP-CM-SQL", Name = "Hồ sơ kết cấu Long An", CustomerId = customer.Id, OperationalProject = project };
                var node = new ProjectScheduleNode { Id = Guid.NewGuid(), OperationalProject = project };
                var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-10); var end = start.AddDays(8);
                var crm = new ProjectScheduleRevision
                {
                    OperationalProject = project,
                    Version = 1,
                    Status = ProjectScheduleRevisionStatus.Published,
                    CreatedByUserId = user.Id,
                    Items = [new ProjectScheduleItem { Node = node, WbsCode = "1.1", Title = "Thi công móng", PlannedStart = start, PlannedEnd = end }]
                };
                var task = new ConstructionTask { DesignProject = design, ProjectScheduleNode = node, TaskCode = "TC-MONG", Wbs = "1.1", Name = "Thi công móng", PlannedStart = start, PlannedEnd = end, ProgressReportedAt = DateTime.UtcNow, ProgressReportedByUserId = user.Id };
                db.AddRange(task, crm, new OperationalProjectMember { OperationalProject = project, UserId = user.Id, StartedAt = DateTime.UtcNow.AddDays(-1), Position = "Chỉ huy trưởng" }, new SiteSettings());
                await db.SaveChangesAsync();
                db.CmScheduleBaselines.Add(new CmScheduleBaseline
                {
                    OperationalProjectId = project.Id,
                    Version = 1,
                    CrmRevisionId = crm.Id,
                    Status = "Published",
                    SiteManagerUserId = user.Id,
                    ApproverUserId = user.Id,
                    CreatedByUserId = user.Id,
                    WorkingDaysOfWeekJson = "[0,1,2,3,4,5,6]",
                    ItemsJson = JsonSerializer.Serialize(new[] { new CmScheduleLine(task.Id, design.Id, node.Id, "1.1", task.Name, start, end, start, end, 100m, true, []) })
                });
                await db.SaveChangesAsync();
                fixture.ProjectId = project.Id; fixture.TaskId = task.Id; fixture.UserId = user.Id; fixture.UpdatedAt = task.UpdatedAt;
                return fixture;
            }
            catch { await fixture.DisposeAsync(); throw; }
        }
        public async ValueTask DisposeAsync()
        {
            var catalog = new SqlConnectionStringBuilder(connection).InitialCatalog;
            if (!catalog.StartsWith(Prefix, StringComparison.Ordinal) || !Guid.TryParseExact(catalog[Prefix.Length..], "N", out _)) throw new InvalidOperationException("Refusing cleanup outside the generated CM test database.");
            await using var db = Context(); await db.Database.EnsureDeletedAsync();
        }
    }
}
