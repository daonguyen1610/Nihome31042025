using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NihomeBackend.Controllers;
using NihomeBackend.Services.GoogleDrive;

namespace nihomebackend.tests.Controllers;

public sealed class GoogleDriveOAuthControllerTests
{
    private readonly Mock<IGoogleDriveSettingsStore> settingsStore = new();
    private readonly Mock<IGoogleDriveAdapter> drive = new();

    public GoogleDriveOAuthControllerTests()
    {
        settingsStore
            .Setup(store => store.GetRuntimeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleDriveOptions
            {
                Enabled = true,
                RefreshToken = "refresh-token",
                RootFolderId = "root",
            });
    }

    [Fact]
    public async Task Test_ReturnsConnectionDetails()
    {
        drive
            .Setup(adapter => adapter.CheckConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DriveConnection(
                "admin@nicon.test", "NICON", "https://drive.google.com/folders/root",
                true, false, true, true));

        var response = await Controller().Test(default);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var body = Assert.IsType<GoogleDriveConnectionTestResponse>(ok.Value);
        Assert.Equal("admin@nicon.test", body.AccountEmail);
        Assert.Equal("NICON", body.FolderName);
        Assert.True(body.IsSharedDrive);
        Assert.True(body.CanAddChildren);
    }

    [Fact]
    public async Task Test_ReturnsNotConnectedWithoutCallingDrive()
    {
        settingsStore
            .Setup(store => store.GetRuntimeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleDriveOptions { Enabled = false });

        var response = await Controller().Test(default);

        AssertBadRequestCode(response.Result, "DRIVE_NOT_CONNECTED");
        drive.Verify(
            adapter => adapter.CheckConnectionAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Test_ReturnsInvalidRootWhenRootIsNotAUsableFolder()
    {
        drive
            .Setup(adapter => adapter.CheckConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DriveConnection(
                "admin@nicon.test", "NICON", "https://drive.google.com/folders/root",
                false, false, true, true));

        var response = await Controller().Test(default);

        AssertBadRequestCode(response.Result, "DRIVE_INVALID_ROOT");
    }

    [Fact]
    public async Task Test_ReturnsReconnectCodeWhenGrantIsInvalid()
    {
        drive
            .Setup(adapter => adapter.CheckConnectionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GoogleDriveReconnectRequiredException("invalid_grant"));

        var response = await Controller().Test(default);

        AssertBadRequestCode(response.Result, "DRIVE_RECONNECT_REQUIRED");
    }

    [Fact]
    public async Task Test_ReturnsFailedCodeForProviderTimeout()
    {
        drive
            .Setup(adapter => adapter.CheckConnectionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("provider timeout"));

        var response = await Controller().Test(default);

        AssertBadRequestCode(response.Result, "DRIVE_TEST_FAILED");
    }

    private GoogleDriveOAuthController Controller() =>
        new(
            null!,
            settingsStore.Object,
            drive.Object,
            NullLogger<GoogleDriveOAuthController>.Instance);

    private static void AssertBadRequestCode(ActionResult? result, string expectedCode)
    {
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var json = JsonSerializer.Serialize(badRequest.Value);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
    }
}
