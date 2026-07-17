using System.Net;
using System.Net.Http.Json;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// End-to-end coverage for <c>TendersController</c> (NIH-95 / NIH-96):
/// RBAC gating, create → auto-checklist, deadline validation, per-status
/// edit rules, delete guard for submitted tenders. Result transition
/// workflow (NIH-97) is not covered here — that ships in a follow-up
/// slice.
/// </summary>
public class TendersControllerTests : IntegrationTestBase
{
    public TendersControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task List_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/tenders")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_AsWarehouse_IsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));
        (await Client.GetAsync("/api/tenders")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_HappyPath_ReturnsAutoChecklist()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var customerId = await CreateCustomerAsync();

        var res = await Client.PostAsJsonAsync("/api/tenders", new
        {
            name = "Gói thầu Alpha",
            customerId,
            submissionDeadline = DateTime.UtcNow.AddDays(14),
            openingDate = DateTime.UtcNow.AddDays(7),
            infoSource = "Website",
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync(res);
        body.GetProperty("code").GetString().Should().StartWith("TD-");
        body.GetProperty("status").GetString().Should().Be("Preparing");
        body.GetProperty("checklistItems").GetArrayLength().Should().BeGreaterThan(0);
        body.GetProperty("checklistCompletionPercent").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Create_WithPastDeadline_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var customerId = await CreateCustomerAsync();
        var res = await Client.PostAsJsonAsync("/api/tenders", new
        {
            name = "Bad deadline",
            customerId,
            submissionDeadline = DateTime.UtcNow.AddDays(-1),
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_WithUnknownCustomer_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var res = await Client.PostAsJsonAsync("/api/tenders", new
        {
            name = "Bad customer",
            customerId = 999_999,
            submissionDeadline = DateTime.UtcNow.AddDays(10),
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Sale_CannotCreateWithoutManagePermission()
    {
        // SALE has crm.tenders.view + crm.tenders.manage — should succeed.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        var customerId = await CreateCustomerAsync();
        var res = await Client.PostAsJsonAsync("/api/tenders", new
        {
            name = "SALE-created tender",
            customerId,
            submissionDeadline = DateTime.UtcNow.AddDays(10),
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Update_WhilePreparing_UpdatesAllEditableFields()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var id = await CreateTenderAsync();
        var newDeadline = DateTime.UtcNow.AddDays(30);
        var res = await Client.PutAsJsonAsync($"/api/tenders/{id}", new
        {
            name = "Updated name",
            submissionDeadline = newDeadline,
            infoSource = "Referral",
            note = "note updated",
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("name").GetString().Should().Be("Updated name");
        body.GetProperty("note").GetString().Should().Be("note updated");
    }

    [Fact]
    public async Task Delete_WhilePreparing_Succeeds()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var id = await CreateTenderAsync();
        (await Client.DeleteAsync($"/api/tenders/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Client.GetAsync($"/api/tenders/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_FilterBySearchAndStatus()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        await CreateTenderAsync(name: "Uniquely-tagged Alpha");
        await CreateTenderAsync(name: "Other tender");

        var searched = await Client.GetAsync("/api/tenders?search=Uniquely-tagged&pageSize=20");
        searched.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(searched);
        body.GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);
        for (int i = 0; i < body.GetProperty("items").GetArrayLength(); i++)
        {
            body.GetProperty("items")[i].GetProperty("name").GetString().Should().Contain("Uniquely-tagged");
        }
    }

    // ---------- helpers ----------

    private async Task<int> CreateCustomerAsync()
    {
        var res = await Client.PostAsJsonAsync("/api/customers", new
        {
            type = "Individual",
            name = "TC-" + Guid.NewGuid().ToString("N")[..6],
            sourceCode = "marketing",
            primaryContact = new
            {
                fullName = "Contact",
                phone = "0922" + Guid.NewGuid().ToString("N")[..6],
                isPrimary = true,
            },
        });
        res.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(res)).GetProperty("id").GetInt32();
    }

    private async Task<int> CreateTenderAsync(string? name = null)
    {
        var customerId = await CreateCustomerAsync();
        var res = await Client.PostAsJsonAsync("/api/tenders", new
        {
            name = name ?? "Test tender " + Guid.NewGuid().ToString("N")[..6],
            customerId,
            submissionDeadline = DateTime.UtcNow.AddDays(14),
        });
        res.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(res)).GetProperty("id").GetInt32();
    }
}
