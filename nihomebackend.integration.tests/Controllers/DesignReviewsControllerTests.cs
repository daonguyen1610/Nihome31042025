using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

public sealed class DesignReviewsControllerTests : IntegrationTestBase
{
    public DesignReviewsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Lifecycle_EnforcesApprovedTaskAndRecipientScopedCommenting()
    {
        var fixture = await CreateFixtureAsync();
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "DESIGN"));

        using var create = await SendWithKeyAsync(HttpMethod.Post,
            $"/api/design-projects/{fixture.DesignProjectId}/reviews", new
            {
                stage = "Concept",
                title = "Phương án Concept nhà máy NICON R01",
                recipientUserIds = new[] { fixture.RecipientUserId },
                projectDocumentIds = new[] { fixture.DocumentId },
            });
        create.StatusCode.Should().Be(HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        var created = await ReadJsonAsync(create);
        var reviewId = created.GetProperty("id").GetInt32();

        using var submit = await SendWithKeyAsync(HttpMethod.Post,
            $"/api/design-projects/{fixture.DesignProjectId}/reviews/{reviewId}/submit-internal", new
            {
                rowVersion = created.GetProperty("rowVersion").GetString(),
            });
        submit.StatusCode.Should().Be(HttpStatusCode.OK, await submit.Content.ReadAsStringAsync());

        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "SALE"));
        using var list = await Client.GetAsync($"/api/design-projects/{fixture.DesignProjectId}/reviews");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var recipientReview = (await ReadJsonAsync(list)).EnumerateArray().Single();
        recipientReview.GetProperty("canManage").GetBoolean().Should().BeFalse();
        recipientReview.GetProperty("canComment").GetBoolean().Should().BeTrue();

        using var comment = await SendWithKeyAsync(HttpMethod.Post,
            $"/api/design-projects/{fixture.DesignProjectId}/reviews/{reviewId}/comments", new
            {
                body = "Kinh doanh đề nghị xác nhận vật liệu hoàn thiện theo brief khách hàng.",
                projectDocumentId = fixture.DocumentId,
            });
        comment.StatusCode.Should().Be(HttpStatusCode.OK, await comment.Content.ReadAsStringAsync());
        var commented = await ReadJsonAsync(comment);
        commented.GetProperty("comments")[0].GetProperty("projectDocumentId").GetInt64()
            .Should().Be(fixture.DocumentId);

        using var invalidDocument = await SendWithKeyAsync(HttpMethod.Post,
            $"/api/design-projects/{fixture.DesignProjectId}/reviews/{reviewId}/comments", new
            {
                body = "Không được gắn sang tài liệu ngoài đợt trình.",
                projectDocumentId = fixture.DocumentId + 1,
            });
        invalidDocument.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.DesignReviewComments.CountAsync(item =>
            item.DesignReviewId == reviewId))).Should().Be(1);

        using var generalComment = await SendWithKeyAsync(HttpMethod.Post,
            $"/api/design-projects/{fixture.DesignProjectId}/reviews/{reviewId}/comments", new
            {
                body = "Lưu ý chung cho bộ hồ sơ.",
            });
        generalComment.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(generalComment)).GetProperty("comments")[1]
            .GetProperty("projectDocumentId").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);

        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "ACCOUNTANT"));
        using var unrelated = await Client.GetAsync($"/api/design-projects/{fixture.DesignProjectId}/reviews");
        unrelated.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(unrelated)).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task SubmitInternal_WhenEvidenceHashChanged_ReturnsBadRequestAndKeepsDraft()
    {
        var fixture = await CreateFixtureAsync();
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "DESIGN"));
        using var create = await SendWithKeyAsync(HttpMethod.Post,
            $"/api/design-projects/{fixture.DesignProjectId}/reviews", new
            {
                stage = "Concept",
                title = "Concept R01 kiểm tra tính bất biến",
                recipientUserIds = new[] { fixture.RecipientUserId },
                projectDocumentIds = new[] { fixture.DocumentId },
            });
        create.EnsureSuccessStatusCode();
        var created = await ReadJsonAsync(create);
        var reviewId = created.GetProperty("id").GetInt32();
        await WithDbAsync(async db =>
        {
            var document = await db.ProjectDocuments.SingleAsync(item => item.Id == fixture.DocumentId);
            document.Sha256 = new string('f', 64);
            await db.SaveChangesAsync();
        });

        using var submit = await SendWithKeyAsync(HttpMethod.Post,
            $"/api/design-projects/{fixture.DesignProjectId}/reviews/{reviewId}/submit-internal", new
            {
                rowVersion = created.GetProperty("rowVersion").GetString(),
            });

        submit.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.DesignReviews.Where(item => item.Id == reviewId)
            .Select(item => item.Status).SingleAsync())).Should().Be(DesignReviewStatus.Draft);
    }

    private async Task<Fixture> CreateFixtureAsync() => await WithDbAsync(async db =>
    {
        var manager = await UserAsync(db, "DESIGN");
        var lead = await UserAsync(db, "DESIGN_LEAD");
        var recipient = await UserAsync(db, "SALE");
        var customer = new Customer
        {
            Name = $"Khách hàng hồ sơ thiết kế {Guid.NewGuid():N}",
            Type = CustomerType.Company,
            SourceCode = "referral",
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var operational = new OperationalProject
        {
            Code = UniqueSlug("OP-DR").ToUpperInvariant(),
            Name = "Nhà máy NICON - trình duyệt Concept",
            CustomerId = customer.Id,
            ProjectManagerUserId = manager.Id,
        };
        db.OperationalProjects.Add(operational);
        await db.SaveChangesAsync();
        var designProject = new DesignProject
        {
            OperationalProjectId = operational.Id,
            ProjectCode = UniqueSlug("DP-DR").ToUpperInvariant(),
            Name = "Thiết kế nhà máy NICON",
            CustomerId = customer.Id,
            ProjectManagerUserId = manager.Id,
            DesignLeadUserId = lead.Id,
            CurrentStage = DesignProjectStage.Concept,
        };
        db.DesignProjects.Add(designProject);
        var assignee = Member(operational.Id, manager.Id, manager.Id, "Kiến trúc sư");
        var approver = Member(operational.Id, lead.Id, manager.Id, "Design Lead");
        db.OperationalProjectMembers.AddRange(assignee, approver);
        await db.SaveChangesAsync();
        var phase = new DesignSchedulePhase
        {
            OperationalProjectId = operational.Id,
            DesignProjectId = designProject.Id,
            Code = DesignSchedulePhaseCode.Concept,
            PlannedStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)),
            PlannedEnd = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = DesignScheduleStatus.Completed,
            ProgressPercent = 100,
            Weight = 100,
            CreatedByUserId = manager.Id,
            UpdatedByUserId = manager.Id,
        };
        var concept = new ConceptOption
        {
            DesignProjectId = designProject.Id,
            Name = "Phương án Concept R01",
            Status = ConceptOptionStatus.PresentedToClient,
            CreatedByUserId = manager.Id,
            UpdatedByUserId = manager.Id,
        };
        db.AddRange(phase, concept);
        await db.SaveChangesAsync();
        db.DesignScheduleTasks.Add(new DesignScheduleTask
        {
            OperationalProjectId = operational.Id,
            DesignProjectId = designProject.Id,
            PhaseId = phase.Id,
            Code = "CON-ARC-001",
            Name = "Hoàn thiện Concept kiến trúc",
            DepartmentCode = "DESIGN",
            AssigneeMemberId = assignee.Id,
            ApproverMemberId = approver.Id,
            PlannedStart = phase.PlannedStart,
            PlannedEnd = phase.PlannedEnd,
            ActualEnd = phase.PlannedEnd,
            Status = DesignScheduleStatus.Completed,
            ApprovalStatus = DesignTaskApprovalStatus.Approved,
            ProgressPercent = 100,
            Weight = 100,
            CreatedByUserId = manager.Id,
            UpdatedByUserId = manager.Id,
        });
        var document = new ProjectDocument
        {
            OperationalProjectId = operational.Id,
            Category = ProjectDocumentCategory.DesignConcept,
            SourceModule = ProjectDocumentSourceModule.Design,
            SourceEntityType = nameof(ConceptOption),
            SourceRecordId = concept.Id,
            LocalPath = $"/files/project-documents/concept-{Guid.NewGuid():N}.pdf",
            OriginalFileName = "NICON-Concept-R01.pdf",
            ContentType = "application/pdf",
            Sha256 = new string('a', 64),
            SyncStatus = ProjectDocumentSyncStatus.Synced,
            CreatedByUserId = manager.Id,
            UpdatedByUserId = manager.Id,
        };
        db.ProjectDocuments.Add(document);
        await db.SaveChangesAsync();
        return new Fixture(designProject.Id, recipient.Id, document.Id);
    });

    private static OperationalProjectMember Member(int projectId, int userId, int actorId, string position) => new()
    {
        OperationalProjectId = projectId,
        UserId = userId,
        Position = position,
        StartedAt = DateTime.UtcNow.AddDays(-1),
        CreatedByUserId = actorId,
        UpdatedByUserId = actorId,
    };

    private static Task<ApplicationUser> UserAsync(NihomeBackend.Data.AppDbContext db, string roleCode) =>
        db.Users.SingleAsync(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode[roleCode]);

    private async Task<HttpResponseMessage> SendWithKeyAsync(HttpMethod method, string uri, object body)
    {
        using var request = new HttpRequestMessage(method, uri) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        return await Client.SendAsync(request);
    }

    private sealed record Fixture(int DesignProjectId, int RecipientUserId, long DocumentId);
}
