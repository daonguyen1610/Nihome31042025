using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// End-to-end coverage for <c>ConceptOptionsController</c> (NIH-114):
/// RBAC gating, CRUD round-trip, state transitions + finalize workflow.
/// </summary>
public class ConceptOptionsControllerTests : IntegrationTestBase
{
    public ConceptOptionsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task List_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/concept-options")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_AsSale_IsForbidden()
    {
        // SALE has no design.concepts.* bundle.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        (await Client.GetAsync("/api/concept-options")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_AsDesign_ReturnsOk()
    {
        // DESIGN has design.concepts.view.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "DESIGN"));
        var res = await Client.GetAsync("/api/concept-options");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("items").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
    }

    [Fact]
    public async Task Create_HappyPath_ReturnsDraftingRow()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateDesignProjectAsync();

        var res = await Client.PostAsJsonAsync("/api/concept-options", new
        {
            designProjectId = projectId,
            name = $"Option {Guid.NewGuid():N}",
            description = "Test description.",
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync(res);
        body.GetProperty("status").GetString().Should().Be("Drafting");
    }

    [Fact]
    public async Task Create_UnknownProject_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var res = await Client.PostAsJsonAsync("/api/concept-options", new
        {
            designProjectId = 9999999,
            name = "orphan",
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Sale_CannotCreate()
    {
        // SALE has no design.concepts.* bundle at all.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateDesignProjectAsync();

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        var res = await Client.PostAsJsonAsync("/api/concept-options", new
        {
            designProjectId = projectId,
            name = "blocked",
        });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Transition_Finalize_UnlocksProjectStage()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateDesignProjectAsync();
        var id = await CreateOptionAsync(projectId);
        await Transition(id, "PendingInternalReview");
        await Transition(id, "PresentedToClient");
        var final = await Client.PostAsJsonAsync($"/api/concept-options/{id}/status", new { status = "Finalized" });
        final.StatusCode.Should().Be(HttpStatusCode.OK);

        // Design project should now sit at BasicDesign stage.
        var proj = await Client.GetAsync($"/api/design-projects/{projectId}");
        proj.EnsureSuccessStatusCode();
        (await ReadJsonAsync(proj)).GetProperty("currentStage").GetString().Should().Be("BasicDesign");
    }

    [Fact]
    public async Task Transition_UnknownStatus_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateDesignProjectAsync();
        var id = await CreateOptionAsync(projectId);
        var res = await Client.PostAsJsonAsync($"/api/concept-options/{id}/status", new { status = "Bogus" });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_PresentedRow_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateDesignProjectAsync();
        var id = await CreateOptionAsync(projectId);
        await Transition(id, "PendingInternalReview");
        await Transition(id, "PresentedToClient");
        (await Client.DeleteAsync($"/api/concept-options/{id}"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // -------- helpers --------

    private async Task<int> CreateDesignProjectAsync()
    {
        var customerId = await FirstCustomerIdAsync();
        var res = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name = $"Concept fixture {Guid.NewGuid():N}",
            customerId,
        });
        res.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(res)).GetProperty("id").GetInt32();
    }

    private async Task<int> CreateOptionAsync(int projectId)
    {
        var res = await Client.PostAsJsonAsync("/api/concept-options", new
        {
            designProjectId = projectId,
            name = $"Option {Guid.NewGuid():N}",
        });
        res.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(res)).GetProperty("id").GetInt32();
    }

    private async Task Transition(int id, string status)
    {
        var res = await Client.PostAsJsonAsync($"/api/concept-options/{id}/status", new { status });
        res.EnsureSuccessStatusCode();
    }

    private async Task<int> FirstCustomerIdAsync()
    {
        return await WithDbAsync<int>(async db =>
        {
            var existing = await db.Customers.OrderBy(c => c.Id).FirstOrDefaultAsync();
            if (existing != null) return existing.Id;

            var customer = new Customer
            {
                Name = "Concept Test Customer " + Guid.NewGuid().ToString("N")[..6],
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
