using System.Net;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// End-to-end coverage for <c>MasterDataController</c>: seeded categories,
/// public reads, admin CRUD, RBAC enforcement, and duplicate handling.
/// </summary>
public class MasterDataControllerTests : IntegrationTestBase
{
    public MasterDataControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetCategories_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/master-data/categories")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCategories_AsAdmin_ReturnsSeededCategories()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);

        var res = await Client.GetAsync("/api/master-data/categories");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await ReadJsonAsync(res);
        var categories = body.EnumerateArray()
            .Select(e => e.GetProperty("category").GetString())
            .ToList();

        categories.Should().Contain(new[]
        {
            "customer_type", "customer_source", "customer_status",
            "lead_status",
            "opportunity_stage", "opportunity_lost_reason",
            "quote_status", "tender_status", "contract_status",
            "design_discipline",
            "permit_type", "permit_status",
        });
    }

    [Fact]
    public async Task GetByCategory_AsAdmin_ReturnsActiveOptionsByDefault()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);

        var res = await Client.GetAsync("/api/master-data/customer_type");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var arr = await ReadJsonAsync(res);
        arr.GetArrayLength().Should().BeGreaterThanOrEqualTo(2);

        var codes = arr.EnumerateArray()
            .Select(e => e.GetProperty("code").GetString())
            .ToList();
        codes.Should().Contain(new[] { "individual", "company" });
    }

    [Fact]
    public async Task GetByCategory_IsCaseInsensitiveOnRoute()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);

        var res = await Client.GetAsync("/api/master-data/Customer_Type");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(res)).GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Create_WithoutManagePermission_IsForbidden()
    {
        // WAREHOUSE role has master-data.view but not master-data.manage.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));

        var code = "wh-" + Guid.NewGuid().ToString("N")[..6];
        var res = await Client.PostAsJsonAsync("/api/master-data/customer_source", new
        {
            code,
            name = "Warehouse creation attempt",
            isActive = true,
            sortOrder = 99,
        });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Read_WithBusinessRole_IsAllowedByMasterDataViewPermission()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));

        (await Client.GetAsync("/api/master-data/customer_type")).StatusCode
            .Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task FullRoundTrip_AsAdmin_Create_Update_Delete()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);

        var code = "src-" + Guid.NewGuid().ToString("N")[..6];
        var created = await Client.PostAsJsonAsync("/api/master-data/customer_source", new
        {
            code,
            name = "Integration test",
            description = "Sự kiện demo",
            isActive = true,
            sortOrder = 50,
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync(created);
        var id = body.GetProperty("id").GetInt32();
        body.GetProperty("category").GetString().Should().Be("customer_source");
        body.GetProperty("code").GetString().Should().Be(code);

        var duplicate = await Client.PostAsJsonAsync("/api/master-data/customer_source", new
        {
            code,
            name = "Dup",
            isActive = true,
            sortOrder = 51,
        });
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var updated = await Client.PutAsJsonAsync($"/api/master-data/options/{id}", new
        {
            code,
            name = "Renamed integration test",
            isActive = false,
            sortOrder = 60,
        });
        updated.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(updated)).GetProperty("name").GetString().Should().Be("Renamed integration test");

        (await Client.DeleteAsync($"/api/master-data/options/{id}")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        (await Client.DeleteAsync($"/api/master-data/options/{id}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_WithInvalidPayload_ReturnsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);

        var res = await Client.PostAsJsonAsync("/api/master-data/customer_source", new
        {
            // missing code and name
            isActive = true,
            sortOrder = 1,
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task IncludeInactive_QueryParam_ReturnsInactiveRowsToo()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);

        // Create one active + one inactive in an isolated category so the assertion
        // is deterministic even when seeded categories are already populated.
        var category = "it-cat-" + Guid.NewGuid().ToString("N")[..6];
        (await Client.PostAsJsonAsync($"/api/master-data/{category}", new
        {
            code = "a",
            name = "Active",
            isActive = true,
            sortOrder = 1,
        })).StatusCode.Should().Be(HttpStatusCode.Created);
        (await Client.PostAsJsonAsync($"/api/master-data/{category}", new
        {
            code = "z",
            name = "Archived",
            isActive = false,
            sortOrder = 2,
        })).StatusCode.Should().Be(HttpStatusCode.Created);

        var activeOnly = await ReadJsonAsync(await Client.GetAsync($"/api/master-data/{category}"));
        activeOnly.GetArrayLength().Should().Be(1);

        var withInactive = await ReadJsonAsync(await Client.GetAsync($"/api/master-data/{category}?includeInactive=true"));
        withInactive.GetArrayLength().Should().Be(2);
    }
}
