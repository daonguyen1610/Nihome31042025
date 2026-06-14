using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// Integration coverage for the runtime RBAC matrix endpoints under
/// /api/admin/rbac. Security-critical: tests every refusal branch (auth,
/// permission gating, system-role immunity, privilege escalation, unknown
/// codes) end-to-end through the real ASP.NET pipeline + EF.
/// </summary>
public class RbacControllerTests : IntegrationTestBase
{
    public RbacControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    // ---------- AuthN / AuthZ ----------

    [Fact]
    public async Task WithoutAuth_ListRoles_ReturnsUnauthorized()
    {
        var res = await Client.GetAsync("/api/admin/rbac/roles");
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AsCustomer_ListRoles_ReturnsForbidden_NoRbacRolesView()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsCustomerAsync);
        var res = await Client.GetAsync("/api/admin/rbac/roles");
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AsCustomer_UpdateRolePermissions_ReturnsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsCustomerAsync);
        var bizRoleId = await WithDbAsync(db => Task.FromResult(db.Roles.First(r => !r.IsSystem).Id));

        var res = await Client.PutAsJsonAsync(
            $"/api/admin/rbac/roles/{bizRoleId}/permissions",
            new { permissions = new[] { "dashboard.view" } });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------- Reads ----------

    [Fact]
    public async Task AsAdmin_ListPermissions_ReturnsCatalog()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        var res = await Client.GetAsync("/api/admin/rbac/permissions");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await ReadJsonAsync(res);
        body.ValueKind.Should().Be(JsonValueKind.Array);
        var codes = body.EnumerateArray().Select(p => p.GetProperty("code").GetString()).ToList();
        codes.Should().Contain("rbac.roles.manage");
        codes.Should().Contain("dashboard.view");
    }

    [Fact]
    public async Task AsAdmin_ListRoles_ContainsSystemAndBusinessRoles()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        var res = await Client.GetAsync("/api/admin/rbac/roles");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await ReadJsonAsync(res);
        var codes = body.EnumerateArray().Select(r => r.GetProperty("code").GetString()).ToList();
        codes.Should().Contain("SUPER_ADMIN");
        codes.Should().Contain("ADMIN");
        codes.Should().Contain("USER");
    }

    [Fact]
    public async Task AsAdmin_GetRolePermissions_ReturnsExpectedShape()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        var adminId = await WithDbAsync(db => Task.FromResult(db.Roles.Single(r => r.Code == "ADMIN").Id));

        var res = await Client.GetAsync($"/api/admin/rbac/roles/{adminId}/permissions");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);

        body.TryGetProperty("role", out var role).Should().BeTrue();
        role.GetProperty("code").GetString().Should().Be("ADMIN");
        body.TryGetProperty("permissions", out var perms).Should().BeTrue();
        perms.ValueKind.Should().Be(JsonValueKind.Array);
        var codes = perms.EnumerateArray().Select(c => c.GetString()).ToList();
        codes.Should().Contain("rbac.roles.manage");
        codes.Should().NotContain("users.manage");
    }

    [Fact]
    public async Task GetRole_UnknownId_ReturnsNotFound()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        var res = await Client.GetAsync("/api/admin/rbac/roles/999999");
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- Writes ----------

    [Fact]
    public async Task UpdateRole_SuperAdminRole_Returns403_SystemImmune()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        var saId = await WithDbAsync(db => Task.FromResult(db.Roles.Single(r => r.Code == "SUPER_ADMIN").Id));

        var res = await Client.PutAsJsonAsync($"/api/admin/rbac/roles/{saId}", new { name = "Hacked" });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var stillName = await WithDbAsync(db => Task.FromResult(
            db.Roles.AsNoTracking().Single(r => r.Id == saId).Name));
        stillName.Should().NotBe("Hacked");
    }

    [Fact]
    public async Task UpdateRolePermissions_SuperAdminRole_Returns403_SystemImmune()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        var saId = await WithDbAsync(db => Task.FromResult(db.Roles.Single(r => r.Code == "SUPER_ADMIN").Id));

        var res = await Client.PutAsJsonAsync(
            $"/api/admin/rbac/roles/{saId}/permissions",
            new { permissions = new[] { "dashboard.view" } });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("ADMIN")]
    [InlineData("USER")]
    public async Task UpdateRolePermissions_AnySystemRole_Returns403(string roleCode)
    {
        // ADMIN and USER matrices are seeder-controlled to prevent self-lockout
        // (e.g. dropping rbac.roles.manage from ADMIN). Only business roles
        // are runtime-editable.
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        var roleId = await WithDbAsync(db => Task.FromResult(db.Roles.Single(r => r.Code == roleCode).Id));
        var before = await WithDbAsync(db => Task.FromResult(
            db.RolePermissions.Count(rp => rp.RoleId == roleId)));

        var res = await Client.PutAsJsonAsync(
            $"/api/admin/rbac/roles/{roleId}/permissions",
            new { permissions = new[] { "dashboard.view" } });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await ReadJsonAsync(res);
        body.GetProperty("error").GetString().Should().Be("system_role_immutable");

        var after = await WithDbAsync(db => Task.FromResult(
            db.RolePermissions.Count(rp => rp.RoleId == roleId)));
        after.Should().Be(before);
    }

    [Fact]
    public async Task UpdateRolePermissions_UnknownCodes_Returns400_WithOffendingList()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        var bizId = await WithDbAsync(db => Task.FromResult(db.Roles.First(r => !r.IsSystem).Id));

        var res = await Client.PutAsJsonAsync(
            $"/api/admin/rbac/roles/{bizId}/permissions",
            new { permissions = new[] { "dashboard.view", "does.not.exist" } });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ReadJsonAsync(res);
        body.GetProperty("error").GetString().Should().Be("unknown_permission_codes");
        body.GetProperty("offending").EnumerateArray()
            .Select(e => e.GetString())
            .Should().Contain("does.not.exist");
    }

    [Fact]
    public async Task UpdateRolePermissions_AdminEscalatesUsersManage_Returns403()
    {
        // Admin (default) does NOT have users.manage. Granting it via the
        // matrix would be privilege escalation and must be blocked.
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        var bizId = await WithDbAsync(db => Task.FromResult(db.Roles.First(r => !r.IsSystem).Id));

        var res = await Client.PutAsJsonAsync(
            $"/api/admin/rbac/roles/{bizId}/permissions",
            new { permissions = new[] { "dashboard.view", "users.manage" } });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await ReadJsonAsync(res);
        body.GetProperty("error").GetString().Should().Be("privilege_escalation_blocked");
        body.GetProperty("offending").EnumerateArray()
            .Select(e => e.GetString())
            .Should().Contain("users.manage");
    }

    [Fact]
    public async Task UpdateRolePermissions_AsSuperAdmin_Succeeds_AndUpdateMePermissionsReflectsChange()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        var bizId = await WithDbAsync(db => Task.FromResult(db.Roles.First(r => !r.IsSystem).Id));

        var res = await Client.PutAsJsonAsync(
            $"/api/admin/rbac/roles/{bizId}/permissions",
            new { permissions = new[] { "dashboard.view", "profile.me.view" } });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("permissions").EnumerateArray()
            .Select(e => e.GetString()).Should().BeEquivalentTo("dashboard.view", "profile.me.view");
    }

    [Fact]
    public async Task UpdateRole_BusinessRole_AcceptsLabelChanges()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        var bizId = await WithDbAsync(db => Task.FromResult(db.Roles.First(r => !r.IsSystem).Id));

        var res = await Client.PutAsJsonAsync(
            $"/api/admin/rbac/roles/{bizId}",
            new { labelKey = "rbac.role.test.label" });
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await ReadJsonAsync(res);
        body.GetProperty("labelKey").GetString().Should().Be("rbac.role.test.label");
    }

    [Fact]
    public async Task UpdateRole_AdminRoleDeactivation_Returns400()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        var adminId = await WithDbAsync(db => Task.FromResult(db.Roles.Single(r => r.Code == "ADMIN").Id));

        var res = await Client.PutAsJsonAsync($"/api/admin/rbac/roles/{adminId}", new { isActive = false });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ReadJsonAsync(res);
        body.GetProperty("error").GetString().Should().Be("invalid_request");
    }

    // ---------- CreateRole ----------

    [Fact]
    public async Task CreateRole_AsSuperAdmin_Returns201_AndIsRetrievable()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        var code = $"E2E_{Guid.NewGuid():N}".Substring(0, 18).ToUpperInvariant();

        var res = await Client.PostAsJsonAsync("/api/admin/rbac/roles", new
        {
            code,
            name = "E2E Created",
            permissions = new[] { "dashboard.view" },
        });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        res.Headers.Location.Should().NotBeNull();
        var body = await ReadJsonAsync(res);
        body.GetProperty("code").GetString().Should().Be(code);
        body.GetProperty("isSystem").GetBoolean().Should().BeFalse();

        // Side effect: retrievable via GET.
        var newId = body.GetProperty("id").GetInt32();
        var get = await Client.GetAsync($"/api/admin/rbac/roles/{newId}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("SUPER_ADMIN")]
    [InlineData("ADMIN")]
    [InlineData("USER")]
    public async Task CreateRole_ReservedCode_Returns409(string code)
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        var res = await Client.PostAsJsonAsync("/api/admin/rbac/roles", new { code, name = "Reserved" });
        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await ReadJsonAsync(res);
        body.GetProperty("error").GetString().Should().Be("role_code_conflict");
    }

    [Fact]
    public async Task CreateRole_InvalidCodeFormat_Returns400()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        var res = await Client.PostAsJsonAsync("/api/admin/rbac/roles", new
        {
            code = "lower-case",
            name = "Bad",
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateRole_EscalationBlocked_ForAdmin()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        var code = $"E2E_{Guid.NewGuid():N}".Substring(0, 18).ToUpperInvariant();

        var res = await Client.PostAsJsonAsync("/api/admin/rbac/roles", new
        {
            code,
            name = "Admin Try",
            permissions = new[] { "users.manage" },
        });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await ReadJsonAsync(res);
        body.GetProperty("error").GetString().Should().Be("privilege_escalation_blocked");
    }

    // ---------- DeleteRole ----------

    [Theory]
    [InlineData("SUPER_ADMIN")]
    [InlineData("ADMIN")]
    [InlineData("USER")]
    public async Task DeleteRole_AnySystemRole_Returns403(string code)
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        var id = await WithDbAsync(db => Task.FromResult(db.Roles.Single(r => r.Code == code).Id));

        var res = await Client.DeleteAsync($"/api/admin/rbac/roles/{id}");
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var stillExists = await WithDbAsync(db => Task.FromResult(
            db.Roles.AsNoTracking().Any(r => r.Id == id)));
        stillExists.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteRole_Success_RoundTripCreateThenDelete()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        var code = $"E2E_{Guid.NewGuid():N}".Substring(0, 18).ToUpperInvariant();

        var created = await Client.PostAsJsonAsync("/api/admin/rbac/roles", new { code, name = "Throwaway" });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var newId = (await ReadJsonAsync(created)).GetProperty("id").GetInt32();

        var del = await Client.DeleteAsync($"/api/admin/rbac/roles/{newId}");
        del.StatusCode.Should().Be(HttpStatusCode.OK);

        var get = await Client.GetAsync($"/api/admin/rbac/roles/{newId}");
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteRole_BlockedWhenUsersAssigned_Returns409()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        var code = $"E2E_{Guid.NewGuid():N}".Substring(0, 18).ToUpperInvariant();
        var created = await Client.PostAsJsonAsync("/api/admin/rbac/roles", new { code, name = "Pinned" });
        var newId = (await ReadJsonAsync(created)).GetProperty("id").GetInt32();

        // Pin a user to it (use seeded customer).
        await WithDbAsync(async db =>
        {
            var u = db.Users.Single(x => x.PhoneNumber == TestDataSeeder.CustomerPhone);
            u.RoleEntityId = newId;
            await db.SaveChangesAsync();
        });

        var del = await Client.DeleteAsync($"/api/admin/rbac/roles/{newId}");
        del.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await ReadJsonAsync(del);
        body.GetProperty("error").GetString().Should().Be("role_in_use");
        body.GetProperty("userCount").GetInt32().Should().Be(1);

        // Cleanup so other tests on this fixture aren't poisoned.
        await WithDbAsync(async db =>
        {
            var u = db.Users.Single(x => x.PhoneNumber == TestDataSeeder.CustomerPhone);
            u.RoleEntityId = db.Roles.Single(r => r.Code == "USER").Id;
            await db.SaveChangesAsync();
        });
    }
}
