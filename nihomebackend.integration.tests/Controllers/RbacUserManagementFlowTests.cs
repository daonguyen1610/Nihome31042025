using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// End-to-end integration test for the RBAC → User Management contract.
///
/// Exercises the whole chain through the public HTTP API only — no SQL, no DB
/// mutations — so a regression that breaks any link surfaces here:
///
///   1. SUPER_ADMIN creates a custom business role with a curated permission set
///      (<c>POST /api/admin/rbac/roles</c>).
///   2. SUPER_ADMIN creates a user whose <c>role</c> field is the new code
///      (<c>POST /api/users</c>) — proves the user-CRUD layer accepts arbitrary
///      RBAC codes, not just the legacy enum.
///   3. The new user logs in and gets back the canonical RBAC code + roleId
///      (<c>POST /api/auth/login</c>) — proves auth response surfaces the
///      RBAC linkage so the SPA admin-area gate works.
///   4. <c>GET /api/users/me/permissions</c> returns exactly the granted set.
///   5. The user can hit an endpoint gated by a permission they have, and is
///      403'd on an endpoint gated by one they don't have.
///   6. SUPER_ADMIN edits the role's permissions; the user logs in again and
///      the new permission set is reflected in /me/permissions (no stale
///      token / role-cache bug).
/// </summary>
public class RbacUserManagementFlowTests : IntegrationTestBase
{
    public RbacUserManagementFlowTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task FullFlow_CreateRole_AssignToUser_LoginAndAccessGatedEndpoints()
    {
        // -------- 1. Authenticate as SUPER_ADMIN --------
        var saClient = Factory.CreateClient();
        await AuthTestHelper.AuthenticateAsync(saClient, AuthTestHelper.LoginAsSuperAdminAsync);

        // -------- 2. Create a custom business role with a curated set --------
        // Permission set chosen so two contrasting endpoints can be probed:
        //   - `users.view`            → can list users      (allowed path)
        //   - (intentionally absent)  → cannot manage roles (denied path)
        var roleCode = $"E2E_FLOW_{Guid.NewGuid():N}".Substring(0, 18).ToUpperInvariant();
        var grantedPermissions = new[] { "dashboard.view", "users.view" };

        var createRoleRes = await saClient.PostAsJsonAsync("/api/admin/rbac/roles", new
        {
            code = roleCode,
            name = "Flow Test Role",
            permissions = grantedPermissions,
        });
        createRoleRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var roleBody = await ReadJsonAsync(createRoleRes);
        var roleId = roleBody.GetProperty("id").GetInt32();

        // -------- 3. Create a user assigned to that custom role --------
        var phone = "099" + new Random().Next(1000000, 9999999).ToString();
        var email = $"flow-{Guid.NewGuid():N}@e2e.nihome.local";
        const string password = "P@ssword1";

        var createUserRes = await saClient.PostAsJsonAsync("/api/users", new
        {
            phoneNumber = phone,
            fullName = "Flow Test User",
            email,
            password,
            role = roleCode,
        });
        createUserRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var userBody = await ReadJsonAsync(createUserRes);
        userBody.GetProperty("role").GetString().Should().Be(roleCode);
        userBody.GetProperty("roleId").GetInt32().Should().Be(roleId);
        userBody.GetProperty("roleName").GetString().Should().Be("Flow Test Role");
        var userId = userBody.GetProperty("id").GetInt32();

        // -------- 4. Log in as the new user (no auth bleed: fresh client) --------
        var userClient = Factory.CreateClient();
        var loginRes = await userClient.PostAsJsonAsync("/api/auth/login", new { phoneNumber = phone, password });
        loginRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginBody = await ReadJsonAsync(loginRes);
        // Auth response must expose the canonical RBAC code (not the legacy enum
        // mirror, which for a custom role is `USER`) — otherwise the SPA admin
        // gate would lock this user out of /admin.
        loginBody.GetProperty("role").GetString().Should().Be(roleCode);
        loginBody.GetProperty("roleId").GetInt32().Should().Be(roleId);
        var userToken = loginBody.GetProperty("accessToken").GetString();
        userClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userToken);

        // -------- 5. /me/permissions reflects exactly the granted set --------
        var mePermsRes = await userClient.GetAsync("/api/users/me/permissions");
        mePermsRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var mePermsBody = await ReadJsonAsync(mePermsRes);
        mePermsBody.GetProperty("role").GetString().Should().Be(roleCode);
        mePermsBody.GetProperty("roleId").GetInt32().Should().Be(roleId);
        var perms = mePermsBody.GetProperty("permissions").EnumerateArray()
            .Select(p => p.GetString())
            .ToHashSet();
        perms.Should().BeEquivalentTo(grantedPermissions);

