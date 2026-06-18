using System.Net;

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
}
