using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// End-to-end coverage for <c>PunchItemsController</c> (NIH-146):
/// RBAC gates (view / manage / verify), CRUD lifecycle, status-machine
/// enforcement and bulk delete.
/// </summary>
public class PunchItemsControllerTests : IntegrationTestBase
{
    public PunchItemsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task List_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/punch-items")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_AsSale_IsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        (await Client.GetAsync("/api/punch-items")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_AsDesign_ReturnsOk()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "DESIGN"));
        (await Client.GetAsync("/api/punch-items")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_AsDesignViewOnly_IsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "DESIGN"));
        var res = await Client.PostAsJsonAsync("/api/punch-items", new
        {
            designProjectId = 1,
            title = "x",
            severity = "Low",
        });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_HappyPath_ReturnsOpenRow()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateProjectAsync();

        var res = await Client.PostAsJsonAsync("/api/punch-items", new
        {
            designProjectId = projectId,
            title = "Broken tile",
            severity = "High",
            location = "Floor 3",
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync(res);
        body.GetProperty("status").GetString().Should().Be("Open");
        body.GetProperty("severity").GetString().Should().Be("High");
        body.GetProperty("punchCode").GetString().Should().StartWith("P-");
    }

    [Fact]
    public async Task StatusEndpoint_RejectsVerified_MustGoThroughDedicatedEndpoint()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateProjectAsync();
        var id = await CreatePunchAsync(projectId);
        (await Client.PostAsJsonAsync($"/api/punch-items/{id}/status", new { status = "InProgress" })).EnsureSuccessStatusCode();
        (await Client.PostAsJsonAsync($"/api/punch-items/{id}/status", new { status = "Fixed" })).EnsureSuccessStatusCode();

        var wrong = await Client.PostAsJsonAsync($"/api/punch-items/{id}/status", new { status = "Verified" });
        wrong.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ReadJsonAsync(wrong);
        body.GetProperty("message").GetString().Should().Contain("/verify");
    }

    [Fact]
    public async Task VerifyEndpoint_AsSale_IsForbidden()
    {
        // First set the item to Fixed as super admin.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateProjectAsync();
        var id = await CreatePunchAsync(projectId);
        (await Client.PostAsJsonAsync($"/api/punch-items/{id}/status", new { status = "InProgress" })).EnsureSuccessStatusCode();
        (await Client.PostAsJsonAsync($"/api/punch-items/{id}/status", new { status = "Fixed" })).EnsureSuccessStatusCode();

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        var res = await Client.PostAsJsonAsync($"/api/punch-items/{id}/verify", new { });
        // SALE has neither view nor verify — 403 either way.
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task VerifyEndpoint_AsPm_FlipsToVerified()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateProjectAsync();
        var id = await CreatePunchAsync(projectId);
        (await Client.PostAsJsonAsync($"/api/punch-items/{id}/status", new { status = "InProgress" })).EnsureSuccessStatusCode();
        (await Client.PostAsJsonAsync($"/api/punch-items/{id}/status", new { status = "Fixed" })).EnsureSuccessStatusCode();

        // PM has construction.punch.** from wildcards.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "PM"));
        var res = await Client.PostAsJsonAsync($"/api/punch-items/{id}/verify", new { resolutionNote = "checked on site" });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("status").GetString().Should().Be("Verified");
    }

    [Fact]
    public async Task Reopen_FromVerified_IncrementsCounter()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateProjectAsync();
        var id = await CreatePunchAsync(projectId);
        (await Client.PostAsJsonAsync($"/api/punch-items/{id}/status", new { status = "InProgress" })).EnsureSuccessStatusCode();
        (await Client.PostAsJsonAsync($"/api/punch-items/{id}/status", new { status = "Fixed" })).EnsureSuccessStatusCode();
        (await Client.PostAsJsonAsync($"/api/punch-items/{id}/verify", new { })).EnsureSuccessStatusCode();

        var reopen = await Client.PostAsJsonAsync($"/api/punch-items/{id}/status", new { status = "Open" });
        reopen.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(reopen);
        body.GetProperty("status").GetString().Should().Be("Open");
        body.GetProperty("reopenCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Update_LockedAfterVerified()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateProjectAsync();
        var id = await CreatePunchAsync(projectId);
        (await Client.PostAsJsonAsync($"/api/punch-items/{id}/status", new { status = "InProgress" })).EnsureSuccessStatusCode();
        (await Client.PostAsJsonAsync($"/api/punch-items/{id}/status", new { status = "Fixed" })).EnsureSuccessStatusCode();
        (await Client.PostAsJsonAsync($"/api/punch-items/{id}/verify", new { })).EnsureSuccessStatusCode();

        var res = await Client.PutAsJsonAsync($"/api/punch-items/{id}", new
        {
            title = "renamed",
            severity = "Low",
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task BulkDelete_OpenOnly()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateProjectAsync();
        var a = await CreatePunchAsync(projectId);
        var b = await CreatePunchAsync(projectId);
        var c = await CreatePunchAsync(projectId);
        (await Client.PostAsJsonAsync($"/api/punch-items/{b}/status", new { status = "InProgress" })).EnsureSuccessStatusCode();

        var res = await Client.PostAsJsonAsync("/api/punch-items/bulk-delete", new { ids = new[] { a, b, c, 999_999 } });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("requested").GetInt32().Should().Be(4);
        body.GetProperty("deleted").GetInt32().Should().Be(2);
        body.GetProperty("failures").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task Get_UnknownId_ReturnsNotFound()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        (await Client.GetAsync("/api/punch-items/999999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // -------- helpers --------

    private async Task<int> CreateProjectAsync()
    {
        var customerId = await FirstCustomerIdAsync();
        var res = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name = $"Punch fixture {Guid.NewGuid():N}",
            customerId,
        });
        res.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(res)).GetProperty("id").GetInt32();
    }

    private async Task<int> CreatePunchAsync(int projectId)
    {
        var res = await Client.PostAsJsonAsync("/api/punch-items", new
        {
            designProjectId = projectId,
            title = $"Punch {Guid.NewGuid():N}"[..8],
            severity = "Medium",
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
                Name = "Punch Test Customer " + Guid.NewGuid().ToString("N")[..6],
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
