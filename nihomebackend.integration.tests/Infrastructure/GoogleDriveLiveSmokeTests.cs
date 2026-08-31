using System.Security.Cryptography;
using System.Text;
using NihomeBackend.Services.GoogleDrive;

namespace NihomeBackend.IntegrationTests.Infrastructure;

public sealed class GoogleDriveLiveSmokeTests
{
    [Fact]
    [Trait("Category", "LiveGoogleDrive")]
    public async Task ConfiguredDrive_SupportsFolderUploadDownloadRenameMoveAndTrash()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("NICON_DRIVE_LIVE_TEST"), "1", StringComparison.Ordinal))
            return;

        var options = new GoogleDriveOptions
        {
            Enabled = true,
            ClientId = Required("NICON_DRIVE_CLIENT_ID"),
            ClientSecret = Required("NICON_DRIVE_CLIENT_SECRET"),
            RefreshToken = Required("NICON_DRIVE_REFRESH_TOKEN"),
            RootFolderId = Required("NICON_DRIVE_ROOT_FOLDER_ID"),
            InstanceId = "nicon-live-smoke",
            ApplicationName = "Nicon Google Drive Live Smoke Test",
            SupportsAllDrives = true,
        };
        using var drive = new GoogleDriveAdapter(new ConfiguredSettingsStore(options));
        var connection = await drive.CheckConnectionAsync();
        Assert.True(connection.IsFolder);
        Assert.False(connection.IsTrashed);
        Assert.True(connection.CanAddChildren);

        string? testRootId = null;
        try
        {
            var uniqueName = $"__nicon_smoke_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
            var testRoot = await drive.EnsureFolderPathAsync([uniqueName]);
            testRootId = testRoot.Id;
            var source = await drive.EnsureFolderPathAsync([uniqueName, "source"]);
            var destination = await drive.EnsureFolderPathAsync([uniqueName, "destination"]);
            var bytes = Encoding.UTF8.GetBytes($"Nicon Drive smoke test {Guid.NewGuid():N}");

            await using var uploadContent = new MemoryStream(bytes);
            var upload = await drive.UploadAsync(
                source.Id,
                $"live-smoke:{Guid.NewGuid():N}",
                1,
                "nicon-drive-smoke.txt",
                "text/plain",
                uploadContent);

            var metadata = await drive.GetMetadataAsync(upload.FileId);
            Assert.NotNull(metadata);
            Assert.Equal("nicon-drive-smoke.txt", metadata.Name);
            Assert.False(metadata.IsTrashed);

            await using var downloaded = new MemoryStream();
            await drive.DownloadAsync(upload.FileId, downloaded);
            Assert.Equal(SHA256.HashData(bytes), SHA256.HashData(downloaded.ToArray()));

            await drive.UpdateFileNameAsync(upload.FileId, "nicon-drive-smoke-renamed.txt");
            Assert.Equal("nicon-drive-smoke-renamed.txt", (await drive.GetMetadataAsync(upload.FileId))!.Name);

            await drive.MoveAsync(upload.FileId, destination.Id);
            Assert.DoesNotContain(await drive.ListChildrenAsync(source.Id), item => item.Id == upload.FileId);
            Assert.Contains(await drive.ListChildrenAsync(destination.Id), item => item.Id == upload.FileId);

            await drive.DeleteAsync(upload.FileId);
            Assert.True((await drive.GetMetadataAsync(upload.FileId))!.IsTrashed);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(testRootId)) await drive.DeleteAsync(testRootId);
        }
    }

    private sealed class ConfiguredSettingsStore(GoogleDriveOptions options) : IGoogleDriveSettingsStore
    {
        public Task<GoogleDriveOptions> GetRuntimeAsync(CancellationToken ct = default) =>
            Task.FromResult(options);

        public Task<GoogleDriveAdminConfigurationResponse> GetAdminAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();

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

    private static string Required(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Missing required live-test environment variable: {name}");
}
