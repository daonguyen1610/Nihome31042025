using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NihomeBackend.Models;
using NihomeBackend.Services;

namespace NihomeBackend.IntegrationTests.Controllers;

public sealed class ProjectScheduleControllerTests : IntegrationTestBase
{
    public ProjectScheduleControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Submit_RequiresTheEmailPreviewBeforeQueueing()
    {
        var projectId = await CreateProjectAsync(await UserIdAsync("PM"));
        var approverId = await UserIdAsync("SALES_MANAGER");
        await AuthenticateAsync("SUPER_ADMIN");
        using var createdResponse = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{projectId}/master-schedule/revisions",
            RevisionPayload(), $"schedule-create-{Guid.NewGuid():N}");
        var revision = (await ReadJsonAsync(createdResponse)).GetProperty("revisions")[0];
        var revisionId = revision.GetProperty("id").GetInt32();

        using var submittedResponse = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{projectId}/master-schedule/revisions/{revisionId}/submit",
            new { approverUserId = approverId, rowVersion = revision.GetProperty("rowVersion").GetString() },
            $"schedule-submit-without-preview-{Guid.NewGuid():N}");

        submittedResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await submittedResponse.Content.ReadAsStringAsync()).Should().Contain("Hãy xem trước email");
        (await WithDbAsync(db => db.ProjectScheduleEmails.AsNoTracking()
            .CountAsync(item => item.RevisionId == revisionId))).Should().Be(0);
    }

    [Fact]
    public async Task Submit_RejectsPreviewAfterRecipientChangesAndQueuesFreshPreview()
    {
        var projectId = await CreateProjectAsync(await UserIdAsync("PM"));
        var approverId = await UserIdAsync("SALES_MANAGER");
        await AuthenticateAsync("SUPER_ADMIN");
        using var createdResponse = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{projectId}/master-schedule/revisions",
            RevisionPayload(), $"schedule-create-{Guid.NewGuid():N}");
        var revision = (await ReadJsonAsync(createdResponse)).GetProperty("revisions")[0];
        var revisionId = revision.GetProperty("id").GetInt32();
        var rowVersion = revision.GetProperty("rowVersion").GetString();
        var previewPath = $"/api/operational-projects/{projectId}/master-schedule/revisions/{revisionId}/email-preview?action=Submit&recipientUserId={approverId}";
        using var originalPreviewResponse = await Client.GetAsync(previewPath);
        var originalToken = (await ReadJsonAsync(originalPreviewResponse)).GetProperty("previewToken").GetString();
        var originalEmail = await WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(item => item.Id == approverId);
            var email = user.Email;
            user.Email = $"schedule-{Guid.NewGuid():N}@example.com";
            await db.SaveChangesAsync();
            return email;
        });
        try
        {
            using var staleResponse = await SendJsonWithKeyAsync(HttpMethod.Post,
                $"/api/operational-projects/{projectId}/master-schedule/revisions/{revisionId}/submit",
                new { approverUserId = approverId, rowVersion, previewToken = originalToken },
                $"schedule-submit-stale-preview-{Guid.NewGuid():N}");
            staleResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await WithDbAsync(db => db.ProjectScheduleEmails.AsNoTracking()
                .CountAsync(item => item.RevisionId == revisionId))).Should().Be(0);
            (await WithDbAsync(db => db.ProjectScheduleRevisions.AsNoTracking()
                .SingleAsync(item => item.Id == revisionId))).Status.Should().Be(ProjectScheduleRevisionStatus.Draft);

            using var freshPreviewResponse = await Client.GetAsync(previewPath);
            var freshPreview = await ReadJsonAsync(freshPreviewResponse);
            var freshToken = freshPreview.GetProperty("previewToken").GetString();
            using var submitted = await SendJsonWithKeyAsync(HttpMethod.Post,
                $"/api/operational-projects/{projectId}/master-schedule/revisions/{revisionId}/submit",
                new { approverUserId = approverId, rowVersion, previewToken = freshToken },
                $"schedule-submit-fresh-preview-{Guid.NewGuid():N}");
            submitted.StatusCode.Should().Be(HttpStatusCode.OK, await submitted.Content.ReadAsStringAsync());
            var queued = await WithDbAsync(db => db.ProjectScheduleEmails.AsNoTracking()
                .SingleAsync(item => item.RevisionId == revisionId));
            queued.ToEmail.Should().Be(freshPreview.GetProperty("emails")[0].GetProperty("toEmail").GetString());
            queued.Body.Should().Be(freshPreview.GetProperty("emails")[0].GetProperty("body").GetString());
        }
        finally
        {
            await WithDbAsync(async db =>
            {
                var user = await db.Users.SingleAsync(item => item.Id == approverId);
                user.Email = originalEmail;
                await db.SaveChangesAsync();
            });
        }
    }

    [Fact]
    public async Task SubmitAndApprove_PublishesRevisionAndQueuesDurableApprovalEmail()
    {
        var projectId = await CreateProjectAsync(await UserIdAsync("PM"));
        var approverId = await UserIdAsync("SALES_MANAGER");
        var approver = await WithDbAsync(db => db.Users.AsNoTracking()
            .Where(user => user.Id == approverId).Select(user => new { user.Email, user.FullName }).SingleAsync());
        await AuthenticateAsync("SUPER_ADMIN");

        using var createdResponse = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{projectId}/master-schedule/revisions",
            RevisionPayload(), $"schedule-create-{Guid.NewGuid():N}");
        createdResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            await createdResponse.Content.ReadAsStringAsync());
        var created = await ReadJsonAsync(createdResponse);
        created.GetProperty("hasSignedUpstreamContract").GetBoolean().Should().BeFalse();
        var revision = created.GetProperty("revisions")[0];
        revision.GetProperty("status").GetString().Should().Be("Draft");
        revision.GetProperty("version").GetInt32().Should().Be(1);
        revision.GetProperty("items").GetArrayLength().Should().Be(2);
        var revisionId = revision.GetProperty("id").GetInt32();
        var approverOptions = created.GetProperty("approverOptions").EnumerateArray()
            .Select(option => option.GetProperty("userId").GetInt32()).ToList();
        approverOptions.Should().Contain(approverId);

        using var previewResponse = await Client.GetAsync(
            $"/api/operational-projects/{projectId}/master-schedule/revisions/{revisionId}/email-preview?action=Submit&recipientUserId={approverId}");
        previewResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var previewResult = await ReadJsonAsync(previewResponse);
        var preview = previewResult.GetProperty("emails")[0];
        var previewToken = previewResult.GetProperty("previewToken").GetString();
        preview.GetProperty("toEmail").GetString().Should().Be(approver.Email);
        preview.GetProperty("body").GetString().Should().Contain("Công trình tiến độ tổng thể");

        using var wrongPreviewResponse = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{projectId}/master-schedule/revisions/{revisionId}/submit",
            new { approverUserId = approverId, rowVersion = revision.GetProperty("rowVersion").GetString(), previewToken = "invalid" },
            $"schedule-submit-invalid-preview-{Guid.NewGuid():N}");
        wrongPreviewResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await WithDbAsync(db => db.ProjectScheduleEmails.AsNoTracking().CountAsync(item => item.RevisionId == revisionId)))
            .Should().Be(0);

        using var submittedResponse = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{projectId}/master-schedule/revisions/{revisionId}/submit",
            new { approverUserId = approverId, rowVersion = revision.GetProperty("rowVersion").GetString(), previewToken },
            $"schedule-submit-{Guid.NewGuid():N}");
        submittedResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            await submittedResponse.Content.ReadAsStringAsync());
        var submitted = await ReadJsonAsync(submittedResponse);
        var submittedRevision = submitted.GetProperty("revisions")[0];
        submittedRevision.GetProperty("status").GetString().Should().Be("PendingApproval");
        submittedRevision.GetProperty("approverUserId").GetInt32().Should().Be(approverId);

        var queued = await WithDbAsync(db => db.ProjectScheduleEmails.AsNoTracking()
            .Where(item => item.RevisionId == revisionId)
            .Select(item => new { item.ToEmail, item.Subject, item.Body, item.Status })
            .ToListAsync());
        var queuedApproval = queued.Should().ContainSingle().Subject;
        queuedApproval.ToEmail.Should().Be(approver.Email);
        queuedApproval.Subject.Should().Contain("Yêu cầu duyệt tiến độ tổng thể");
        queuedApproval.Body.Should().Contain("<table role=\"presentation\"");
        queuedApproval.Body.Should().Contain("PJ-");
        queuedApproval.Body.Should().Contain(WebUtility.HtmlEncode(approver.FullName));
        queuedApproval.Body.Should().NotContain("{{");
        queuedApproval.Body.Should().Be(preview.GetProperty("body").GetString());
        queuedApproval.Subject.Should().Be(preview.GetProperty("subject").GetString());
        queuedApproval.Status.Should().BeOneOf(
            ProjectScheduleEmail.PendingStatus, ProjectScheduleEmail.SentStatus);

        // The approval notice is only valid while the revision waits for its
        // approver, so approving cancels a still-pending notice. Deliver it
        // first, then verify a decision does not revoke a delivered message.
        using (var scope = Factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ProjectScheduleEmailService>()
                .SendPendingAsync(CancellationToken.None);
        }
        var deliveredApproval = await WithDbAsync(db => db.ProjectScheduleEmails.AsNoTracking()
            .SingleAsync(item => item.RevisionId == revisionId));
        deliveredApproval.Status.Should().Be(ProjectScheduleEmail.SentStatus);
        deliveredApproval.SentAt.Should().NotBeNull();

        await AuthenticateAsync("SALES_MANAGER");
        using var publicationPreviewResponse = await Client.GetAsync(
            $"/api/operational-projects/{projectId}/master-schedule/revisions/{revisionId}/email-preview?action=Publish");
        publicationPreviewResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var publicationToken = (await ReadJsonAsync(publicationPreviewResponse)).GetProperty("previewToken").GetString();
        using var missingPublicationPreview = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{projectId}/master-schedule/revisions/{revisionId}/review",
            new { decision = "Approve", rowVersion = submittedRevision.GetProperty("rowVersion").GetString() },
            $"schedule-approve-without-preview-{Guid.NewGuid():N}");
        missingPublicationPreview.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await missingPublicationPreview.Content.ReadAsStringAsync()).Should().Contain("Hãy xem trước email");
        (await WithDbAsync(db => db.ProjectScheduleRevisions.AsNoTracking()
            .SingleAsync(item => item.Id == revisionId))).Status.Should().Be(ProjectScheduleRevisionStatus.PendingApproval);
        using var approvedResponse = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{projectId}/master-schedule/revisions/{revisionId}/review",
            new { decision = "Approve", comment = "Đồng ý phát hành", rowVersion = submittedRevision.GetProperty("rowVersion").GetString(), previewToken = publicationToken },
            $"schedule-approve-{Guid.NewGuid():N}");
        approvedResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            await approvedResponse.Content.ReadAsStringAsync());
        var approved = await ReadJsonAsync(approvedResponse);
        approved.GetProperty("publishedRevisionId").GetInt32().Should().Be(revisionId);
        approved.GetProperty("revisions")[0].GetProperty("status").GetString().Should().Be("Published");

        using (var scope = Factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ProjectScheduleEmailService>()
                .SendPendingAsync(CancellationToken.None);
        }
        var delivered = await WithDbAsync(db => db.ProjectScheduleEmails.AsNoTracking()
            .SingleAsync(item => item.RevisionId == revisionId));
        delivered.Status.Should().Be(ProjectScheduleEmail.SentStatus);
        delivered.SentAt.Should().NotBeNull();
    }

    [Fact]
    public async Task EmailReview_OnlyProjectManagerMayConfirmOrRetryAnExhaustedEmail()
    {
        var managerId = await UserIdAsync("PM");
        var projectId = await CreateProjectAsync(managerId);
        var revisionId = await WithDbAsync(async db =>
        {
            var revision = new ProjectScheduleRevision
            {
                OperationalProjectId = projectId,
                Version = 1,
                Status = ProjectScheduleRevisionStatus.Published,
                CreatedByUserId = managerId,
            };
            db.ProjectScheduleRevisions.Add(revision);
            await db.SaveChangesAsync();
            return revision.Id;
        });
        var emailIds = await WithDbAsync(async db =>
        {
            var messages = new[] { 1, 2 }.Select(number => new ProjectScheduleEmail
            {
                OperationalProjectId = projectId,
                RevisionId = revisionId,
                RecipientUserId = managerId,
                ToEmail = $"recipient{number}@example.com",
                Subject = "Thông báo tiến độ",
                Body = "<p>Tiến độ dự án</p>",
                Status = ProjectScheduleEmail.NeedsReviewStatus,
                AttemptCount = ProjectScheduleEmailService.MaxAttempts,
            }).ToArray();
            db.ProjectScheduleEmails.AddRange(messages);
            await db.SaveChangesAsync();
            return messages.Select(item => item.Id).ToArray();
        });

        await AuthenticateAsync("SALES_MANAGER");
        (await Client.GetAsync($"/api/operational-projects/{projectId}/master-schedule/emails/needs-review"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var denied = await Client.PostAsJsonAsync(
            $"/api/operational-projects/{projectId}/master-schedule/emails/{emailIds[0]}/resolve",
            new { decision = "Retry" });
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await AuthenticateAsync("PM");
        using var listed = await Client.GetAsync(
            $"/api/operational-projects/{projectId}/master-schedule/emails/needs-review");
        listed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(listed)).GetArrayLength().Should().Be(2);
        using var received = await Client.PostAsJsonAsync(
            $"/api/operational-projects/{projectId}/master-schedule/emails/{emailIds[0]}/resolve",
            new { decision = "ConfirmReceived" });
        received.StatusCode.Should().Be(HttpStatusCode.NoContent,
            await received.Content.ReadAsStringAsync());
        using var retried = await Client.PostAsJsonAsync(
            $"/api/operational-projects/{projectId}/master-schedule/emails/{emailIds[1]}/resolve",
            new { decision = "Retry" });
        retried.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var repeated = await Client.PostAsJsonAsync(
            $"/api/operational-projects/{projectId}/master-schedule/emails/{emailIds[0]}/resolve",
            new { decision = "Retry" });
        repeated.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var rows = await WithDbAsync(db => db.ProjectScheduleEmails.AsNoTracking()
            .Where(item => emailIds.Contains(item.Id)).OrderBy(item => item.Id)
            .Select(item => new { item.Status, item.ReviewedByUserId, item.ReviewedAt, item.AttemptCount })
            .ToListAsync());
        rows[0].Status.Should().Be(ProjectScheduleEmail.ConfirmedReceivedStatus);
        rows[0].ReviewedByUserId.Should().Be(managerId);
        rows[0].ReviewedAt.Should().NotBeNull();
        rows[1].Status.Should().BeOneOf(ProjectScheduleEmail.PendingStatus, ProjectScheduleEmail.SentStatus);
        rows[1].AttemptCount.Should().BeLessThan(ProjectScheduleEmailService.MaxAttempts);
    }

    [Fact]
    public async Task MasterSchedule_EnforcesAuthorizationAndValidation()
    {
        var projectId = await CreateProjectAsync(await UserIdAsync("PM"));
        var approverId = await UserIdAsync("SALES_MANAGER");
        var otherApproverId = await UserIdAsync("BGD");

        Client.DefaultRequestHeaders.Authorization = null;
        (await Client.GetAsync($"/api/operational-projects/{projectId}/master-schedule"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var anonymousCreate = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{projectId}/master-schedule/revisions",
            RevisionPayload(), $"schedule-anon-{Guid.NewGuid():N}");
        anonymousCreate.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await AuthenticateAsync("USER");
        (await Client.GetAsync($"/api/operational-projects/{projectId}/master-schedule"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var customerCreate = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{projectId}/master-schedule/revisions",
            RevisionPayload(), $"schedule-customer-{Guid.NewGuid():N}");
        customerCreate.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await AuthenticateAsync("SUPER_ADMIN");
        // Each rejected create runs on its own project: the InMemory provider
        // used by these tests cannot roll back the draft row created before the
        // item validation fails.
        var duplicateProjectId = await CreateProjectAsync(await UserIdAsync("PM"));
        using var duplicateWbs = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{duplicateProjectId}/master-schedule/revisions",
            new
            {
                changeReason = "Trùng mã WBS",
                items = new object[]
                {
                    Item("1", "Móng", "2026-06-01", "2026-06-30", null, Guid.NewGuid()),
                    Item("1", "Thân", "2026-07-01", "2026-07-31", null, Guid.NewGuid()),
                },
            },
            $"schedule-duplicate-{Guid.NewGuid():N}");
        duplicateWbs.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var frameProjectId = await CreateProjectAsync(await UserIdAsync("PM"));
        var frameParent = Guid.NewGuid();
        using var outsideParentFrame = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{frameProjectId}/master-schedule/revisions",
            new
            {
                changeReason = "Con ngoài khung cha",
                items = new object[]
                {
                    Item("1", "Móng", "2026-06-01", "2026-06-30", null, frameParent),
                    Item("1.1", "Ép cọc", "2026-07-01", "2026-07-10", frameParent, Guid.NewGuid()),
                },
            },
            $"schedule-frame-{Guid.NewGuid():N}");
        outsideParentFrame.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var parentNode = Guid.NewGuid();
        var childNode = Guid.NewGuid();
        using var validCreate = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{projectId}/master-schedule/revisions",
            new
            {
                changeReason = "Thiết lập tiến độ tổng thể",
                items = new object[]
                {
                    Item("1", "Móng", "2026-06-01", "2026-06-30", null, parentNode),
                    Item("1.1", "Ép cọc", "2026-06-02", "2026-06-10", parentNode, childNode),
                },
            },
            $"schedule-valid-{Guid.NewGuid():N}");
        validCreate.StatusCode.Should().Be(HttpStatusCode.OK,
            await validCreate.Content.ReadAsStringAsync());
        var revision = (await ReadJsonAsync(validCreate)).GetProperty("revisions")[0];
        var revisionId = revision.GetProperty("id").GetInt32();
        var rowVersion = revision.GetProperty("rowVersion").GetString();
        using var validPreviewResponse = await Client.GetAsync(
            $"/api/operational-projects/{projectId}/master-schedule/revisions/{revisionId}/email-preview?action=Submit&recipientUserId={approverId}");
        var validPreviewToken = (await ReadJsonAsync(validPreviewResponse)).GetProperty("previewToken").GetString();

        using var secondDraft = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{projectId}/master-schedule/revisions",
            RevisionPayload(), $"schedule-second-{Guid.NewGuid():N}");
        secondDraft.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var ineligibleApprover = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{projectId}/master-schedule/revisions/{revisionId}/submit",
            new { approverUserId = await UserIdAsync("PM"), rowVersion, previewToken = validPreviewToken },
            $"schedule-ineligible-{Guid.NewGuid():N}");
        ineligibleApprover.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ineligibleApprover.Content.ReadAsStringAsync())
            .Should().Contain("Người duyệt");

        using var staleSubmit = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{projectId}/master-schedule/revisions/{revisionId}/submit",
            new { approverUserId = approverId, rowVersion = "AAAAAAAAAAA=", previewToken = validPreviewToken },
            $"schedule-stale-{Guid.NewGuid():N}");
        staleSubmit.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var submit = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{projectId}/master-schedule/revisions/{revisionId}/submit",
            new { approverUserId = approverId, rowVersion, previewToken = validPreviewToken },
            $"schedule-submit-{Guid.NewGuid():N}");
        submit.StatusCode.Should().Be(HttpStatusCode.OK,
            await submit.Content.ReadAsStringAsync());
        var pendingRevision = (await ReadJsonAsync(submit)).GetProperty("revisions")[0];
        var pendingVersion = pendingRevision.GetProperty("rowVersion").GetString();

        await AuthenticateAsync("BGD");
        using var wrongReviewer = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{projectId}/master-schedule/revisions/{revisionId}/review",
            new { decision = "Approve", rowVersion = pendingVersion },
            $"schedule-wrong-reviewer-{Guid.NewGuid():N}");
        wrongReviewer.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await WithDbAsync(db => db.ProjectScheduleRevisions.AsNoTracking()
            .SingleAsync(item => item.Id == revisionId))).Status
            .Should().Be(ProjectScheduleRevisionStatus.PendingApproval);

        await AuthenticateAsync("SALES_MANAGER");
        using var rejectWithoutReason = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{projectId}/master-schedule/revisions/{revisionId}/review",
            new { decision = "Reject", rowVersion = pendingVersion },
            $"schedule-reject-empty-{Guid.NewGuid():N}");
        rejectWithoutReason.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var reject = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{projectId}/master-schedule/revisions/{revisionId}/review",
            new { decision = "Reject", comment = "Cần bổ sung mốc nghiệm thu", rowVersion = pendingVersion },
            $"schedule-reject-{Guid.NewGuid():N}");
        reject.StatusCode.Should().Be(HttpStatusCode.OK, await reject.Content.ReadAsStringAsync());
        (await ReadJsonAsync(reject)).GetProperty("revisions")[0].GetProperty("status")
            .GetString().Should().Be("Rejected");
        (await WithDbAsync(db => db.ProjectScheduleRevisions.AsNoTracking()
            .CountAsync(item => item.OperationalProjectId == projectId)))
            .Should().Be(1);
        otherApproverId.Should().BePositive();
    }

    private Task AuthenticateAsync(string roleCode) => AuthTestHelper.AuthenticateAsync(
        Client, client => AuthTestHelper.LoginAsRoleAsync(client, roleCode));

    private async Task<int> UserIdAsync(string roleCode)
    {
        var phone = roleCode == "SUPER_ADMIN"
            ? TestDataSeeder.SuperAdminPhone
            : TestDataSeeder.BusinessRolePhonesByCode[roleCode];
        return await WithDbAsync(db => db.Users.Where(user => user.PhoneNumber == phone)
            .Select(user => user.Id).SingleAsync());
    }

    private Task<int> CreateProjectAsync(int managerUserId) => WithDbAsync(async db =>
    {
        var customer = new Customer
        {
            Name = $"Master schedule customer {Guid.NewGuid():N}",
            Type = CustomerType.Company,
            SourceCode = "referral",
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var project = new OperationalProject
        {
            Code = $"PJ-MS-{Guid.NewGuid():N}"[..30],
            Name = "Công trình tiến độ tổng thể",
            CustomerId = customer.Id,
            ProjectManagerUserId = managerUserId,
        };
        db.OperationalProjects.Add(project);
        await db.SaveChangesAsync();
        return project.Id;
    });

    private static object RevisionPayload()
    {
        var parent = Guid.NewGuid();
        return new
        {
            changeReason = "Thiết lập tiến độ tổng thể dự án",
            items = new object[]
            {
                Item("1", "Phần móng", "2026-06-01", "2026-06-30", null, parent),
                Item("1.1", "Ép cọc", "2026-06-02", "2026-06-10", parent, Guid.NewGuid()),
            },
        };
    }

    private static object Item(string wbsCode, string title, string start, string end,
        Guid? parentNodeId, Guid nodeId) => new
        {
            nodeId,
            parentNodeId,
            wbsCode,
            title,
            plannedStart = start,
            plannedEnd = end,
            sortOrder = 0,
        };

    private async Task<HttpResponseMessage> SendJsonWithKeyAsync(
        HttpMethod method, string uri, object payload, string key)
    {
        using var request = new HttpRequestMessage(method, uri) { Content = JsonContent.Create(payload) };
        request.Headers.Add("Idempotency-Key", key);
        return await Client.SendAsync(request);
    }
}
