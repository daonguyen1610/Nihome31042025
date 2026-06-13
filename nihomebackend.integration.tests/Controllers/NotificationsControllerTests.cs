using System.Net;

namespace NihomeBackend.IntegrationTests.Controllers;

public class NotificationsControllerTests : IntegrationTestBase
{
    public NotificationsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task List_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/notifications")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_AsAdmin_ReturnsOk()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        (await Client.GetAsync("/api/notifications")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnreadCount_AsAdmin_ReturnsOk()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        var res = await Client.GetAsync("/api/notifications/unread-count");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("count").GetInt32().Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task MarkAllRead_AsAdmin_ReturnsOk()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        var res = await Client.PostAsync("/api/notifications/mark-all-read", content: null);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_Unknown_AsAdmin_ReturnsNotFound()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        (await Client.DeleteAsync("/api/notifications/999999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
