using NihomeBackend.Services.GoogleDrive;
using Moq;

namespace nihomebackend.tests.Services;

public sealed class GoogleDriveAdapterTests
{
    [Fact]
    public async Task CheckConnectionAsync_IncompleteOAuthSettings_AreRejectedBeforeDriveRequest()
    {
        var credentialStore = new Mock<IGoogleDriveCredentialStore>();
        credentialStore.Setup(store => store.GetRefreshTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        using var adapter = new GoogleDriveAdapter(new GoogleDriveOptions
        {
            ClientId = "client-id",
            ClientSecret = "client-secret",
            RootFolderId = "root-folder",
        }, credentialStore.Object);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adapter.CheckConnectionAsync());

        Assert.Contains("kết nối lại", exception.Message);
    }
}