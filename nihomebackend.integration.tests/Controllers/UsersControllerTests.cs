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
}
