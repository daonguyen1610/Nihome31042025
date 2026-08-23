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
        var beforeResponse = await Client.GetAsync("/api/site-settings/otp-settings");
        beforeResponse.EnsureSuccessStatusCode();
        var before = await ReadJsonAsync(beforeResponse);
        var registration = before.GetProperty("enableOtpForRegistration").GetBoolean();
        var forgotPassword = before.GetProperty("enableOtpForForgotPassword").GetBoolean();

        var res = await Client.PutAsJsonAsync("/api/site-settings/otp-settings", new
        {
            enableOtpForRegistration = !registration,
            enableOtpForForgotPassword = !forgotPassword,
        });

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var afterResponse = await Client.GetAsync("/api/site-settings/otp-settings");
        afterResponse.EnsureSuccessStatusCode();
        var after = await ReadJsonAsync(afterResponse);
        after.GetProperty("enableOtpForRegistration").GetBoolean().Should().Be(registration);
        after.GetProperty("enableOtpForForgotPassword").GetBoolean().Should().Be(forgotPassword);
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
