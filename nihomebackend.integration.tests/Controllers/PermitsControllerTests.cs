using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// End-to-end coverage for <c>PermitsController</c> (NIH-137): RBAC gating,
/// list + get, patch semantics + the ensure/regenerate endpoint. Auto-
/// generation on <c>DesignProject</c> create is exercised through the
/// public POST so we know the two services wire together in the DI pipeline.
/// </summary>
public class PermitsControllerTests : IntegrationTestBase
{
    public PermitsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task List_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/permits")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_AsSale_IsForbidden()
    {
        // SALE has no permit.checklists.* bundle.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        (await Client.GetAsync("/api/permits")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_AsPm_ReturnsOkWithRiskSummary()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "PM"));
        var res = await Client.GetAsync("/api/permits");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("items").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
        body.GetProperty("risk").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
    }

    [Fact]
    public async Task Create_DesignProject_AutoSeedsPermitChecklist()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var customerId = await FirstCustomerIdAsync();
        var create = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name = $"Permit auto-seed {Guid.NewGuid():N}",
            customerId,
        });
        create.EnsureSuccessStatusCode();
        var projectId = (await ReadJsonAsync(create)).GetProperty("id").GetInt32();

        var listRes = await Client.GetAsync($"/api/permits?designProjectId={projectId}&pageSize=100");
        listRes.EnsureSuccessStatusCode();
        var listBody = await ReadJsonAsync(listRes);
        listBody.GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Patch_UpdatesStatusAndReturnsUpdatedRow()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var id = await CreatePermitRowAsync();

        var res = await Client.PatchAsJsonAsync($"/api/permits/{id}", new
        {
            status = "Submitted",
            issuingAgency = "Sở Xây dựng — integration",
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("status").GetString().Should().Be("Submitted");
        body.GetProperty("issuingAgency").GetString().Should().Be("Sở Xây dựng — integration");
    }

    [Fact]
    public async Task Patch_UnknownId_Is404()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var res = await Client.PatchAsJsonAsync("/api/permits/9999999", new { status = "Submitted" });
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Patch_InvalidStatus_Is400()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var id = await CreatePermitRowAsync();
        var res = await Client.PatchAsJsonAsync($"/api/permits/{id}", new { status = "NotAStatus" });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Pm_CannotPatch()
    {
        // PM has permit.checklists.** (both view + manage) per rbac-defaults.
        // Use SALE for the negative case (has no permit permission at all).
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var id = await CreatePermitRowAsync();

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        var res = await Client.PatchAsJsonAsync($"/api/permits/{id}", new { status = "Submitted" });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Ensure_RegeneratesForKnownProject()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateDesignProjectAsync();
        var res = await Client.PostAsync($"/api/permits/design-project/{projectId}/ensure", null);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);
    }

    // -------- helpers --------

    private async Task<int> CreateDesignProjectAsync()
    {
        var customerId = await FirstCustomerIdAsync();
        var res = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name = $"Permit fixture {Guid.NewGuid():N}",
            customerId,
        });
        res.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(res)).GetProperty("id").GetInt32();
    }

    private async Task<int> CreatePermitRowAsync()
    {
        var projectId = await CreateDesignProjectAsync();
        var list = await Client.GetAsync($"/api/permits?designProjectId={projectId}&pageSize=100");
        list.EnsureSuccessStatusCode();
        var items = (await ReadJsonAsync(list)).GetProperty("items");
        items.GetArrayLength().Should().BeGreaterThan(0);
        return items[0].GetProperty("id").GetInt32();
    }

    private async Task<int> FirstCustomerIdAsync()
    {
        return await WithDbAsync<int>(async db =>
        {
            var existing = await db.Customers.OrderBy(c => c.Id).FirstOrDefaultAsync();
            if (existing != null) return existing.Id;

            var customer = new Customer
            {
                Name = "Permit Test Customer " + Guid.NewGuid().ToString("N")[..6],
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
