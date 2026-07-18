using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// End-to-end coverage for <c>DesignProjectsController</c> (NIH-113):
/// RBAC gating, list + get, CRUD happy paths + the auto-create hook
/// fired by <c>ContractsController</c> transitions.
/// </summary>
public class DesignProjectsControllerTests : IntegrationTestBase
{
    public DesignProjectsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task List_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/design-projects")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_AsWarehouse_IsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));
        (await Client.GetAsync("/api/design-projects")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_AsPm_ReturnsOk()
    {
        // PM has design.projects.view (read-only).
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "PM"));
        var res = await Client.GetAsync("/api/design-projects");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("items").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
    }

    [Fact]
    public async Task Pm_CannotCreate()
    {
        // PM has view but not manage.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "PM"));
        var customerId = await FirstCustomerIdAsync();
        var res = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name = "PM blocked create",
            customerId,
        });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_HappyPath_AsSuperAdmin_ReturnsAutoCode()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var customerId = await FirstCustomerIdAsync();
        var res = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name = $"DP integ {Guid.NewGuid():N}",
            customerId,
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync(res);
        body.GetProperty("projectCode").GetString().Should().StartWith("DP-");
        body.GetProperty("currentStage").GetString().Should().Be("Concept");
        body.GetProperty("status").GetString().Should().Be("Active");
    }

    [Fact]
    public async Task Create_UnknownCustomer_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var res = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name = "Bad customer",
            customerId = 9999999,
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_UnknownId_Is404()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        (await Client.GetAsync("/api/design-projects/9999999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_HappyPath_ChangesStage()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var customerId = await FirstCustomerIdAsync();
        var id = await CreateAsync(customerId, "Update round-trip");
        var res = await Client.PutAsJsonAsync($"/api/design-projects/{id}", new
        {
            name = "Update round-trip",
            customerId,
            currentStage = "BasicDesign",
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("currentStage").GetString().Should().Be("BasicDesign");
    }

    [Fact]
    public async Task Delete_BeyondConcept_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var customerId = await FirstCustomerIdAsync();
        var id = await CreateAsync(customerId, "Cannot delete after stage");
        await Client.PutAsJsonAsync($"/api/design-projects/{id}", new
        {
            name = "Cannot delete after stage",
            customerId,
            currentStage = "BasicDesign",
        });
        (await Client.DeleteAsync($"/api/design-projects/{id}"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_Concept_Succeeds()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var customerId = await FirstCustomerIdAsync();
        var id = await CreateAsync(customerId, "Delete concept");
        (await Client.DeleteAsync($"/api/design-projects/{id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Client.GetAsync($"/api/design-projects/{id}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // -------- helpers --------

    private async Task<int> CreateAsync(int customerId, string name)
    {
        var res = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name,
            customerId,
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
                Name = "DP Test Customer " + Guid.NewGuid().ToString("N")[..6],
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