        // -------- 6. Permission gates honor the matrix --------
        // Allowed: users.view → GET /api/users
        var allowedRes = await userClient.GetAsync("/api/users");
        allowedRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Denied: rbac.roles.manage → POST /api/admin/rbac/roles
        var deniedRes = await userClient.PostAsJsonAsync("/api/admin/rbac/roles", new
        {
            code = "FORBIDDEN_E2E",
            name = "Should Be Forbidden",
            permissions = Array.Empty<string>(),
        });
        deniedRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // -------- 7. Permission update is reflected on next login --------
        // SUPER_ADMIN adds `rbac.roles.view` to the role. A fresh login by the
        // user picks up the new permission. (Existing token claims are coarse
        // — PermissionService resolves the live set per request — so even
        // mid-session the gate would honor it; relogin is the worst-case path.)
        var expandedPermissions = grantedPermissions.Concat(new[] { "rbac.roles.view" }).ToArray();
        var updatePermsRes = await saClient.PutAsJsonAsync(
            $"/api/admin/rbac/roles/{roleId}/permissions",
            new { permissions = expandedPermissions });
        updatePermsRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var userClient2 = Factory.CreateClient();
        var relogin = await userClient2.PostAsJsonAsync("/api/auth/login", new { phoneNumber = phone, password });
        relogin.StatusCode.Should().Be(HttpStatusCode.OK);
        var relog = await ReadJsonAsync(relogin);
        userClient2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", relog.GetProperty("accessToken").GetString());

        var mePerms2Res = await userClient2.GetAsync("/api/users/me/permissions");
        var mePerms2Body = await ReadJsonAsync(mePerms2Res);
        var perms2 = mePerms2Body.GetProperty("permissions").EnumerateArray()
            .Select(p => p.GetString())
            .ToHashSet();
        perms2.Should().BeEquivalentTo(expandedPermissions);

        // The newly-granted permission unlocks the previously-denied endpoint
        // (rbac.roles.view ⊃ list endpoint, but NOT manage — manage is still 403).
        var listRolesRes = await userClient2.GetAsync("/api/admin/rbac/roles");
        listRolesRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // -------- 8. Cleanup --------
        // Soft-delete the user (DELETE is soft on this controller); detach from
        // the role so the role can be hard-deleted; then delete the role.
        await saClient.DeleteAsync($"/api/users/{userId}");
        await saClient.PutAsJsonAsync($"/api/users/{userId}", new { role = "USER" });
        var deleteRoleRes = await saClient.DeleteAsync($"/api/admin/rbac/roles/{roleId}");
        deleteRoleRes.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateUser_ChangeToNewRoleCode_ChangesEffectivePermissions()
    {
        // Variant flow: an existing user is *promoted* into a new custom role
        // mid-life, not created with it. Proves UsersController.UpdateAsync
        // re-resolves the RBAC linkage (not just the enum mirror) and that the
        // user's permissions immediately match the new role on next login.

        var saClient = Factory.CreateClient();
        await AuthTestHelper.AuthenticateAsync(saClient, AuthTestHelper.LoginAsSuperAdminAsync);

        // Two roles with non-overlapping permission sets so the swap is observable.
        var roleA = $"E2E_A_{Guid.NewGuid():N}".Substring(0, 18).ToUpperInvariant();
        var roleB = $"E2E_B_{Guid.NewGuid():N}".Substring(0, 18).ToUpperInvariant();
        var permsA = new[] { "dashboard.view", "users.view" };
        var permsB = new[] { "dashboard.view", "content.news.view" };

        var roleARes = await saClient.PostAsJsonAsync("/api/admin/rbac/roles",
            new { code = roleA, name = "Role A", permissions = permsA });
        roleARes.StatusCode.Should().Be(HttpStatusCode.Created);
        var roleAId = (await ReadJsonAsync(roleARes)).GetProperty("id").GetInt32();

        var roleBRes = await saClient.PostAsJsonAsync("/api/admin/rbac/roles",
            new { code = roleB, name = "Role B", permissions = permsB });
        roleBRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var roleBId = (await ReadJsonAsync(roleBRes)).GetProperty("id").GetInt32();

        var phone = "099" + new Random().Next(1000000, 9999999).ToString();
        const string password = "P@ssword1";
        var createUserRes = await saClient.PostAsJsonAsync("/api/users", new
        {
            phoneNumber = phone,
            fullName = "Promotable User",
            email = $"promote-{Guid.NewGuid():N}@e2e.nihome.local",
            password,
            role = roleA,
        });
        createUserRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var userId = (await ReadJsonAsync(createUserRes)).GetProperty("id").GetInt32();

        // Promote: PUT /api/users/{id} with the new role code.
        var updateRes = await saClient.PutAsJsonAsync($"/api/users/{userId}", new { role = roleB });
        updateRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var updateBody = await ReadJsonAsync(updateRes);
        updateBody.GetProperty("role").GetString().Should().Be(roleB);
        updateBody.GetProperty("roleId").GetInt32().Should().Be(roleBId);

        // Login as the user; permissions should now match roleB, not roleA.
        var userClient = Factory.CreateClient();
        var login = await userClient.PostAsJsonAsync("/api/auth/login", new { phoneNumber = phone, password });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        userClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", (await ReadJsonAsync(login)).GetProperty("accessToken").GetString());

        var perms = (await ReadJsonAsync(await userClient.GetAsync("/api/users/me/permissions")))
            .GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).ToHashSet();
        perms.Should().BeEquivalentTo(permsB);
        perms.Should().NotContain("users.view"); // belonged to roleA, must not leak

        // Cleanup
        await saClient.DeleteAsync($"/api/users/{userId}");
        await saClient.PutAsJsonAsync($"/api/users/{userId}", new { role = "USER" });
        await saClient.DeleteAsync($"/api/admin/rbac/roles/{roleAId}");
        await saClient.DeleteAsync($"/api/admin/rbac/roles/{roleBId}");
    }
}
