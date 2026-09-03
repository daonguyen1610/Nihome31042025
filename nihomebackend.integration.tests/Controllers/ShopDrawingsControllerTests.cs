using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// End-to-end coverage for <c>ShopDrawingsController</c> (NIH-116):
/// RBAC gating, CRUD round-trip, state transitions with the stricter
/// approve permission, and the bulk delete endpoint with partial success.
/// </summary>
public class ShopDrawingsControllerTests : IntegrationTestBase
{
    public ShopDrawingsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task List_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/shop-drawings?designProjectId=1")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_AsSale_IsForbidden()
    {
        // SALE has no design.shop.* bundle.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        (await Client.GetAsync("/api/shop-drawings?designProjectId=1")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_AsDesign_ReturnsOkWithStatusCounts()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateShopStageProjectAsync();
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "DESIGN"));
        var res = await Client.GetAsync($"/api/shop-drawings?designProjectId={projectId}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("items").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
        body.GetProperty("statusCounts").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
    }

    [Fact]
    public async Task DisciplineScopedMember_ListDetailAndContent_HideOtherDisciplines()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateShopStageProjectAsync();
        var architectureId = await CreateDrawingAsync(projectId, "architecture");
        var structureId = await CreateDrawingAsync(projectId, "structure");
        using (var file = CreateFileForm("restricted structure", "structure.pdf", "application/pdf"))
            (await Client.PostAsync($"/api/shop-drawings/{structureId}/upload", file)).EnsureSuccessStatusCode();
        await SeedDisciplineMemberAsync(projectId, "architecture");

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "ARCHITECT"));
        var list = await Client.GetAsync($"/api/shop-drawings?designProjectId={projectId}");
        var body = await ReadJsonAsync(list);

        list.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("total").GetInt32().Should().Be(1);
        body.GetProperty("items")[0].GetProperty("id").GetInt32().Should().Be(architectureId);
        body.GetProperty("statusCounts").GetProperty("Drafting").GetInt32().Should().Be(1);
        (await Client.GetAsync($"/api/shop-drawings/{architectureId}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await Client.GetAsync($"/api/shop-drawings/{structureId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.GetAsync($"/api/shop-drawings/{structureId}/content"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Upload_UsesAuthenticatedContentRouteAndRejectsUnsupportedFiles()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateShopStageProjectAsync();
        var drawingId = await CreateDrawingAsync(projectId, "architecture");

        using var invalidForm = CreateFileForm("invalid", "malware.exe", "application/octet-stream");
        (await Client.PostAsync($"/api/shop-drawings/{drawingId}/upload", invalidForm))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var validForm = CreateFileForm("shop drawing content", "shop-drawing.pdf", "application/pdf");
        var upload = await Client.PostAsync($"/api/shop-drawings/{drawingId}/upload", validForm);
        upload.StatusCode.Should().Be(HttpStatusCode.OK);
        var filePath = (await ReadJsonAsync(upload)).GetProperty("filePath").GetString();

        (await Client.GetAsync(filePath)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var content = await Client.GetAsync($"/api/shop-drawings/{drawingId}/content");
        content.StatusCode.Should().Be(HttpStatusCode.OK);
        (await content.Content.ReadAsStringAsync()).Should().Be("shop drawing content");

        using var anonymousClient = Factory.CreateClient();
        (await anonymousClient.GetAsync($"/api/shop-drawings/{drawingId}/content"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_HappyPath_AllocatesCode()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateShopStageProjectAsync();

        var res = await Client.PostAsJsonAsync("/api/shop-drawings", new
        {
            designProjectId = projectId,
            disciplineCode = "architecture",
            constructionItem = "Móng cọc",
            title = $"Test drawing {Guid.NewGuid():N}",
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync(res);
        body.GetProperty("status").GetString().Should().Be("Drafting");
        body.GetProperty("drawingCode").GetString().Should().StartWith("KT-SD-");
        body.GetProperty("constructionItem").GetString().Should().Be("Móng cọc");
    }

    [Fact]
    public async Task Create_ProjectNotInShopStage_IsBadRequest()
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

        var res = await Client.PostAsJsonAsync("/api/shop-drawings", new
        {
            designProjectId = projectId,
            disciplineCode = "architecture",
            constructionItem = "Móng cọc",
            title = "should fail",
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Transition_HappyPath_ToApproved()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateShopStageProjectAsync();
        var id = await CreateDrawingAsync(projectId, "architecture");
        (await Client.PostAsJsonAsync($"/api/shop-drawings/{id}/status", new { status = "InReview" }))
            .EnsureSuccessStatusCode();
        var res = await Client.PostAsJsonAsync($"/api/shop-drawings/{id}/status", new { status = "Approved" });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(res)).GetProperty("status").GetString().Should().Be("Approved");
    }

    [Fact]
    public async Task Transition_InvalidStatus_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateShopStageProjectAsync();
        var id = await CreateDrawingAsync(projectId, "architecture");
        var res = await Client.PostAsJsonAsync($"/api/shop-drawings/{id}/status", new { status = "Bogus" });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Transition_ReleasedNotReachable_IsBadRequest()
    {
        // Released is the exclusive output of the (future) IFC release flow.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateShopStageProjectAsync();
        var id = await CreateDrawingAsync(projectId, "architecture");
        foreach (var s in new[] { "InReview", "Approved", "PendingIfc" })
        {
            (await Client.PostAsJsonAsync($"/api/shop-drawings/{id}/status", new { status = s }))
                .EnsureSuccessStatusCode();
        }
        var res = await Client.PostAsJsonAsync($"/api/shop-drawings/{id}/status", new { status = "Released" });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task BulkDelete_MixedStatuses_DeletesEveryRequestedRow()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateShopStageProjectAsync();
        var a = await CreateDrawingAsync(projectId, "architecture");
        var b = await CreateDrawingAsync(projectId, "architecture");
        var c = await CreateDrawingAsync(projectId, "structure");
        (await Client.PostAsJsonAsync($"/api/shop-drawings/{c}/status", new { status = "InReview" }))
            .EnsureSuccessStatusCode();

        var res = await Client.PostAsJsonAsync("/api/shop-drawings/bulk-delete", new
        {
            ids = new[] { a, b, c },
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("requested").GetInt32().Should().Be(3);
        body.GetProperty("deleted").GetInt32().Should().Be(3);
        body.GetProperty("failures").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task BulkDelete_AsSale_IsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        var res = await Client.PostAsJsonAsync("/api/shop-drawings/bulk-delete", new
        {
            ids = new[] { 1 },
        });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Sale_CannotCreate()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateShopStageProjectAsync();

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        var res = await Client.PostAsJsonAsync("/api/shop-drawings", new
        {
            designProjectId = projectId,
            disciplineCode = "architecture",
            constructionItem = "blocked",
            title = "blocked",
        });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_Approved_RemovesRevisionAndIfcItemButPreservesRelease()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateShopStageProjectAsync();
        var drawingId = await CreateDrawingAsync(projectId, "architecture");
        (await Client.PostAsJsonAsync($"/api/shop-drawings/{drawingId}/status", new { status = "InReview" }))
            .EnsureSuccessStatusCode();
        (await Client.PostAsJsonAsync($"/api/shop-drawings/{drawingId}/status", new { status = "Approved" }))
            .EnsureSuccessStatusCode();
        var releaseId = await WithDbAsync<int>(async db =>
        {
            var userId = await db.Users.Select(user => user.Id).FirstAsync();
            var release = new IfcRelease
            {
                DesignProjectId = projectId,
                ReleaseNumber = $"IFC-DELETE-{Guid.NewGuid():N}",
                Title = "Preserved release",
            };
            db.IfcReleases.Add(release);
            await db.SaveChangesAsync();
            db.IfcReleaseItems.Add(new IfcReleaseItem
            {
                IfcReleaseId = release.Id,
                ShopDrawingId = drawingId,
            });
            db.DrawingRevisions.Add(new DrawingRevision
            {
                TargetType = DrawingRevisionTargetType.ShopDrawing,
                TargetId = drawingId,
                RevisionNumber = 1,
                ReasonCode = "client-change",
                Note = "Delete cleanup",
                IsCurrent = true,
                CreatedByUserId = userId,
            });
            await db.SaveChangesAsync();
            return release.Id;
        });

        (await Client.DeleteAsync($"/api/shop-drawings/{drawingId}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        await WithDbAsync(async db =>
        {
            (await db.ShopDrawings.AnyAsync(drawing => drawing.Id == drawingId)).Should().BeFalse();
            (await db.IfcReleaseItems.AnyAsync(item => item.ShopDrawingId == drawingId)).Should().BeFalse();
            (await db.DrawingRevisions.AnyAsync(revision => revision.TargetId == drawingId
                && revision.TargetType == DrawingRevisionTargetType.ShopDrawing)).Should().BeFalse();
            (await db.IfcReleases.AnyAsync(release => release.Id == releaseId)).Should().BeTrue();
        });
    }

    // -------- helpers --------

    /// <summary>
    /// Create a fresh DesignProject and seed the required setup stage.
    /// </summary>
    private async Task<int> CreateShopStageProjectAsync()
    {
        var customerId = await FirstCustomerIdAsync();
        var operationalProjectId = await CreateOperationalProjectAsync(customerId);
        var proj = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name = $"Shop-stage {Guid.NewGuid():N}",
            customerId,
            operationalProjectId,
        });
        proj.EnsureSuccessStatusCode();
        var projectId = (await ReadJsonAsync(proj)).GetProperty("id").GetInt32();

        await WithDbAsync(async db =>
        {
            var project = await db.DesignProjects.FindAsync(projectId);
            project!.CurrentStage = DesignProjectStage.ShopDrawing;
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
                Name = "Shop drawing integration fixture",
                CustomerId = customerId,
                ProjectManagerUserId = designUserId,
            };
            db.OperationalProjects.Add(project);
            await db.SaveChangesAsync();
            return project.Id;
        });
    }

    private async Task<int> CreateDrawingAsync(int projectId, string discipline, string constructionItem = "Móng cọc")
    {
        var res = await Client.PostAsJsonAsync("/api/shop-drawings", new
        {
            designProjectId = projectId,
            disciplineCode = discipline,
            constructionItem,
            title = $"{discipline} SD {Guid.NewGuid():N}",
        });
        res.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(res)).GetProperty("id").GetInt32();
    }

    private async Task SeedDisciplineMemberAsync(int designProjectId, string discipline)
    {
        await WithDbAsync(async db =>
        {
            var userId = await db.Users
                .Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["ARCHITECT"])
                .Select(user => user.Id)
                .SingleAsync();
            var operationalProjectId = await db.DesignProjects
                .Where(project => project.Id == designProjectId)
                .Select(project => project.OperationalProjectId!.Value)
                .SingleAsync();
            db.OperationalProjectMembers.Add(new OperationalProjectMember
            {
                OperationalProjectId = operationalProjectId,
                UserId = userId,
                Position = "Architect",
                StartedAt = DateTime.UtcNow.AddDays(-1),
                CreatedByUserId = userId,
                UpdatedByUserId = userId,
                Roles =
                [
                    new OperationalProjectMemberRole
                    {
                        RoleCode = ProjectTeamRoleCode.Architect,
                        Scope = ProjectRoleScope.Discipline,
                        ScopeValue = discipline,
                        StartedAt = DateTime.UtcNow.AddDays(-1),
                    },
                ],
            });
            await db.SaveChangesAsync();
        });
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
                Name = "Shop Test Customer " + Guid.NewGuid().ToString("N")[..6],
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
