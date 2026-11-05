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

        using var submittedResponse = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{projectId}/master-schedule/revisions/{revisionId}/submit",
            new { approverUserId = approverId, rowVersion = revision.GetProperty("rowVersion").GetString() },
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
        queuedApproval.Status.Should().BeOneOf(
            ProjectScheduleEmail.PendingStatus, ProjectScheduleEmail.SentStatus);

        await AuthenticateAsync("SALES_MANAGER");
        using var approvedResponse = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{projectId}/master-schedule/revisions/{revisionId}/review",
            new { decision = "Approve", comment = "Đồng ý phát hành", rowVersion = submittedRevision.GetProperty("rowVersion").GetString() },
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

        using var secondDraft = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{projectId}/master-schedule/revisions",
            RevisionPayload(), $"schedule-second-{Guid.NewGuid():N}");
        secondDraft.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var ineligibleApprover = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{projectId}/master-schedule/revisions/{revisionId}/submit",
            new { approverUserId = await UserIdAsync("PM"), rowVersion },
            $"schedule-ineligible-{Guid.NewGuid():N}");
        ineligibleApprover.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ineligibleApprover.Content.ReadAsStringAsync())
            .Should().Contain("Người duyệt");

        using var staleSubmit = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{projectId}/master-schedule/revisions/{revisionId}/submit",
            new { approverUserId = approverId, rowVersion = "AAAAAAAAAAA=" },
            $"schedule-stale-{Guid.NewGuid():N}");
        staleSubmit.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var submit = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{projectId}/master-schedule/revisions/{revisionId}/submit",
            new { approverUserId = approverId, rowVersion },
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
