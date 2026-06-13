using System.Net;

namespace NihomeBackend.IntegrationTests.Controllers;

public class SiteSettingsControllerTests : IntegrationTestBase
{
    public SiteSettingsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetOtpSettings_IsPublic_ReturnsOk()
    {
        (await Client.GetAsync("/api/site-settings/otp-settings")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetMapEmbed_IsPublic_ReturnsOk()
    {
        (await Client.GetAsync("/api/site-settings/map-embed")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateOtpSettings_WithoutAuth_ReturnsUnauthorized()
    {
        var res = await Client.PutAsJsonAsync("/api/site-settings/otp-settings", new
        {
            enableOtpForRegistration = true,
            enableOtpForForgotPassword = true,
        });
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateOtpSettings_AsAdmin_ReturnsOk()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        var res = await Client.PutAsJsonAsync("/api/site-settings/otp-settings", new
        {
            enableOtpForRegistration = false,
            enableOtpForForgotPassword = false,
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetEmailTemplates_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/site-settings/email-templates")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
