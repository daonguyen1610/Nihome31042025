using System.Net;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// End-to-end coverage for <c>WorkflowsController</c> — CRUD, RBAC
/// (view vs manage), validation, and duplicate handling.
/// NIH-225 config-only scope.
/// </summary>
public class WorkflowsControllerTests : IntegrationTestBase
{
    public WorkflowsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    private static object BuildPayload(
        string module = "quotes",
        string action = "approve",
        string name = "Duyệt báo giá test",
        bool isActive = true,
        object[]? steps = null)
    {
        steps ??= new object[]
        {
            new { order = 1, name = "Trưởng nhóm", approverRoleCode = "SALES_MANAGER", slaHours = 24, requireAllApprovers = false },
            new { order = 2, name = "Giám đốc", approverRoleCode = "BGD", slaHours = 48, requireAllApprovers = true },
        };
        return new
        {
            module,
            action,
            name,
            description = "Integration test workflow",
            isActive,
            sortOrder = 5,
            steps,
        };
    }

    private static string RandomAction() => "act-" + Guid.NewGuid().ToString("N")[..6];

    // ---------------- Auth / RBAC ----------------

    [Fact]
    public async Task List_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/workflows")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_AsAdmin_ReturnsOk()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        (await Client.GetAsync("/api/workflows")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task List_AsBusinessRole_IsAllowedByViewPermission()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        (await Client.GetAsync("/api/workflows")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_WithoutManagePermission_IsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));

        var res = await Client.PostAsJsonAsync("/api/workflows", BuildPayload(action: RandomAction()));
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------------- CRUD round trip ----------------

    [Fact]
    public async Task FullRoundTrip_AsAdmin_Create_Update_Delete()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);

        var action = RandomAction();
        var created = await Client.PostAsJsonAsync("/api/workflows", BuildPayload(action: action));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync(created);
        var id = body.GetProperty("id").GetInt32();
        body.GetProperty("module").GetString().Should().Be("quotes");
        body.GetProperty("action").GetString().Should().Be(action);
        body.GetProperty("steps").GetArrayLength().Should().Be(2);
        body.GetProperty("steps")[0].GetProperty("order").GetInt32().Should().Be(1);

        // Update
        var updatePayload = BuildPayload(
            action: action,
            name: "Renamed via integration test",
            steps: new object[]
            {
                new { order = 1, name = "Solo step", approverRoleCode = "BGD", slaHours = 12, requireAllApprovers = false },
            });
        var updated = await Client.PutAsJsonAsync($"/api/workflows/{id}", updatePayload);
        updated.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(updated)).GetProperty("name").GetString()
            .Should().Be("Renamed via integration test");

        // Delete
        (await Client.DeleteAsync($"/api/workflows/{id}")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
        (await Client.DeleteAsync($"/api/workflows/{id}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_DuplicateModuleActionPair_ReturnsConflict()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);

        var action = RandomAction();
        (await Client.PostAsJsonAsync("/api/workflows", BuildPayload(action: action)))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await Client.PostAsJsonAsync("/api/workflows", BuildPayload(action: action)))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ---------------- Validation ----------------

    [Fact]
    public async Task Create_WithEmptySteps_ReturnsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);

        var res = await Client.PostAsJsonAsync("/api/workflows",
            BuildPayload(action: RandomAction(), steps: Array.Empty<object>()));
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_WithUnknownApproverRole_ReturnsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);

        var res = await Client.PostAsJsonAsync("/api/workflows", BuildPayload(
            action: RandomAction(),
            steps: new object[]
            {
                new { order = 1, name = "X", approverRoleCode = "GHOST_ROLE_NOT_IN_RBAC", slaHours = 24, requireAllApprovers = false },
            }));
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
