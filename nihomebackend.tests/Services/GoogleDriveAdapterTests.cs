using NihomeBackend.Services.GoogleDrive;

namespace nihomebackend.tests.Services;

public sealed class GoogleDriveAdapterTests
{
    [Fact]
    public async Task CheckConnectionAsync_IncompleteOAuthSettings_AreRejectedBeforeDriveRequest()
    {
        using var adapter = new GoogleDriveAdapter(new GoogleDriveOptions
        {
            ClientId = "client-id",
            ClientSecret = "client-secret",
            RootFolderId = "root-folder",
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adapter.CheckConnectionAsync());

        Assert.Contains("RefreshToken", exception.Message);
    }
}