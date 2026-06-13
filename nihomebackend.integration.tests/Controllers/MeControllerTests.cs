using System.Net;

namespace NihomeBackend.IntegrationTests.Controllers;

public class MeControllerTests : IntegrationTestBase
{
    public MeControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetMe_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/users/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMe_AsAdmin_ReturnsOk()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        var res = await Client.GetAsync("/api/users/me");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await ReadJsonAsync(res);
        body.GetProperty("phoneNumber").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task UpdateMe_AsAdmin_ReturnsOk()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        var res = await Client.PutAsJsonAsync("/api/users/me", new { fullName = "Admin Updated", email = "admin-updated@example.com" });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_TooShort_ReturnsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        var res = await Client.PostAsJsonAsync("/api/users/me/change-password", new { currentPassword = "Admin@123", newPassword = "abc" });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetDocuments_AsAdmin_ReturnsOk()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        (await Client.GetAsync("/api/users/me/documents")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
