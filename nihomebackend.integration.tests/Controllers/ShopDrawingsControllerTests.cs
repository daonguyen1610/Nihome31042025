using System.Net;
using System.Net.Http.Json;
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
        (await Client.GetAsync("/api/shop-drawings")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_AsSale_IsForbidden()
    {
        // SALE has no design.shop.* bundle.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        (await Client.GetAsync("/api/shop-drawings")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_AsDesign_ReturnsOkWithStatusCounts()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "DESIGN"));
        var res = await Client.GetAsync("/api/shop-drawings");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("items").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
        body.GetProperty("statusCounts").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
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
        var proj = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name = $"Concept-stage {Guid.NewGuid():N}",
            customerId,
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
    public async Task BulkDelete_MixedRows_ReportsPartialSuccess()
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
        body.GetProperty("deleted").GetInt32().Should().Be(2);
        body.GetProperty("failures").GetArrayLength().Should().Be(1);
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

    // -------- helpers --------

    /// <summary>
    /// Create a fresh DesignProject, finalize a Concept option to unlock
    /// BasicDesign, then approve 1 basic-design doc per required discipline
    /// and unlock Shop Drawing. Returns the project id.
    /// </summary>
    private async Task<int> CreateShopStageProjectAsync()
    {
        var customerId = await FirstCustomerIdAsync();
        var proj = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name = $"Shop-stage {Guid.NewGuid():N}",
            customerId,
        });
        proj.EnsureSuccessStatusCode();
        var projectId = (await ReadJsonAsync(proj)).GetProperty("id").GetInt32();

        // Concept → BasicDesign
        var opt = await Client.PostAsJsonAsync("/api/concept-options", new
        {
            designProjectId = projectId,
            name = "AutoFinalized",
        });
        opt.EnsureSuccessStatusCode();
        var optId = (await ReadJsonAsync(opt)).GetProperty("id").GetInt32();
        foreach (var s in new[] { "PendingInternalReview", "PresentedToClient", "Finalized" })
        {
            (await Client.PostAsJsonAsync($"/api/concept-options/{optId}/status", new { status = s }))
                .EnsureSuccessStatusCode();
        }

        // BasicDesign → ShopDrawing (approve 1 doc per required discipline)
        foreach (var d in new[] { "architecture", "structure", "mep" })
        {
            var docRes = await Client.PostAsJsonAsync("/api/basic-design-docs", new
            {
                designProjectId = projectId,
                disciplineCode = d,
                title = $"BD {d} {Guid.NewGuid():N}",
            });
            docRes.EnsureSuccessStatusCode();
            var docId = (await ReadJsonAsync(docRes)).GetProperty("id").GetInt32();
            (await Client.PostAsJsonAsync($"/api/basic-design-docs/{docId}/status", new { status = "SubmittedForReview" }))
                .EnsureSuccessStatusCode();
            (await Client.PostAsJsonAsync($"/api/basic-design-docs/{docId}/status", new { status = "InternallyApproved" }))
                .EnsureSuccessStatusCode();
        }
        (await Client.PostAsync($"/api/basic-design-docs/design-project/{projectId}/unlock-shop-drawing", null))
            .EnsureSuccessStatusCode();
        return projectId;
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
