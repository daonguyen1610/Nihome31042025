using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NihomeBackend.Controllers;
using NihomeBackend.Models;
using NihomeBackend.Services.Audit;
using NihomeBackend.Services.GoogleDrive;

namespace nihomebackend.tests.Controllers;

public sealed class GoogleDriveOAuthControllerTests
{
    private readonly Mock<IGoogleDriveSettingsStore> settingsStore = new();
    private readonly Mock<IGoogleDriveAdapter> drive = new();
    private readonly Mock<IDriveTreeMigrationService> migration = new();
    private readonly Mock<IAuditLogger> audit = new();

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

    [Theory]
    [InlineData(DriveTreeMigrationStatus.Completed, AuditStatus.Success, null)]
    [InlineData(DriveTreeMigrationStatus.Failed, AuditStatus.Failure, "boom")]
    public async Task RunTreeMigration_IgnoresRequestCancellationAndIsAudited(
        DriveTreeMigrationStatus status, string auditStatus, string? error)
    {
        var result = new DriveTreeMigrationResult(3, status, 2, 1, error);
        migration.Setup(service => service.RunAsync(7, CancellationToken.None)).ReturnsAsync(result);

        var response = await SignedInController().RunTreeMigration();

        Assert.Same(result, Assert.IsType<OkObjectResult>(response.Result).Value);
        migration.Verify(service => service.RunAsync(7, CancellationToken.None), Times.Once);
        audit.Verify(log => log.Log(It.Is<AuditEvent>(entry => entry.Action == "drive.tree-migration.run"
            && entry.ResourceId == "3" && entry.Status == auditStatus && entry.FailureReason == error)), Times.Once);
    }

    [Fact]
    public async Task UndoTreeMigration_IgnoresRequestCancellationAndIsAudited()
    {
        migration.Setup(service => service.UndoAsync(3, 7, CancellationToken.None))
            .ReturnsAsync(new DriveTreeMigrationResult(3, DriveTreeMigrationStatus.RolledBack, 2, 0, null));

        var response = await SignedInController().UndoTreeMigration(3);

        Assert.IsType<OkObjectResult>(response.Result);
        migration.Verify(service => service.UndoAsync(3, 7, CancellationToken.None), Times.Once);
        audit.Verify(log => log.Log(It.Is<AuditEvent>(entry => entry.Action == "drive.tree-migration.undo"
            && entry.ResourceId == "3" && entry.Status == AuditStatus.Success)), Times.Once);
    }

    [Fact]
    public async Task UndoTreeMigration_ReturnsConflictWithBlockerCode()
    {
        migration.Setup(service => service.UndoAsync(3, 7, CancellationToken.None))
            .ThrowsAsync(new DriveTreeMigrationBlockedException("DRIVE_TREE_MIGRATION_RUNNING"));

        var response = await SignedInController().UndoTreeMigration(3);

        var conflict = Assert.IsType<ConflictObjectResult>(response.Result);
        using var body = JsonDocument.Parse(JsonSerializer.Serialize(conflict.Value));
        Assert.Equal("DRIVE_TREE_MIGRATION_RUNNING", body.RootElement.GetProperty("code").GetString());
        audit.VerifyNoOtherCalls();
    }

    private GoogleDriveOAuthController SignedInController()
    {
        var controller = Controller();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "7")], "test")),
                RequestAborted = new CancellationToken(canceled: true),
            },
        };
        return controller;
    }

    private GoogleDriveOAuthController Controller() =>
        new(
            null!,
            settingsStore.Object,
            drive.Object,
            migration.Object,
            audit.Object,
            NullLogger<GoogleDriveOAuthController>.Instance);

    private static void AssertBadRequestCode(ActionResult? result, string expectedCode)
    {
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var json = JsonSerializer.Serialize(badRequest.Value);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
    }
}
