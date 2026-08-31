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
    public async Task SettingsStore_EncryptsSecretsAndNeverReturnsThemToAdmin()
    {
        await using var db = CreateDb();
        var store = new GoogleDriveSettingsStore(db, new EphemeralDataProtectionProvider());

        await store.UpdateAsync(ValidConfiguration("client-secret-value"), 7);
        await store.SaveRefreshTokenAsync("refresh-token-value", "admin@example.com", 7, string.Empty);

        var persisted = await db.GoogleDriveCredentials.AsNoTracking().SingleAsync();
        Assert.Equal(1, persisted.Id);
        Assert.NotEqual("refresh-token-value", persisted.ProtectedRefreshToken);
        Assert.NotEqual("client-secret-value", persisted.ProtectedClientSecret);
        var runtime = await store.GetRuntimeAsync();
        Assert.Equal("refresh-token-value", runtime.RefreshToken);
        Assert.Equal("client-secret-value", runtime.ClientSecret);
        var admin = await store.GetAdminAsync();
        Assert.True(admin.HasClientSecret);
        Assert.True(admin.HasRefreshToken);
        Assert.Equal("admin@example.com", persisted.AccountEmail);
        Assert.Equal(7, persisted.ConnectedByUserId);
    }

    [Fact]
    public async Task SettingsStore_WhenDatabaseIsEmpty_ReturnsDisabledDefaults()
    {
        await using var db = CreateDb();
        var store = new GoogleDriveSettingsStore(db, new EphemeralDataProtectionProvider());

        var runtime = await store.GetRuntimeAsync();
        var admin = await store.GetAdminAsync();

        Assert.False(runtime.Enabled);
        Assert.Empty(runtime.ClientId);
        Assert.Empty(runtime.RefreshToken);
        Assert.False(admin.HasClientSecret);
        Assert.False(admin.HasRefreshToken);
    }

    [Fact]
    public async Task SettingsStore_BlankSecretOnUpdate_PreservesEncryptedSecret()
    {
        await using var db = CreateDb();
        var store = new GoogleDriveSettingsStore(db, new EphemeralDataProtectionProvider());
        var first = await store.UpdateAsync(ValidConfiguration("client-secret-value"), 7);
        var protectedSecret = (await db.GoogleDriveCredentials.AsNoTracking().SingleAsync()).ProtectedClientSecret;
        var update = ValidConfiguration();
        update.RowVersion = first.RowVersion;
        update.ApplicationName = "Updated Nicon Drive";

        var result = await store.UpdateAsync(update, 8);

        Assert.True(result.HasClientSecret);
        Assert.Equal(protectedSecret, (await db.GoogleDriveCredentials.AsNoTracking().SingleAsync()).ProtectedClientSecret);
        Assert.Equal("client-secret-value", (await store.GetRuntimeAsync()).ClientSecret);
    }

    [Fact]
    public async Task SettingsStore_ChangingClientIdWithoutNewSecret_IsRejected()
    {
        await using var db = CreateDb();
        var store = new GoogleDriveSettingsStore(db, new EphemeralDataProtectionProvider());
        var current = await store.UpdateAsync(ValidConfiguration("client-secret-value"), 7);
        var update = ValidConfiguration();
        update.RowVersion = current.RowVersion;
        update.ClientId = "456.apps.googleusercontent.com";

        await Assert.ThrowsAsync<GoogleDriveSettingsValidationException>(() =>
            store.UpdateAsync(update, 8));

        var runtime = await store.GetRuntimeAsync();
        Assert.Equal("123.apps.googleusercontent.com", runtime.ClientId);
        Assert.Equal("client-secret-value", runtime.ClientSecret);
    }

    [Fact]
    public async Task SettingsStore_ChangingOAuthIdentity_RemovesExistingRefreshToken()
    {
        await using var db = CreateDb();
        var store = new GoogleDriveSettingsStore(db, new EphemeralDataProtectionProvider());
        var first = await store.UpdateAsync(ValidConfiguration("client-secret-value"), 7);
        await store.SaveRefreshTokenAsync("refresh-token-value", "owner@example.com", 7, string.Empty);
        var current = await store.GetAdminAsync();
        var update = ValidConfiguration("replacement-secret");
        update.RowVersion = current.RowVersion;
        update.ClientId = "456.apps.googleusercontent.com";

        var result = await store.UpdateAsync(update, 8);

        Assert.False(result.HasRefreshToken);
        Assert.Null(result.AccountEmail);
        Assert.Empty((await store.GetRuntimeAsync()).RefreshToken);
    }

    [Fact]
    public async Task SettingsStore_ClearRefreshToken_RemovesOnlyConnectionMetadata()
    {
        await using var db = CreateDb();
        var store = new GoogleDriveSettingsStore(db, new EphemeralDataProtectionProvider());
        await store.UpdateAsync(ValidConfiguration("client-secret-value"), 7);
        await store.SaveRefreshTokenAsync("refresh-token-value", "owner@example.com", 7, string.Empty);
        var current = await store.GetAdminAsync();

        await store.ClearRefreshTokenAsync(8, current.RowVersion);

        var result = await store.GetAdminAsync();
        Assert.True(result.HasClientSecret);
        Assert.False(result.HasRefreshToken);
        Assert.Null(result.AccountEmail);
        Assert.Null(result.ConnectedAt);
        Assert.Equal("123.apps.googleusercontent.com", result.ClientId);
    }

    [Fact]
    public async Task SettingsStore_ClearRefreshToken_WithStaleVersion_PreservesNewConnection()
    {
        await using var db = CreateDb();
        var store = new GoogleDriveSettingsStore(db, new EphemeralDataProtectionProvider());
        await store.UpdateAsync(ValidConfiguration("client-secret-value"), 7);
        await store.SaveRefreshTokenAsync("new-refresh-token", "new-owner@example.com", 7, string.Empty);

        await Assert.ThrowsAsync<GoogleDriveSettingsConcurrencyException>(() =>
            store.ClearRefreshTokenAsync(8, Convert.ToBase64String([1, 2, 3])));

        Assert.Equal("new-refresh-token", (await store.GetRuntimeAsync()).RefreshToken);
        Assert.Equal("new-owner@example.com", (await store.GetAdminAsync()).AccountEmail);
    }

    [Fact]
    public async Task SettingsStore_FirstClientAssignment_RemovesMigratedRefreshToken()
    {
        await using var db = CreateDb();
        var protection = new EphemeralDataProtectionProvider();
        db.GoogleDriveCredentials.Add(new NihomeBackend.Models.GoogleDriveCredential
        {
            Id = 1,
            ProtectedRefreshToken = protection.CreateProtector("Nicon.GoogleDrive.RefreshToken.v1")
                .Protect("legacy-refresh-token"),
        });
        await db.SaveChangesAsync();
        var store = new GoogleDriveSettingsStore(db, protection);

        var result = await store.UpdateAsync(ValidConfiguration("client-secret-value"), 7);

        Assert.False(result.HasRefreshToken);
        Assert.Empty((await store.GetRuntimeAsync()).RefreshToken);
    }

    [Fact]
    public async Task SettingsStore_ChangingFolderTopology_InvalidatesExistingBindings()
    {
        await using var db = CreateDb();
        var store = new GoogleDriveSettingsStore(db, new EphemeralDataProtectionProvider());
        var current = await store.UpdateAsync(ValidConfiguration("client-secret-value"), 7);
        db.ProjectDriveFolders.Add(new NihomeBackend.Models.ProjectDriveFolder
        {
            OperationalProjectId = 42,
            Category = NihomeBackend.Models.ProjectDocumentCategory.DesignConcept,
            DriveFolderId = "old-folder",
        });
        await db.SaveChangesAsync();
        var update = ValidConfiguration();
        update.RowVersion = current.RowVersion;
        update.RootFolderId = "0987654321root";

        await store.UpdateAsync(update, 8);

        Assert.Empty(db.ProjectDriveFolders);
    }

    [Fact]
    public async Task SettingsStore_FirstTopologyAssignment_InvalidatesMigratedBindings()
    {
        await using var db = CreateDb();
        db.GoogleDriveCredentials.Add(new NihomeBackend.Models.GoogleDriveCredential { Id = 1 });
        db.ProjectDriveFolders.Add(new NihomeBackend.Models.ProjectDriveFolder
        {
            OperationalProjectId = 42,
            Category = NihomeBackend.Models.ProjectDocumentCategory.DesignConcept,
            DriveFolderId = "legacy-folder",
        });
        await db.SaveChangesAsync();
        var store = new GoogleDriveSettingsStore(db, new EphemeralDataProtectionProvider());

        await store.UpdateAsync(ValidConfiguration("client-secret-value"), 7);

        Assert.Empty(db.ProjectDriveFolders);
    }

    [Theory]
    [InlineData("invalid-client", "https://example.com/api/site-settings/google-drive/oauth/callback", "1234567890root", 15)]
    [InlineData("123.apps.googleusercontent.com", "https://example.com/wrong", "1234567890root", 15)]
    [InlineData("123.apps.googleusercontent.com", "https://example.com/api/site-settings/google-drive/oauth/callback", "bad id", 15)]
    [InlineData("123.apps.googleusercontent.com", "https://example.com/api/site-settings/google-drive/oauth/callback", "1234567890root", 4)]
    public async Task SettingsStore_InvalidConfiguration_IsRejected(
        string clientId,
        string redirectUri,
        string rootFolderId,
        int pollIntervalSeconds)
    {
        await using var db = CreateDb();
        var store = new GoogleDriveSettingsStore(db, new EphemeralDataProtectionProvider());
        var request = ValidConfiguration("client-secret-value");
        request.ClientId = clientId;
        request.OAuthRedirectUri = redirectUri;
        request.RootFolderId = rootFolderId;
        request.PollIntervalSeconds = pollIntervalSeconds;

        await Assert.ThrowsAsync<GoogleDriveSettingsValidationException>(() => store.UpdateAsync(request, 7));
        Assert.Empty(db.GoogleDriveCredentials);
    }

    [Fact]
    public async Task SettingsStore_NullString_IsRejectedAsValidationError()
    {
        await using var db = CreateDb();
        var store = new GoogleDriveSettingsStore(db, new EphemeralDataProtectionProvider());
        var request = ValidConfiguration("client-secret-value");
        request.ClientId = null!;

        await Assert.ThrowsAsync<GoogleDriveSettingsValidationException>(() => store.UpdateAsync(request, 7));
        Assert.Empty(db.GoogleDriveCredentials);
    }

    [Fact]
    public async Task SettingsStore_StaleRowVersion_IsRejectedWithoutChangingData()
    {
        await using var db = CreateDb();
        var store = new GoogleDriveSettingsStore(db, new EphemeralDataProtectionProvider());
        await store.UpdateAsync(ValidConfiguration("client-secret-value"), 7);
        var update = ValidConfiguration();
        update.RowVersion = Convert.ToBase64String([1, 2, 3]);
        update.ApplicationName = "Stale change";

        await Assert.ThrowsAsync<GoogleDriveSettingsConcurrencyException>(() => store.UpdateAsync(update, 8));
        Assert.Equal("Nicon Google Drive Integration", (await store.GetRuntimeAsync()).ApplicationName);
    }

    [Fact]
    public async Task CreateAuthorizationRequest_UsesOfflineConsentStateAndPkce()
    {
        var service = CreateOAuthService(new GoogleDriveOptions
        {
            Enabled = true,
            ClientId = "client-id",
            ClientSecret = "client-secret",
            OAuthRedirectUri = "https://example.com/api/site-settings/google-drive/oauth/callback",
        });

        var response = await service.CreateAuthorizationRequestAsync(12);

        Assert.StartsWith("https://accounts.google.com/o/oauth2/v2/auth?", response.AuthorizationUrl);
        Assert.Contains("access_type=offline", response.AuthorizationUrl);
        Assert.Contains("prompt=consent%20select_account", response.AuthorizationUrl);
        Assert.Contains("code_challenge_method=S256", response.AuthorizationUrl);
        Assert.Contains("code_challenge=", response.AuthorizationUrl);
        Assert.Contains("state=", response.AuthorizationUrl);
        Assert.DoesNotContain("client-secret", response.AuthorizationUrl);
    }

    [Fact]
    public async Task DisconnectAsync_RevokesProviderTokenAndClearsStoredCredential()
    {
        var operations = new List<string>();
        var options = new GoogleDriveOptions
        {
            RefreshToken = "stored-refresh-token",
            ConfigurationVersion = "current-version",
        };
        var settings = new Mock<IGoogleDriveSettingsStore>();
        settings.Setup(store => store.GetRuntimeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(options);
        settings.Setup(store => store.ClearRefreshTokenAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => operations.Add("clear"));
        var clients = new Mock<IHttpClientFactory>();
        clients.Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(new OAuthHttpHandler(onRevoke: () => operations.Add("revoke"))));
        var service = new GoogleDriveOAuthService(
            new EphemeralDataProtectionProvider(), settings.Object, clients.Object,
            Mock.Of<IGoogleDriveAdapter>(), Mock.Of<IPermissionService>(),
            NullLogger<GoogleDriveOAuthService>.Instance);

        var result = await service.DisconnectAsync(12);

        Assert.True(result.HadStoredCredential);
        Assert.True(result.ProviderRevoked);
        settings.Verify(store => store.ClearRefreshTokenAsync(
            12, "current-version", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(["clear", "revoke"], operations);
    }

    [Fact]
    public async Task DisconnectAsync_WhenCredentialChanged_DoesNotRevokeNewerCredential()
    {
        var options = new GoogleDriveOptions
        {
            RefreshToken = "stored-refresh-token",
            ConfigurationVersion = "stale-version",
        };
        var settings = new Mock<IGoogleDriveSettingsStore>();
        settings.Setup(store => store.GetRuntimeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(options);
        settings.Setup(store => store.ClearRefreshTokenAsync(
                12, "stale-version", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GoogleDriveSettingsConcurrencyException());
        var clients = new Mock<IHttpClientFactory>();
        var service = new GoogleDriveOAuthService(
            new EphemeralDataProtectionProvider(), settings.Object, clients.Object,
            Mock.Of<IGoogleDriveAdapter>(), Mock.Of<IPermissionService>(),
            NullLogger<GoogleDriveOAuthService>.Instance);

        await Assert.ThrowsAsync<GoogleDriveSettingsConcurrencyException>(() => service.DisconnectAsync(12));

        clients.Verify(factory => factory.CreateClient(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DisconnectAsync_WhenProviderRevocationFails_StillClearsStoredCredential()
    {
        var options = new GoogleDriveOptions
        {
            RefreshToken = "stored-refresh-token",
            ConfigurationVersion = "current-version",
        };
        var settings = new Mock<IGoogleDriveSettingsStore>();
        settings.Setup(store => store.GetRuntimeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(options);
        var clients = new Mock<IHttpClientFactory>();
        clients.Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(new OAuthHttpHandler(revokeSucceeds: false)));
        var service = new GoogleDriveOAuthService(
            new EphemeralDataProtectionProvider(), settings.Object, clients.Object,
            Mock.Of<IGoogleDriveAdapter>(), Mock.Of<IPermissionService>(),
            NullLogger<GoogleDriveOAuthService>.Instance);

        var result = await service.DisconnectAsync(12);

        Assert.True(result.HadStoredCredential);
        Assert.False(result.ProviderRevoked);
        settings.Verify(store => store.ClearRefreshTokenAsync(
            12, "current-version", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DisconnectAsync_WhenProviderRevocationTimesOut_ReturnsDisconnectedWarning()
    {
        var options = new GoogleDriveOptions
        {
            RefreshToken = "stored-refresh-token",
            ConfigurationVersion = "current-version",
        };
        var settings = new Mock<IGoogleDriveSettingsStore>();
        settings.Setup(store => store.GetRuntimeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(options);
        var clients = new Mock<IHttpClientFactory>();
        clients.Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(new OAuthHttpHandler(revokeTimesOut: true)));
        var service = new GoogleDriveOAuthService(
            new EphemeralDataProtectionProvider(), settings.Object, clients.Object,
            Mock.Of<IGoogleDriveAdapter>(), Mock.Of<IPermissionService>(),
            NullLogger<GoogleDriveOAuthService>.Instance);

        var result = await service.DisconnectAsync(12);

        Assert.True(result.HadStoredCredential);
        Assert.False(result.ProviderRevoked);
        settings.Verify(store => store.ClearRefreshTokenAsync(
            12, "current-version", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DisconnectAsync_WithoutStoredCredential_IsSuccessfulNoOp()
    {
        var settings = new Mock<IGoogleDriveSettingsStore>();
        settings.Setup(store => store.GetRuntimeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleDriveOptions());
        var clients = new Mock<IHttpClientFactory>();
        var service = new GoogleDriveOAuthService(
            new EphemeralDataProtectionProvider(), settings.Object, clients.Object,
            Mock.Of<IGoogleDriveAdapter>(), Mock.Of<IPermissionService>(),
            NullLogger<GoogleDriveOAuthService>.Instance);

        var result = await service.DisconnectAsync(12);

        Assert.False(result.HadStoredCredential);
        Assert.False(result.ProviderRevoked);
        clients.Verify(factory => factory.CreateClient(It.IsAny<string>()), Times.Never);
        settings.Verify(store => store.ClearRefreshTokenAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CompleteAsync_ConfigurationChangedBeforeExchange_ReturnsConfigurationChanged()
    {
        var initial = new GoogleDriveOptions
        {
            Enabled = true,
            ClientId = "123.apps.googleusercontent.com",
            ClientSecret = "client-secret",
            OAuthRedirectUri = "https://example.com/api/site-settings/google-drive/oauth/callback",
            ConfigurationVersion = "version-one",
        };
        var changed = new GoogleDriveOptions
        {
            Enabled = true,
            ClientId = initial.ClientId,
            ClientSecret = initial.ClientSecret,
            OAuthRedirectUri = initial.OAuthRedirectUri,
            ConfigurationVersion = "version-two",
        };
        var settings = new Mock<IGoogleDriveSettingsStore>();
        settings.SetupSequence(store => store.GetRuntimeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(initial)
            .ReturnsAsync(changed);
        var permissions = new Mock<IPermissionService>();
        permissions.Setup(service => service.HasAsync(12, "system.settings.manage", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var clients = new Mock<IHttpClientFactory>();
        var service = new GoogleDriveOAuthService(
            new EphemeralDataProtectionProvider(),
            settings.Object,
            clients.Object,
            Mock.Of<IGoogleDriveAdapter>(),
            permissions.Object,
            NullLogger<GoogleDriveOAuthService>.Instance);
        var start = await service.CreateAuthorizationRequestAsync(12);
        var state = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(
            new Uri(start.AuthorizationUrl).Query)["state"].ToString();

        var result = await service.CompleteAsync("authorization-code", state, null);

        Assert.Equal(GoogleDriveOAuthResult.ConfigurationChanged, result);
        clients.Verify(factory => factory.CreateClient(It.IsAny<string>()), Times.Never);
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
        var start = await service.CreateAuthorizationRequestAsync(12);
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
        var settingsStore = new Mock<IGoogleDriveSettingsStore>();
        settingsStore.Setup(store => store.GetRuntimeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(options);
        var permissions = new Mock<IPermissionService>();
        permissions.Setup(service => service.HasAsync(12, "system.settings.manage", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var client = new HttpClient(new OAuthHttpHandler());
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(factory => factory.CreateClient(It.IsAny<string>())).Returns(client);
        var service = new GoogleDriveOAuthService(
            dataProtection,
            settingsStore.Object,
            httpClientFactory.Object,
            Mock.Of<IGoogleDriveAdapter>(),
            permissions.Object,
            NullLogger<GoogleDriveOAuthService>.Instance);
        var start = await service.CreateAuthorizationRequestAsync(12);
        var state = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(
            new Uri(start.AuthorizationUrl).Query)["state"].ToString();

        var result = await service.CompleteAsync("authorization-code", state, null);

        Assert.Equal(GoogleDriveOAuthResult.Success, result);
        settingsStore.Verify(store => store.SaveRefreshTokenAsync(
            "new-refresh-token", "owner@example.com", 12, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
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
        var settingsStore = new Mock<IGoogleDriveSettingsStore>();
        settingsStore.Setup(store => store.GetRuntimeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(options);
        var permissions = new Mock<IPermissionService>();
        permissions.Setup(service => service.HasAsync(12, "system.settings.manage", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(new OAuthHttpHandler(canManageRoot: false)));
        var service = new GoogleDriveOAuthService(
            dataProtection,
            settingsStore.Object,
            httpClientFactory.Object,
            Mock.Of<IGoogleDriveAdapter>(),
            permissions.Object,
            NullLogger<GoogleDriveOAuthService>.Instance);
        var start = await service.CreateAuthorizationRequestAsync(12);
        var state = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(
            new Uri(start.AuthorizationUrl).Query)["state"].ToString();

        var result = await service.CompleteAsync("authorization-code", state, null);

        Assert.Equal(GoogleDriveOAuthResult.RootValidationFailed, result);
        settingsStore.Verify(store => store.SaveRefreshTokenAsync(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
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
        var settingsStore = new Mock<IGoogleDriveSettingsStore>();
        settingsStore.Setup(store => store.GetRuntimeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(options);
        settingsStore.Setup(store => store.GetAdminAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleDriveAdminConfigurationResponse(
                true, options.ClientId, true, true, options.OAuthRedirectUri,
                options.FrontendReturnUrl, options.RootFolderId, options.InstanceId,
                options.ApplicationName, options.Folders, true, 15,
                "owner@example.com", DateTime.UtcNow, "row-version"));
        var drive = new Mock<IGoogleDriveAdapter>();
        drive.Setup(adapter => adapter.CheckConnectionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GoogleDriveReconnectRequiredException("revoked"));
        var service = new GoogleDriveOAuthService(
            new EphemeralDataProtectionProvider(),
            settingsStore.Object,
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

    private static GoogleDriveOAuthService CreateOAuthService(GoogleDriveOptions options)
    {
        var settings = new Mock<IGoogleDriveSettingsStore>();
        settings.Setup(store => store.GetRuntimeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(options);
        return new GoogleDriveOAuthService(
            new EphemeralDataProtectionProvider(),
            settings.Object,
            Mock.Of<IHttpClientFactory>(),
            Mock.Of<IGoogleDriveAdapter>(),
            Mock.Of<IPermissionService>(),
            NullLogger<GoogleDriveOAuthService>.Instance);
    }

    private static UpdateGoogleDriveConfigurationRequest ValidConfiguration(string? clientSecret = null) => new()
    {
        Enabled = true,
        ClientId = "123.apps.googleusercontent.com",
        ClientSecret = clientSecret,
        OAuthRedirectUri = "https://example.com/api/site-settings/google-drive/oauth/callback",
        FrontendReturnUrl = "/admin/settings?tab=drive",
        RootFolderId = "1234567890root",
        InstanceId = "nicon-test",
        ApplicationName = "Nicon Google Drive Integration",
        Folders = new GoogleDriveFolderOptions(),
        SupportsAllDrives = true,
        PollIntervalSeconds = 15,
    };

    private sealed class OAuthHttpHandler(
        bool canManageRoot = true,
        bool revokeSucceeds = true,
        bool revokeTimesOut = false,
        Action? onRevoke = null) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var isTokenRequest = request.RequestUri?.AbsolutePath.EndsWith("/token", StringComparison.Ordinal) == true;
            var isRootRequest = request.RequestUri?.AbsolutePath.Contains("/drive/v3/files/", StringComparison.Ordinal) == true;
            var isRevokeRequest = request.RequestUri?.AbsolutePath.EndsWith("/revoke", StringComparison.Ordinal) == true;
            if (isRevokeRequest) onRevoke?.Invoke();
            if (isRevokeRequest && revokeTimesOut)
                return Task.FromException<HttpResponseMessage>(new TaskCanceledException("Provider timeout."));
            var status = isRootRequest && !canManageRoot || isRevokeRequest && !revokeSucceeds
                ? HttpStatusCode.Forbidden
                : HttpStatusCode.OK;
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