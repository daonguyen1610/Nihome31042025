using System.Net;
using System.Net.Http.Json;
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
        (await Client.GetAsync("/api/basic-design-docs")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_AsSale_IsForbidden()
    {
        // SALE has no design.basic.* bundle.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        (await Client.GetAsync("/api/basic-design-docs")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_AsDesign_ReturnsOkWithReadiness()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "DESIGN"));
        var res = await Client.GetAsync("/api/basic-design-docs");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("items").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
        body.GetProperty("readiness").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
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
        var proj = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name = $"Concept-stage {Guid.NewGuid():N}",
            customerId,
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

    // -------- helpers --------

    /// <summary>
    /// Create a fresh DesignProject, finalize a Concept option on it so
    /// the parent stage flips to BasicDesign, and return the project id.
    /// </summary>
    private async Task<int> CreateBasicStageProjectAsync()
    {
        var customerId = await FirstCustomerIdAsync();
        var proj = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name = $"Basic-stage {Guid.NewGuid():N}",
            customerId,
        });
        proj.EnsureSuccessStatusCode();
        var projectId = (await ReadJsonAsync(proj)).GetProperty("id").GetInt32();

        // Push a concept option through the state machine to unlock BasicDesign.
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
        return projectId;
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
