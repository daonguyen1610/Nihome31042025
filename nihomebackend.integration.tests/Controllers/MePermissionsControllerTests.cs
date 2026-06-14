using System.Net;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// Integration coverage for GET /api/users/me/permissions (PermissionService
/// resolution through the real HTTP + JWT + EF pipeline). Security-critical:
/// asserts the response shape and the actual permission sets per role match
/// the seed defaults.
/// </summary>
public class MePermissionsControllerTests : IntegrationTestBase
{
    public MePermissionsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task WithoutAuth_ReturnsUnauthorized()
    {
        var res = await Client.GetAsync("/api/users/me/permissions");
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AsSuperAdmin_IncludesEveryCoreCode()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        var res = await Client.GetAsync("/api/users/me/permissions");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await ReadJsonAsync(res);
        body.GetProperty("role").GetString().Should().Be("SUPER_ADMIN");
        var perms = body.GetProperty("permissions").EnumerateArray().Select(e => e.GetString()).ToHashSet();

        // SUPER_ADMIN must always hold the catalog superset.
        perms.Should().Contain(new[]
        {
            "dashboard.view",
            "users.view",
            "users.manage",
            "rbac.roles.view",
            "rbac.roles.manage",
            "profile.me.view",
            "profile.me.update",
        });
    }

    [Fact]
    public async Task AsAdmin_HasRbacRolesButNotUsersManage()
    {
        // Security guarantee: ADMIN role manages everything EXCEPT user
        // accounts (which only SUPER_ADMIN may touch). Regression here would
        // be a privilege-escalation bug.
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        var res = await Client.GetAsync("/api/users/me/permissions");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await ReadJsonAsync(res);
        body.GetProperty("role").GetString().Should().Be("ADMIN");
        var perms = body.GetProperty("permissions").EnumerateArray().Select(e => e.GetString()).ToList();

        perms.Should().Contain("rbac.roles.manage");
        perms.Should().Contain("users.view");
        perms.Should().NotContain("users.manage");
    }

    [Fact]
    public async Task AsCustomerUser_OnlyHasProfileMeCodes()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsCustomerAsync);
        var res = await Client.GetAsync("/api/users/me/permissions");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await ReadJsonAsync(res);
        body.GetProperty("role").GetString().Should().Be("USER");
        var perms = body.GetProperty("permissions").EnumerateArray().Select(e => e.GetString()).ToList();

        perms.Should().BeEquivalentTo(new[] { "profile.me.view", "profile.me.update" });
    }

    [Fact]
    public async Task ResponseShape_ContainsRoleRoleIdAndPermissions()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        var res = await Client.GetAsync("/api/users/me/permissions");
        var body = await ReadJsonAsync(res);

        body.TryGetProperty("role", out _).Should().BeTrue();
        body.TryGetProperty("roleId", out _).Should().BeTrue();
        body.TryGetProperty("permissions", out var perms).Should().BeTrue();
        perms.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
    }
}
