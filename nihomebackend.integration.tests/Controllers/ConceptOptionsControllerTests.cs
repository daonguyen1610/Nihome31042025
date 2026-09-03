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
        (await Client.GetAsync("/api/concept-options?designProjectId=1")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_AsSale_IsForbidden()
    {
        // SALE has no design.concepts.* bundle.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        (await Client.GetAsync("/api/concept-options?designProjectId=1")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_AsDesign_ReturnsOk()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateDesignProjectAsync();
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "DESIGN"));
        var res = await Client.GetAsync($"/api/concept-options?designProjectId={projectId}");
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
    public async Task Create_UnknownProject_IsNotFoundAfterAccessCheck()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var res = await Client.PostAsJsonAsync("/api/concept-options", new
        {
            designProjectId = 9999999,
            name = "orphan",
        });
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
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
    public async Task Transition_Design_CanMoveButNotFinalize()
    {
        // DESIGN has design.concepts.{view|manage} but NOT
        // design.concepts.finalize (per rbac-defaults). Non-Finalize
        // transitions must pass; approval access is project-masked with 404.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateDesignProjectAsync();
        var id = await CreateOptionAsync(projectId);

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "DESIGN"));
        var toReview = await Client.PostAsJsonAsync($"/api/concept-options/{id}/status", new { status = "PendingInternalReview" });
        toReview.StatusCode.Should().Be(HttpStatusCode.OK);
        var toPresented = await Client.PostAsJsonAsync($"/api/concept-options/{id}/status", new { status = "PresentedToClient" });
        toPresented.StatusCode.Should().Be(HttpStatusCode.OK);
        var toFinal = await Client.PostAsJsonAsync($"/api/concept-options/{id}/status", new { status = "Finalized" });
        toFinal.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_PresentedRow_Succeeds()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateDesignProjectAsync();
        var id = await CreateOptionAsync(projectId);
        await Transition(id, "PendingInternalReview");
        await Transition(id, "PresentedToClient");
        (await Client.DeleteAsync($"/api/concept-options/{id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Client.GetAsync($"/api/concept-options/{id}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // -------- helpers --------

    private async Task<int> CreateDesignProjectAsync()
    {
        var customerId = await FirstCustomerIdAsync();
        var operationalProjectId = await CreateOperationalProjectAsync(customerId);
        var res = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name = $"Concept fixture {Guid.NewGuid():N}",
            customerId,
            operationalProjectId,
        });
        res.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(res)).GetProperty("id").GetInt32();
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
                Name = "Concept integration fixture",
                CustomerId = customerId,
                ProjectManagerUserId = designUserId,
            };
            db.OperationalProjects.Add(project);
            await db.SaveChangesAsync();
            var member = new OperationalProjectMember
            {
                OperationalProjectId = project.Id,
                UserId = designUserId,
                Position = "Designer",
                StartedAt = DateTime.UtcNow.AddDays(-1),
                CreatedByUserId = designUserId,
                UpdatedByUserId = designUserId,
            };
            member.Roles.Add(new OperationalProjectMemberRole
            {
                RoleCode = ProjectTeamRoleCode.Architect,
                Scope = ProjectRoleScope.Project,
                StartedAt = member.StartedAt,
            });
            db.OperationalProjectMembers.Add(member);
            await db.SaveChangesAsync();
            return project.Id;
        });
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
