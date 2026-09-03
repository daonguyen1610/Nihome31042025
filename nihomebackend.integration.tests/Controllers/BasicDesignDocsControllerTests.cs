using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// End-to-end coverage for <c>BasicDesignDocsController</c> (NIH-115):
/// RBAC gating, CRUD round-trip, state transitions with the stricter
/// approve permission, and the Shop-Drawing unlock endpoint.
/// </summary>
public class BasicDesignDocsControllerTests : IntegrationTestBase
{
    public BasicDesignDocsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task List_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/basic-design-docs?designProjectId=1")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_AsSale_IsForbidden()
    {
        // SALE has no design.basic.* bundle.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        (await Client.GetAsync("/api/basic-design-docs?designProjectId=1")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_AsDesign_ReturnsOkWithReadiness()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateBasicStageProjectAsync();
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "DESIGN"));
        var res = await Client.GetAsync($"/api/basic-design-docs?designProjectId={projectId}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("items").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
        body.GetProperty("readiness").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
    }

    [Fact]
    public async Task DisciplineScopedMember_ListAndDetail_HideOtherDisciplines()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateBasicStageProjectAsync();
        var architectureId = await CreateDocAsync(projectId, "architecture");
        var structureId = await CreateDocAsync(projectId, "structure");
        using (var file = CreateFileForm("restricted structure", "structure.pdf", "application/pdf"))
            (await Client.PostAsync($"/api/basic-design-docs/{structureId}/upload", file))
                .EnsureSuccessStatusCode();
        var designUserId = await WithDbAsync(db => db.Users
            .Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["ARCHITECT"])
            .Select(user => user.Id)
            .SingleAsync());
        await WithDbAsync(async db =>
        {
            var operationalProjectId = await db.DesignProjects
                .Where(project => project.Id == projectId)
                .Select(project => project.OperationalProjectId!.Value)
                .SingleAsync();
            var member = new OperationalProjectMember
            {
                OperationalProjectId = operationalProjectId,
                UserId = designUserId,
                Position = "Architect",
                StartedAt = DateTime.UtcNow.AddDays(-1),
                CreatedByUserId = designUserId,
                UpdatedByUserId = designUserId,
                Roles =
                [
                    new OperationalProjectMemberRole
                    {
                        RoleCode = ProjectTeamRoleCode.Architect,
                        Scope = ProjectRoleScope.Discipline,
                        ScopeValue = "architecture",
                        StartedAt = DateTime.UtcNow.AddDays(-1),
                    },
                ],
            };
            db.OperationalProjectMembers.Add(member);
            var architecture = await db.BasicDesignDocs.FindAsync(architectureId);
            architecture!.Status = BasicDesignDocStatus.InternallyApproved;
            await db.SaveChangesAsync();
        });

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "ARCHITECT"));
        var list = await Client.GetAsync($"/api/basic-design-docs?designProjectId={projectId}");
        var body = await ReadJsonAsync(list);

        list.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("total").GetInt32().Should().Be(1);
        body.GetProperty("items")[0].GetProperty("id").GetInt32().Should().Be(architectureId);
        body.GetProperty("readiness").GetProperty("requiredDisciplineCodes")
            .EnumerateArray().Select(item => item.GetString()).Should().Equal("architecture");
        body.GetProperty("readiness").GetProperty("internallyApprovedDisciplineCodes")
            .EnumerateArray().Select(item => item.GetString()).Should().Equal("architecture");
        body.GetProperty("readiness").GetProperty("readyForShopDrawing").GetBoolean().Should().BeTrue();
        (await Client.GetAsync($"/api/basic-design-docs/{architectureId}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await Client.GetAsync($"/api/basic-design-docs/{structureId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.GetAsync($"/api/basic-design-docs/{structureId}/content"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Upload_UsesAuthenticatedContentRouteAndRejectsUnsupportedFiles()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateBasicStageProjectAsync();
        var documentId = await CreateDocAsync(projectId, "architecture");

        using var invalidForm = CreateFileForm("invalid", "malware.exe", "application/octet-stream");
        (await Client.PostAsync($"/api/basic-design-docs/{documentId}/upload", invalidForm))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var validForm = CreateFileForm("basic design content", "basic-design.pdf", "application/pdf");
        var upload = await Client.PostAsync($"/api/basic-design-docs/{documentId}/upload", validForm);
        upload.StatusCode.Should().Be(HttpStatusCode.OK);
        var filePath = (await ReadJsonAsync(upload)).GetProperty("filePath").GetString();

        (await Client.GetAsync(filePath)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var content = await Client.GetAsync($"/api/basic-design-docs/{documentId}/content");
        content.StatusCode.Should().Be(HttpStatusCode.OK);
        (await content.Content.ReadAsStringAsync()).Should().Be("basic design content");

        using var anonymousClient = Factory.CreateClient();
        (await anonymousClient.GetAsync($"/api/basic-design-docs/{documentId}/content"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_HappyPath_AllocatesCode()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateBasicStageProjectAsync();

        var res = await Client.PostAsJsonAsync("/api/basic-design-docs", new
        {
            designProjectId = projectId,
            disciplineCode = "architecture",
            title = $"Test drawing {Guid.NewGuid():N}",
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync(res);
        body.GetProperty("status").GetString().Should().Be("InProgress");
        body.GetProperty("documentCode").GetString().Should().StartWith("KT-BD-");
    }

    [Fact]
    public async Task Create_ProjectNotInBasicStage_IsBadRequest()
    {
        // The auto-created project after DesignProject POST is at Concept stage.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var customerId = await FirstCustomerIdAsync();
        var operationalProjectId = await CreateOperationalProjectAsync(customerId);
        var proj = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name = $"Concept-stage {Guid.NewGuid():N}",
            customerId,
            operationalProjectId,
        });
        var projectId = (await ReadJsonAsync(proj)).GetProperty("id").GetInt32();

        var res = await Client.PostAsJsonAsync("/api/basic-design-docs", new
        {
            designProjectId = projectId,
            disciplineCode = "architecture",
            title = "should fail",
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Transition_HappyPath_ToInternallyApproved()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateBasicStageProjectAsync();
        var id = await CreateDocAsync(projectId, "architecture");
        (await Client.PostAsJsonAsync($"/api/basic-design-docs/{id}/status", new { status = "SubmittedForReview" }))
            .EnsureSuccessStatusCode();
        var res = await Client.PostAsJsonAsync($"/api/basic-design-docs/{id}/status", new { status = "InternallyApproved" });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(res)).GetProperty("status").GetString().Should().Be("InternallyApproved");
    }

    [Fact]
    public async Task Transition_InvalidStatus_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateBasicStageProjectAsync();
        var id = await CreateDocAsync(projectId, "architecture");
        var res = await Client.PostAsJsonAsync($"/api/basic-design-docs/{id}/status", new { status = "Bogus" });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UnlockShopDrawing_HappyPath_AdvancesStage()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateBasicStageProjectAsync();
        // Approve 1 doc per required discipline.
        foreach (var d in new[] { "architecture", "structure", "mep" })
        {
            var id = await CreateDocAsync(projectId, d);
            (await Client.PostAsJsonAsync($"/api/basic-design-docs/{id}/status", new { status = "SubmittedForReview" }))
                .EnsureSuccessStatusCode();
            (await Client.PostAsJsonAsync($"/api/basic-design-docs/{id}/status", new { status = "InternallyApproved" }))
                .EnsureSuccessStatusCode();
        }

        var res = await Client.PostAsync($"/api/basic-design-docs/design-project/{projectId}/unlock-shop-drawing", null);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(res)).GetProperty("currentStage").GetString().Should().Be("ShopDrawing");

        var proj = await Client.GetAsync($"/api/design-projects/{projectId}");
        proj.EnsureSuccessStatusCode();
        (await ReadJsonAsync(proj)).GetProperty("currentStage").GetString().Should().Be("ShopDrawing");
    }

    [Fact]
    public async Task UnlockShopDrawing_NotReady_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateBasicStageProjectAsync();
        // Approve only 2 of 3.
        foreach (var d in new[] { "architecture", "structure" })
        {
            var id = await CreateDocAsync(projectId, d);
            (await Client.PostAsJsonAsync($"/api/basic-design-docs/{id}/status", new { status = "SubmittedForReview" }))
                .EnsureSuccessStatusCode();
            (await Client.PostAsJsonAsync($"/api/basic-design-docs/{id}/status", new { status = "InternallyApproved" }))
                .EnsureSuccessStatusCode();
        }

        var res = await Client.PostAsync($"/api/basic-design-docs/design-project/{projectId}/unlock-shop-drawing", null);
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Sale_CannotCreate()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateBasicStageProjectAsync();

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        var res = await Client.PostAsJsonAsync("/api/basic-design-docs", new
        {
            designProjectId = projectId,
            disciplineCode = "architecture",
            title = "blocked",
        });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_AfterReview_RemovesRevision()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateBasicStageProjectAsync();
        var id = await CreateDocAsync(projectId, "architecture");
        (await Client.PostAsJsonAsync($"/api/basic-design-docs/{id}/status", new { status = "SubmittedForReview" }))
            .EnsureSuccessStatusCode();
        await WithDbAsync(async db =>
        {
            var userId = await db.Users.Select(user => user.Id).FirstAsync();
            db.DrawingRevisions.Add(new DrawingRevision
            {
                TargetType = DrawingRevisionTargetType.BasicDesignDoc,
                TargetId = id,
                RevisionNumber = 1,
                ReasonCode = "client-change",
                Note = "Delete cleanup",
                IsCurrent = true,
                CreatedByUserId = userId,
            });
            await db.SaveChangesAsync();
        });

        (await Client.DeleteAsync($"/api/basic-design-docs/{id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        await WithDbAsync(async db =>
        {
            (await db.BasicDesignDocs.AnyAsync(doc => doc.Id == id)).Should().BeFalse();
            (await db.DrawingRevisions.AnyAsync(revision => revision.TargetId == id
                && revision.TargetType == DrawingRevisionTargetType.BasicDesignDoc)).Should().BeFalse();
        });
    }

    // -------- helpers --------

    /// <summary>
    /// Create a fresh DesignProject and seed the required setup stage.
    /// </summary>
    private async Task<int> CreateBasicStageProjectAsync()
    {
        var customerId = await FirstCustomerIdAsync();
        var operationalProjectId = await CreateOperationalProjectAsync(customerId);
        var proj = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name = $"Basic-stage {Guid.NewGuid():N}",
            customerId,
            operationalProjectId,
        });
        proj.EnsureSuccessStatusCode();
        var projectId = (await ReadJsonAsync(proj)).GetProperty("id").GetInt32();

        await WithDbAsync(async db =>
        {
            var project = await db.DesignProjects.FindAsync(projectId);
            project!.CurrentStage = DesignProjectStage.BasicDesign;
            await db.SaveChangesAsync();
        });
        return projectId;
    }

    private async Task<int> CreateOperationalProjectAsync(int customerId)
    {
        return await WithDbAsync<int>(async db =>
        {
            var designUserId = await db.Users
                .Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["DESIGN"])
                .Select(user => user.Id)
                .SingleAsync();
            var project = new OperationalProject
            {
                Code = $"PJ-TEST-{Guid.NewGuid():N}",
                Name = "Basic design integration fixture",
                CustomerId = customerId,
                ProjectManagerUserId = designUserId,
            };
            db.OperationalProjects.Add(project);
            await db.SaveChangesAsync();
            return project.Id;
        });
    }

    private async Task<int> CreateDocAsync(int projectId, string discipline)
    {
        var res = await Client.PostAsJsonAsync("/api/basic-design-docs", new
        {
            designProjectId = projectId,
            disciplineCode = discipline,
            title = $"{discipline} doc {Guid.NewGuid():N}",
        });
        res.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(res)).GetProperty("id").GetInt32();
    }

    private static MultipartFormDataContent CreateFileForm(string content, string fileName, string contentType)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        return form;
    }

    private async Task<int> FirstCustomerIdAsync()
    {
        return await WithDbAsync<int>(async db =>
        {
            var existing = await db.Customers.OrderBy(c => c.Id).FirstOrDefaultAsync();
            if (existing != null) return existing.Id;
            var customer = new Customer
            {
                Name = "Basic Test Customer " + Guid.NewGuid().ToString("N")[..6],
                SourceCode = "referral",
                RelationshipStatus = CustomerRelationshipStatus.InProgress,
                Type = CustomerType.Company,
            };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            return customer.Id;
        });
    }
}
