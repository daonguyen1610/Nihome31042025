using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Net;
using System.Text;
using NihomeBackend.Data;
using NihomeBackend.Services;
using NihomeBackend.Services.GoogleDrive;

namespace nihomebackend.tests.Services;

public sealed class GoogleDriveOAuthServiceTests
{
    [Fact]
    public async Task CredentialStore_EncryptsDatabaseTokenAndReadsItBack()
    {
        await using var db = CreateDb();
        var store = new GoogleDriveCredentialStore(
            db,
            new EphemeralDataProtectionProvider(),
            new GoogleDriveOptions());

        await store.SaveAsync("refresh-token-value", "admin@example.com", 7);

        var persisted = await db.GoogleDriveCredentials.AsNoTracking().SingleAsync();
        Assert.NotEqual("refresh-token-value", persisted.ProtectedRefreshToken);
        Assert.Equal("refresh-token-value", await store.GetRefreshTokenAsync());
        Assert.Equal("admin@example.com", persisted.AccountEmail);
        Assert.Equal(7, persisted.ConnectedByUserId);
    }

    [Fact]
    public async Task CredentialStore_UsesConfiguredTokenWhenDatabaseIsEmpty()
    {
        await using var db = CreateDb();
        var store = new GoogleDriveCredentialStore(
            db,
            new EphemeralDataProtectionProvider(),
            new GoogleDriveOptions { RefreshToken = "configured-fallback" });

        Assert.Equal("configured-fallback", await store.GetRefreshTokenAsync());
        var metadata = await store.GetMetadataAsync();
        Assert.False(metadata.HasDatabaseCredential);
        Assert.True(metadata.HasConfiguredFallback);
    }

    [Fact]
    public void CreateAuthorizationRequest_UsesOfflineConsentStateAndPkce()
    {
        var service = CreateOAuthService(new GoogleDriveOptions
        {
            Enabled = true,
            ClientId = "client-id",
            ClientSecret = "client-secret",
            OAuthRedirectUri = "https://example.com/api/site-settings/google-drive/oauth/callback",
        });

        var response = service.CreateAuthorizationRequest(12);

        Assert.StartsWith("https://accounts.google.com/o/oauth2/v2/auth?", response.AuthorizationUrl);
        Assert.Contains("access_type=offline", response.AuthorizationUrl);
        Assert.Contains("prompt=consent", response.AuthorizationUrl);
        Assert.Contains("code_challenge_method=S256", response.AuthorizationUrl);
        Assert.Contains("code_challenge=", response.AuthorizationUrl);
        Assert.Contains("state=", response.AuthorizationUrl);
        Assert.DoesNotContain("client-secret", response.AuthorizationUrl);
    }

    [Fact]
    public void IsInvalidGrant_RecognizesNestedOAuthFailure()
    {
        var exception = new InvalidOperationException(
            "outer",
            new Exception("Error: invalid_grant; token expired or revoked"));

        Assert.True(GoogleDriveAuthenticationErrors.IsInvalidGrant(exception));
        Assert.False(GoogleDriveAuthenticationErrors.IsInvalidGrant(new Exception("network timeout")));
    }

    [Fact]
    public async Task CompleteAsync_WhenInitiatingUserLostPermission_RejectsBeforeTokenExchange()
    {
        var options = new GoogleDriveOptions
        {
            Enabled = true,
            ClientId = "client-id",
            ClientSecret = "client-secret",
            OAuthRedirectUri = "https://example.com/api/site-settings/google-drive/oauth/callback",
        };
        var service = CreateOAuthService(options);
        var start = service.CreateAuthorizationRequest(12);
        var state = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(
            new Uri(start.AuthorizationUrl).Query)["state"].ToString();

        var result = await service.CompleteAsync("authorization-code", state, null);

        Assert.Equal(GoogleDriveOAuthResult.AuthorizationExpired, result);
    }

    [Fact]
    public async Task CompleteAsync_ProviderErrorWithInvalidState_IsRejectedAsInvalidState()
    {
        var service = CreateOAuthService(new GoogleDriveOptions
        {
            Enabled = true,
            ClientId = "client-id",
            ClientSecret = "client-secret",
            OAuthRedirectUri = "https://example.com/api/site-settings/google-drive/oauth/callback",
        });

        var result = await service.CompleteAsync(null, "forged-state", "access_denied");

        Assert.Equal(GoogleDriveOAuthResult.InvalidState, result);
    }

