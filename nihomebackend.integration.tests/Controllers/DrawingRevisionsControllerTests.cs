using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// End-to-end coverage for <c>DrawingRevisionsController</c> (NIH-117):
/// RBAC gating, append-only create semantics (auto-number + previous
/// flip to superseded), diff endpoint, and cross-family target
/// resolution.
/// </summary>
public class DrawingRevisionsControllerTests : IntegrationTestBase
{
    public DrawingRevisionsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task List_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/drawing-revisions?designProjectId=1")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_AsSale_IsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        (await Client.GetAsync("/api/drawing-revisions?designProjectId=1")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_AsDesign_ReturnsOk()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var (projectId, _) = await CreateShopStageProjectWithFirstDrawingAsync();
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "DESIGN"));
        var res = await Client.GetAsync($"/api/drawing-revisions?designProjectId={projectId}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task List_ByDesignProject_ReturnsOnlyThatProjectsRevisions()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var (projectId, shopId) = await CreateShopStageProjectWithFirstDrawingAsync();
        var created = await Client.PostAsJsonAsync("/api/drawing-revisions", new
        {
            targetType = "ShopDrawing",
            targetId = shopId,
            reasonCode = "client-request",
            note = "Project-filtered revision",
        });
        created.EnsureSuccessStatusCode();

        var matching = await Client.GetAsync($"/api/drawing-revisions?designProjectId={projectId}");
        var missing = await Client.GetAsync("/api/drawing-revisions?designProjectId=2147483647");

        matching.EnsureSuccessStatusCode();
        (await ReadJsonAsync(matching)).GetProperty("items").GetArrayLength().Should().Be(1);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DisciplineScopedMember_ListDetailAndDiff_HideOtherDisciplines()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var (projectId, architectureDrawingId) = await CreateShopStageProjectWithFirstDrawingAsync();
        var structureDrawing = await Client.PostAsJsonAsync("/api/shop-drawings", new
        {
            designProjectId = projectId,
            disciplineCode = "structure",
            constructionItem = "Structure",
            title = "Structure SD",
        });
        structureDrawing.EnsureSuccessStatusCode();
        var structureDrawingId = (await ReadJsonAsync(structureDrawing)).GetProperty("id").GetInt32();
        var architectureRevision = await CreateRevisionAsync(architectureDrawingId, "Architecture revision");
        var structureRevision = await CreateRevisionAsync(structureDrawingId, "Structure revision");
        await SeedDisciplineMemberAsync(projectId, "architecture");

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "ARCHITECT"));
        var list = await Client.GetAsync($"/api/drawing-revisions?designProjectId={projectId}");
        var body = await ReadJsonAsync(list);

        list.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("total").GetInt32().Should().Be(1);
        body.GetProperty("items")[0].GetProperty("id").GetInt32().Should().Be(architectureRevision);
        (await Client.GetAsync($"/api/drawing-revisions/{architectureRevision}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await Client.GetAsync($"/api/drawing-revisions/{structureRevision}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.GetAsync(
            $"/api/drawing-revisions/diff?fromId={architectureRevision}&toId={structureRevision}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_ForShopDrawing_HappyPath_StartsAtR1()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var (_, shopId) = await CreateShopStageProjectWithFirstDrawingAsync();

        var res = await Client.PostAsJsonAsync("/api/drawing-revisions", new
        {
            targetType = "ShopDrawing",
            targetId = shopId,
            reasonCode = "client-request",
            note = "First revision from integration test",
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync(res);
        body.GetProperty("revisionNumber").GetInt32().Should().Be(1);
        body.GetProperty("isCurrent").GetBoolean().Should().BeTrue();
        body.GetProperty("targetType").GetString().Should().Be("ShopDrawing");
        body.GetProperty("targetCode").GetString().Should().StartWith("KT-SD-");
    }

    [Fact]
    public async Task Create_SecondRevision_FlipsPreviousToSuperseded()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var (_, shopId) = await CreateShopStageProjectWithFirstDrawingAsync();

        var first = await Client.PostAsJsonAsync("/api/drawing-revisions", new
        {
            targetType = "ShopDrawing",
            targetId = shopId,
            reasonCode = "client-request",
            note = "R1",
        });
        first.EnsureSuccessStatusCode();
        var firstId = (await ReadJsonAsync(first)).GetProperty("id").GetInt32();

        var second = await Client.PostAsJsonAsync("/api/drawing-revisions", new
        {
            targetType = "ShopDrawing",
            targetId = shopId,
            reasonCode = "mep-sync",
            note = "R2",
        });
        second.EnsureSuccessStatusCode();
        (await ReadJsonAsync(second)).GetProperty("revisionNumber").GetInt32().Should().Be(2);

        var reloaded = await Client.GetAsync($"/api/drawing-revisions/{firstId}");
        reloaded.EnsureSuccessStatusCode();
        (await ReadJsonAsync(reloaded)).GetProperty("isCurrent").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Create_UnknownTarget_IsNotFoundAfterAccessCheck()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var res = await Client.PostAsJsonAsync("/api/drawing-revisions", new
        {
            targetType = "ShopDrawing",
            targetId = 9999999,
            reasonCode = "client-request",
            note = "should fail",
        });
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_UnknownReason_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var (_, shopId) = await CreateShopStageProjectWithFirstDrawingAsync();
        var res = await Client.PostAsJsonAsync("/api/drawing-revisions", new
        {
            targetType = "ShopDrawing",
            targetId = shopId,
            reasonCode = "not-a-real-reason",
            note = "note",
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_MissingNote_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var (_, shopId) = await CreateShopStageProjectWithFirstDrawingAsync();
        var res = await Client.PostAsJsonAsync("/api/drawing-revisions", new
        {
            targetType = "ShopDrawing",
            targetId = shopId,
            reasonCode = "client-request",
            note = "",
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Sale_CannotCreate()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var (_, shopId) = await CreateShopStageProjectWithFirstDrawingAsync();

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        var res = await Client.PostAsJsonAsync("/api/drawing-revisions", new
        {
            targetType = "ShopDrawing",
            targetId = shopId,
            reasonCode = "client-request",
            note = "blocked",
        });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Diff_ReturnsMetadataChanges()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var (_, shopId) = await CreateShopStageProjectWithFirstDrawingAsync();

        var a = await Client.PostAsJsonAsync("/api/drawing-revisions", new
        {
            targetType = "ShopDrawing",
            targetId = shopId,
            reasonCode = "client-request",
            note = "first note",
        });
        var b = await Client.PostAsJsonAsync("/api/drawing-revisions", new
        {
            targetType = "ShopDrawing",
            targetId = shopId,
            reasonCode = "mep-sync",
            note = "different note",
        });
        var aId = (await ReadJsonAsync(a)).GetProperty("id").GetInt32();
        var bId = (await ReadJsonAsync(b)).GetProperty("id").GetInt32();

        var diff = await Client.GetAsync($"/api/drawing-revisions/diff?fromId={aId}&toId={bId}");
        diff.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(diff);
        body.GetProperty("changes").GetArrayLength().Should().BeGreaterOrEqualTo(1);
        body.GetProperty("from").GetProperty("id").GetInt32().Should().Be(aId);
        body.GetProperty("to").GetProperty("id").GetInt32().Should().Be(bId);
    }

    [Fact]
    public async Task Diff_AcrossDifferentTargets_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var (projectId, shopId) = await CreateShopStageProjectWithFirstDrawingAsync();

        // Create a second shop drawing on the same project.
        var second = await Client.PostAsJsonAsync("/api/shop-drawings", new
        {
            designProjectId = projectId,
            disciplineCode = "structure",
            constructionItem = "Cột kết cấu",
            title = "Second SD",
        });
        second.EnsureSuccessStatusCode();
        var shopId2 = (await ReadJsonAsync(second)).GetProperty("id").GetInt32();

        var a = await Client.PostAsJsonAsync("/api/drawing-revisions", new
        {
            targetType = "ShopDrawing",
            targetId = shopId,
            reasonCode = "client-request",
            note = "a",
        });
        var b = await Client.PostAsJsonAsync("/api/drawing-revisions", new
        {
            targetType = "ShopDrawing",
            targetId = shopId2,
            reasonCode = "client-request",
            note = "b",
        });
        var aId = (await ReadJsonAsync(a)).GetProperty("id").GetInt32();
        var bId = (await ReadJsonAsync(b)).GetProperty("id").GetInt32();

        var diff = await Client.GetAsync($"/api/drawing-revisions/diff?fromId={aId}&toId={bId}");
        diff.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // -------- helpers --------

    private async Task<(int ProjectId, int ShopDrawingId)> CreateShopStageProjectWithFirstDrawingAsync()
    {
        var customerId = await FirstCustomerIdAsync();
        var operationalProjectId = await CreateOperationalProjectAsync(customerId);
        var proj = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name = $"Rev fixture {Guid.NewGuid():N}",
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

        // First shop drawing.
        var shop = await Client.PostAsJsonAsync("/api/shop-drawings", new
        {
            designProjectId = projectId,
            disciplineCode = "architecture",
            constructionItem = "Móng cọc",
            title = $"SD arch {Guid.NewGuid():N}",
        });
        shop.EnsureSuccessStatusCode();
        var shopId = (await ReadJsonAsync(shop)).GetProperty("id").GetInt32();

        return (projectId, shopId);
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
                Name = "Drawing revision integration fixture",
                CustomerId = customerId,
                ProjectManagerUserId = designUserId,
            };
            db.OperationalProjects.Add(project);
            await db.SaveChangesAsync();
            return project.Id;
        });
    }

    private async Task<int> CreateRevisionAsync(int targetId, string note)
    {
        var response = await Client.PostAsJsonAsync("/api/drawing-revisions", new
        {
            targetType = "ShopDrawing",
            targetId,
            reasonCode = "client-request",
            note,
        });
        response.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(response)).GetProperty("id").GetInt32();
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

    private async Task<int> FirstCustomerIdAsync()
    {
        return await WithDbAsync<int>(async db =>
        {
            var existing = await db.Customers.OrderBy(c => c.Id).FirstOrDefaultAsync();
            if (existing != null) return existing.Id;
            var customer = new Customer
            {
                Name = "Rev Test Customer " + Guid.NewGuid().ToString("N")[..6],
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
