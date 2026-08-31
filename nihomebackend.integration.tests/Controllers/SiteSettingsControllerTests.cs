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
    public async Task GoogleDriveConfiguration_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/site-settings/google-drive/configuration"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Client.PutAsJsonAsync("/api/site-settings/google-drive/configuration", new { }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task StartGoogleDriveOAuth_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.PostAsync("/api/site-settings/google-drive/oauth/start", null))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Client.PostAsync("/api/site-settings/google-drive/oauth/disconnect", null))
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
        (await Client.GetAsync("/api/site-settings/google-drive/configuration"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Client.PutAsJsonAsync("/api/site-settings/google-drive/configuration", new { }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Client.PostAsync("/api/site-settings/google-drive/oauth/start", null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Client.PostAsync("/api/site-settings/google-drive/oauth/disconnect", null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GoogleDriveConfiguration_AsAdmin_PersistsWithoutReturningSecret()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        var currentResponse = await Client.GetAsync("/api/site-settings/google-drive/configuration");
        currentResponse.EnsureSuccessStatusCode();
        var current = await ReadJsonAsync(currentResponse);
        const string secret = "integration-client-secret";

        var updateResponse = await Client.PutAsJsonAsync(
            "/api/site-settings/google-drive/configuration",
            new
            {
                enabled = false,
                clientId = "123.apps.googleusercontent.com",
                clientSecret = secret,
                oAuthRedirectUri = "https://example.com/api/site-settings/google-drive/oauth/callback",
                frontendReturnUrl = "/admin/settings?tab=drive",
                rootFolderId = "1234567890root",
                instanceId = "nicon-integration",
                applicationName = "Nicon Google Drive Integration",
                folders = new
                {
                    surveyMedia = "01_Khao_sat",
                    crmPreDesign = "01_CRM_PreDesign",
                    designConcept = "02_Thiet_ke/01_So_bo_Concept",
                    designBasic = "02_Thiet_ke/02_Co_so",
                    designShopDrawing = "02_Thiet_ke/03_Chi_tiet_ShopDrawing",
                    legalPermits = "03_Xin_phep_Phap_ly",
                    constructionAcceptance = "04_Thi_cong_Nghiem_thu",
                    procurement = "05_Cung_ung_Vat_tu",
                    financeContracts = "06_Tai_chinh_Hop_dong",
                },
                supportsAllDrives = true,
                pollIntervalSeconds = 15,
                rowVersion = current.GetProperty("rowVersion").GetString(),
            });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var raw = await updateResponse.Content.ReadAsStringAsync();
        raw.Should().NotContain(secret);
        var updated = await ReadJsonAsync(updateResponse);
        updated.TryGetProperty("clientSecret", out _).Should().BeFalse();
        updated.GetProperty("hasClientSecret").GetBoolean().Should().BeTrue();
        updated.GetProperty("clientId").GetString().Should().Be("123.apps.googleusercontent.com");
    }

    [Fact]
    public async Task GoogleDriveConfiguration_InvalidEnabledPayload_ReturnsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);

        var response = await Client.PutAsJsonAsync(
            "/api/site-settings/google-drive/configuration",
            new
            {
                enabled = true,
                clientId = "invalid",
                clientSecret = "valid-secret-value",
                oAuthRedirectUri = "https://example.com/wrong",
                frontendReturnUrl = "/admin/settings?tab=drive",
                rootFolderId = "invalid",
                instanceId = "nicon-integration",
                applicationName = "Nicon Google Drive Integration",
                folders = new { },
                supportsAllDrives = true,
                pollIntervalSeconds = 15,
                rowVersion = "",
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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

    [Fact]
    public async Task DisconnectGoogleDrive_AsAdmin_ClearsLocalConnectionWithoutSecrets()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);

        var response = await Client.PostAsync("/api/site-settings/google-drive/oauth/disconnect", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var raw = await response.Content.ReadAsStringAsync();
        raw.ToLowerInvariant().Should().NotContain("refreshtoken");
        var body = await ReadJsonAsync(response);
        body.GetProperty("hadStoredCredential").GetBoolean().Should().BeFalse();
        body.GetProperty("providerRevoked").GetBoolean().Should().BeFalse();
    }
}
