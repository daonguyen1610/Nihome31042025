using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

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

    [Fact]
    public async Task GetGoogleDriveStatus_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/site-settings/google-drive/status"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task StartGoogleDriveOAuth_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.PostAsync("/api/site-settings/google-drive/oauth/start", null))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GoogleDriveAdministration_AsSalesRole_ReturnsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "SALE"));

        (await Client.GetAsync("/api/site-settings/google-drive/status"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Client.PostAsync("/api/site-settings/google-drive/oauth/start", null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GoogleDriveCallback_WithInvalidState_RedirectsToSafeFrontendResult()
    {
        using var noRedirectClient = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await noRedirectClient.GetAsync(
            "/api/site-settings/google-drive/oauth/callback?code=test&state=invalid");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.OriginalString.Should().Be(
            "/admin/settings?tab=drive&driveOAuth=invalid_state");
    }

    [Fact]
    public async Task GetGoogleDriveStatus_AsAdmin_ReturnsDisabledWithoutSecrets()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);

        var response = await Client.GetAsync("/api/site-settings/google-drive/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(response);
        body.GetProperty("status").GetString().Should().Be("Disabled");
        body.TryGetProperty("refreshToken", out _).Should().BeFalse();
    }
}
