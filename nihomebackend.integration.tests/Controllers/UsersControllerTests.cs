using System.Net;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.Rbac;

namespace NihomeBackend.IntegrationTests.Controllers;

public class UsersControllerTests : IntegrationTestBase
{
    public UsersControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task List_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/users")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_AsAdmin_ReturnsOk()
    {
        // ADMIN has users.view (system role: full catalog minus
        // users.manage + system.audit.manage) so listing must succeed; the
        // ability to mutate users is covered by Create_AsAdmin_ReturnsForbidden.
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        (await Client.GetAsync("/api/users")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_AsAdmin_ReturnsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        var resp = await Client.PostAsJsonAsync("/api/users", new
        {
            phoneNumber = "0900000099",
            fullName = "Blocked",
            password = "P@ssword1",
            role = "ADMIN",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_AsSuperAdmin_ReturnsOk()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        (await Client.GetAsync("/api/users")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetRoles_AsSuperAdmin_ReturnsOk()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        (await Client.GetAsync("/api/users/roles")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task FullRoundTrip_Create_Update_Delete_AsSuperAdmin()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);

        var phone = "0987" + new Random().Next(100000, 999999).ToString();
        var created = await Client.PostAsJsonAsync("/api/users", new
        {
            phoneNumber = phone,
            fullName = "Test User",
            email = "tu@example.com",
            password = "P@ssword1",
            role = "ADMIN",
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await ReadJsonAsync(created)).GetProperty("id").GetInt32();

        var updated = await Client.PutAsJsonAsync($"/api/users/{id}", new
        {
            fullName = "Test User v2",
            role = "ADMIN",
            isActive = false,
        });
        updated.StatusCode.Should().Be(HttpStatusCode.OK);

        (await Client.DeleteAsync($"/api/users/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GlobalExceptionHandler_ReturnsProblemDetailsWithTraceId_OnDuplicatePhone()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);

        var phone = "0987" + new Random().Next(100000, 999999).ToString();
        var first = await Client.PostAsJsonAsync("/api/users", new
        {
            phoneNumber = phone,
            fullName = "First User",
            email = $"first-{Guid.NewGuid():N}@example.com",
            password = "P@ssword1",
            role = "USER",
        });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var duplicate = await Client.PostAsJsonAsync("/api/users", new
        {
            phoneNumber = phone,
            fullName = "Dup User",
            email = $"dup-{Guid.NewGuid():N}@example.com",
            password = "P@ssword1",
            role = "USER",
        });

        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        duplicate.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var body = await ReadJsonAsync(duplicate);
        // RFC-7807 ProblemDetails shape: status, detail (human-readable), and
        // a traceId extension that matches the server's HttpContext.TraceIdentifier.
        body.GetProperty("status").GetInt32().Should().Be(409);
        body.GetProperty("detail").GetString().Should().Contain("Phone");
        body.GetProperty("traceId").GetString().Should().NotBeNullOrEmpty();
        // The legacy "message" mirror was removed; clients must read "detail".
        body.TryGetProperty("message", out _).Should().BeFalse();
    }

    // -------------------- Custom RBAC role assignment --------------------

    [Fact]
    public async Task Create_WithCustomBusinessRole_AssignsRoleEntityId()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);

        var customCode = $"BIZ_{Guid.NewGuid():N}".Substring(0, 12).ToUpperInvariant();
        await SeedCustomRole(customCode, "Custom Business Role");

        var phone = "0987" + new Random().Next(100000, 999999).ToString();
        var created = await Client.PostAsJsonAsync("/api/users", new
        {
            phoneNumber = phone,
            fullName = "Custom Role User",
            email = $"custom-{Guid.NewGuid():N}@example.com",
            password = "P@ssword1",
            role = customCode,
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await ReadJsonAsync(created);
        var newId = body.GetProperty("id").GetInt32();
        body.GetProperty("role").GetString().Should().Be(customCode);
        body.GetProperty("roleName").GetString().Should().Be("Custom Business Role");
        body.GetProperty("roleId").GetInt32().Should().BeGreaterThan(0);

        // Verify persistence: RoleEntityId is set; legacy enum mirrors as USER
        // (custom roles never auto-elevate via the enum-based gates).
        await WithDbAsync(async db =>
        {
            var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == newId);
            user.RoleEntityId.Should().NotBeNull();
            user.Role.Should().Be(UserRole.USER);
        });

        // Cleanup so fixture isn't polluted for later tests.
        (await Client.DeleteAsync($"/api/users/{newId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Update_FromSystemRoleToCustomBusinessRole_PersistsBothSides()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);

        var customCode = $"BIZ_{Guid.NewGuid():N}".Substring(0, 12).ToUpperInvariant();
        await SeedCustomRole(customCode, "Promoted Role");

        var phone = "0987" + new Random().Next(100000, 999999).ToString();
        var created = await Client.PostAsJsonAsync("/api/users", new
        {
            phoneNumber = phone,
            fullName = "Will Be Promoted",
            email = $"promote-{Guid.NewGuid():N}@example.com",
            password = "P@ssword1",
            role = "USER",
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await ReadJsonAsync(created)).GetProperty("id").GetInt32();

        var updated = await Client.PutAsJsonAsync($"/api/users/{id}", new
        {
            role = customCode,
        });
        updated.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await ReadJsonAsync(updated);
        body.GetProperty("role").GetString().Should().Be(customCode);
        body.GetProperty("roleName").GetString().Should().Be("Promoted Role");

        (await Client.DeleteAsync($"/api/users/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Create_WithUnknownRoleCode_ReturnsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);

        var phone = "0987" + new Random().Next(100000, 999999).ToString();
        var resp = await Client.PostAsJsonAsync("/api/users", new
        {
            phoneNumber = phone,
            fullName = "Bad Role",
            email = $"bad-{Guid.NewGuid():N}@example.com",
            password = "P@ssword1",
            role = "TOTALLY_UNKNOWN_ROLE_CODE",
        });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ReadJsonAsync(resp);
        body.GetProperty("detail").GetString()!.ToLowerInvariant().Should().Contain("role");
    }

    private async Task SeedCustomRole(string code, string name)
    {
        await WithDbAsync(async db =>
        {
            if (!await db.Roles.AnyAsync(r => r.Code == code))
            {
                db.Roles.Add(new Role
                {
                    Code = code,
                    Name = name,
                    IsSystem = false,
                    IsActive = true,
                    InitialPermissionsSeeded = true,
                });
                await db.SaveChangesAsync();
            }
        });
    }
}