    [Fact]
    public async Task CompleteAsync_ValidCode_PersistsRefreshTokenForInitiatingUser()
    {
        var options = new GoogleDriveOptions
        {
            Enabled = true,
            ClientId = "client-id",
            ClientSecret = "client-secret",
            OAuthRedirectUri = "https://example.com/api/site-settings/google-drive/oauth/callback",
            RootFolderId = "root-folder",
        };
        var dataProtection = new EphemeralDataProtectionProvider();
        var credentialStore = new Mock<IGoogleDriveCredentialStore>();
        var permissions = new Mock<IPermissionService>();
        permissions.Setup(service => service.HasAsync(12, "system.settings.manage", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var client = new HttpClient(new OAuthHttpHandler());
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(factory => factory.CreateClient(It.IsAny<string>())).Returns(client);
        var service = new GoogleDriveOAuthService(
            options,
            dataProtection,
            credentialStore.Object,
            httpClientFactory.Object,
            Mock.Of<IGoogleDriveAdapter>(),
            permissions.Object,
            NullLogger<GoogleDriveOAuthService>.Instance);
        var start = service.CreateAuthorizationRequest(12);
        var state = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(
            new Uri(start.AuthorizationUrl).Query)["state"].ToString();

        var result = await service.CompleteAsync("authorization-code", state, null);

        Assert.Equal(GoogleDriveOAuthResult.Success, result);
        credentialStore.Verify(store => store.SaveAsync(
            "new-refresh-token", "owner@example.com", 12, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CompleteAsync_CandidateCannotManageRoot_DoesNotReplaceCredential()
    {
        var options = new GoogleDriveOptions
        {
            Enabled = true,
            ClientId = "client-id",
            ClientSecret = "client-secret",
            OAuthRedirectUri = "https://example.com/api/site-settings/google-drive/oauth/callback",
            RootFolderId = "root-folder",
        };
        var dataProtection = new EphemeralDataProtectionProvider();
        var credentialStore = new Mock<IGoogleDriveCredentialStore>();
        var permissions = new Mock<IPermissionService>();
        permissions.Setup(service => service.HasAsync(12, "system.settings.manage", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(new OAuthHttpHandler(canManageRoot: false)));
        var service = new GoogleDriveOAuthService(
            options,
            dataProtection,
            credentialStore.Object,
            httpClientFactory.Object,
            Mock.Of<IGoogleDriveAdapter>(),
            permissions.Object,
            NullLogger<GoogleDriveOAuthService>.Instance);
        var start = service.CreateAuthorizationRequest(12);
        var state = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(
            new Uri(start.AuthorizationUrl).Query)["state"].ToString();

        var result = await service.CompleteAsync("authorization-code", state, null);

        Assert.Equal(GoogleDriveOAuthResult.RootValidationFailed, result);
        credentialStore.Verify(store => store.SaveAsync(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetStatusAsync_ReconnectException_ReturnsReconnectRequiredWithoutSecrets()
    {
        var options = new GoogleDriveOptions
        {
            Enabled = true,
            ClientId = "client-id",
            ClientSecret = "client-secret",
            OAuthRedirectUri = "https://example.com/api/site-settings/google-drive/oauth/callback",
        };
        var credentialStore = new Mock<IGoogleDriveCredentialStore>();
        credentialStore.Setup(store => store.GetMetadataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleDriveCredentialMetadata(true, false, "owner@example.com", DateTime.UtcNow));
        var drive = new Mock<IGoogleDriveAdapter>();
        drive.Setup(adapter => adapter.CheckConnectionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GoogleDriveReconnectRequiredException("revoked"));
        var service = new GoogleDriveOAuthService(
            options,
            new EphemeralDataProtectionProvider(),
            credentialStore.Object,
            Mock.Of<IHttpClientFactory>(),
            drive.Object,
            Mock.Of<IPermissionService>(),
            NullLogger<GoogleDriveOAuthService>.Instance);

        var status = await service.GetStatusAsync();

        Assert.Equal("ReconnectRequired", status.Status);
        Assert.Equal("owner@example.com", status.AccountEmail);
        Assert.DoesNotContain("client-secret", status.Error ?? string.Empty);
    }

    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    private static GoogleDriveOAuthService CreateOAuthService(GoogleDriveOptions options) => new(
        options,
        new EphemeralDataProtectionProvider(),
        Mock.Of<IGoogleDriveCredentialStore>(),
        Mock.Of<IHttpClientFactory>(),
        Mock.Of<IGoogleDriveAdapter>(),
        Mock.Of<IPermissionService>(),
        NullLogger<GoogleDriveOAuthService>.Instance);

    private sealed class OAuthHttpHandler(bool canManageRoot = true) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var isTokenRequest = request.RequestUri?.AbsolutePath.EndsWith("/token", StringComparison.Ordinal) == true;
            var isRootRequest = request.RequestUri?.AbsolutePath.Contains("/drive/v3/files/", StringComparison.Ordinal) == true;
            var status = isRootRequest && !canManageRoot ? HttpStatusCode.Forbidden : HttpStatusCode.OK;
            var json = isTokenRequest
                ? "{\"access_token\":\"access-token\",\"refresh_token\":\"new-refresh-token\"}"
                : isRootRequest
                    ? "{\"mimeType\":\"application/vnd.google-apps.folder\",\"trashed\":false,\"capabilities\":{\"canAddChildren\":true}}"
                    : "{\"user\":{\"emailAddress\":\"owner@example.com\"}}";
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }
    }
}