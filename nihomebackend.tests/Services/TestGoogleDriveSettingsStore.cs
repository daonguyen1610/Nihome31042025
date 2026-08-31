using NihomeBackend.Services.GoogleDrive;

namespace nihomebackend.tests.Services;

internal sealed class TestGoogleDriveSettingsStore(GoogleDriveOptions options) : IGoogleDriveSettingsStore
{
    public Task<GoogleDriveOptions> GetRuntimeAsync(CancellationToken ct = default) =>
        Task.FromResult(options);

    public Task<GoogleDriveAdminConfigurationResponse> GetAdminAsync(CancellationToken ct = default) =>
        Task.FromResult(new GoogleDriveAdminConfigurationResponse(
            options.Enabled,
            options.ClientId,
            !string.IsNullOrWhiteSpace(options.ClientSecret),
            !string.IsNullOrWhiteSpace(options.RefreshToken),
            options.OAuthRedirectUri,
            options.FrontendReturnUrl,
            options.RootFolderId,
            options.InstanceId,
            options.ApplicationName,
            options.Folders,
            options.SupportsAllDrives,
            options.PollIntervalSeconds,
            null,
            null,
            string.Empty));

    public Task<GoogleDriveAdminConfigurationResponse> UpdateAsync(
        UpdateGoogleDriveConfigurationRequest request,
        int updatedByUserId,
        CancellationToken ct = default) => throw new NotSupportedException();

    public Task SaveRefreshTokenAsync(
        string refreshToken,
        string? accountEmail,
        int connectedByUserId,
        string expectedConfigurationVersion,
        CancellationToken ct = default) => throw new NotSupportedException();
}
