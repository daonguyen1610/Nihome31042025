using System.Net;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// Coverage for the notification-template administration endpoints
/// added in NIH-381. The user-facing endpoints (bell, mark-read, etc.)
/// remain covered by <see cref="NotificationsControllerTests"/> — this
/// file focuses on the admin surface + RBAC gating.
/// </summary>
public class NotificationTemplatesControllerTests : IntegrationTestBase
{
    public NotificationTemplatesControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task ListTemplates_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/notifications/templates")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListTemplates_AsAdmin_ReturnsSeededTemplates()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);

        var res = await Client.GetAsync("/api/notifications/templates");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await ReadJsonAsync(res);
        var codes = body.EnumerateArray()
            .Select(e => e.GetProperty("code").GetString())
            .ToList();

        codes.Should().Contain(new[]
        {
            "lead.assigned",
            "quote.submitted-for-approval",
            "quote.approved",
            "quote.rejected",
            "contract.activated",
            "design.revision.created",
            "permit.expiring-soon",
        });
    }

    [Fact]
    public async Task ListTemplates_AsBusinessRole_IsForbidden()
    {
        // Business roles like SALE only carry system.notifications view/none;
        // the admin surface requires system.notifications.manage.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));

        (await Client.GetAsync("/api/notifications/templates")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetTemplate_ReturnsShapeWithChannelAndKeys()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);

        var res = await Client.GetAsync("/api/notifications/templates/quote.approved");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await ReadJsonAsync(res);
        body.GetProperty("code").GetString().Should().Be("quote.approved");
        body.GetProperty("titleKey").GetString().Should().Be("notification.quote.approved.title");
        body.GetProperty("bodyKey").GetString().Should().Be("notification.quote.approved.body");
        body.GetProperty("channel").GetString().Should().Be("InApp");
        body.GetProperty("isActive").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task GetTemplate_UnknownCode_Returns404()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);

        (await Client.GetAsync("/api/notifications/templates/does.not.exist")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateTemplate_TogglesChannelAndActive()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);

        var res = await Client.PutAsJsonAsync(
            "/api/notifications/templates/quote.approved",
            new { channel = "Both", isActive = false });
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await ReadJsonAsync(res);
        body.GetProperty("channel").GetString().Should().Be("Both");
        body.GetProperty("isActive").GetBoolean().Should().BeFalse();

        // Restore for other tests using the same shared factory.
        (await Client.PutAsJsonAsync(
            "/api/notifications/templates/quote.approved",
            new { channel = "InApp", isActive = true })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task UpdateTemplate_WithoutManagePermission_IsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));

        var res = await Client.PutAsJsonAsync(
            "/api/notifications/templates/quote.approved",
            new { channel = "Both", isActive = false });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
