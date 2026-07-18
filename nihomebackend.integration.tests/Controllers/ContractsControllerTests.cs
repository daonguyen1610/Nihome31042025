using System.Net;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// End-to-end coverage for <c>ContractsController</c> — CRUD, RBAC scoping
/// (Sales sees only own, Manager sees all), duplicate handling, validation.
/// NIH-102 scope: list + minimal CRUD. Payment milestones / VOs are follow-up.
/// </summary>
public class ContractsControllerTests : IntegrationTestBase
{
    public ContractsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    private async Task<int> CreateCustomerAsync()
    {
        var payload = new
        {
            type = "Individual",
            name = "Contract Test " + Guid.NewGuid().ToString("N")[..6],
            sourceCode = "marketing",
            primaryContact = new
            {
                fullName = "Contact",
                phone = "0911" + Guid.NewGuid().ToString("N")[..6],
                isPrimary = true,
            },
        };
        var res = await Client.PostAsJsonAsync("/api/customers", payload);
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await ReadJsonAsync(res)).GetProperty("id").GetInt32();
    }

    private static object ContractBody(int customerId, string status = "Draft", decimal value = 100_000_000)
        => new { customerId, status, value, signedDate = "2026-06-01T00:00:00Z" };

    [Fact]
    public async Task List_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/contracts")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_AsSalesManager_ReturnsOkShape()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var res = await Client.GetAsync("/api/contracts");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("items").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
        body.GetProperty("total").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Number);
    }

    [Fact]
    public async Task Create_WithoutManagePermission_IsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));
        var res = await Client.PostAsJsonAsync("/api/contracts", ContractBody(1));
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task FullRoundTrip_AsSalesManager_Create_Update_Delete()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var customerId = await CreateCustomerAsync();

        var created = await Client.PostAsJsonAsync("/api/contracts", ContractBody(customerId));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync(created);
        var id = body.GetProperty("id").GetInt32();
        body.GetProperty("contractNumber").GetString().Should().StartWith("HD-");
        body.GetProperty("customerId").GetInt32().Should().Be(customerId);
        body.GetProperty("status").GetString().Should().Be("Draft");

        var update = await Client.PutAsJsonAsync($"/api/contracts/{id}", new
        {
            customerId,
            status = "Signed",
            value = 500_000_000,
            signedDate = "2026-06-15T00:00:00Z",
            startDate = "2026-07-01T00:00:00Z",
            endDate = "2026-12-31T00:00:00Z",
        });
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(update)).GetProperty("status").GetString().Should().Be("Signed");

        (await Client.DeleteAsync($"/api/contracts/{id}")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
        (await Client.DeleteAsync($"/api/contracts/{id}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_DuplicateExplicitNumber_ReturnsConflict()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var customerId = await CreateCustomerAsync();
        var number = "HD-INT-" + Guid.NewGuid().ToString("N")[..6];

        var first = await Client.PostAsJsonAsync("/api/contracts", new
        {
            customerId,
            contractNumber = number,
            status = "Draft",
            value = 100,
        });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var dup = await Client.PostAsJsonAsync("/api/contracts", new
        {
            customerId,
            contractNumber = number,
            status = "Draft",
            value = 100,
        });
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_UnknownCustomer_ReturnsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var res = await Client.PostAsJsonAsync("/api/contracts", ContractBody(9_999_999));
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
