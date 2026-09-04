using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NihomeBackend.Models;
using NihomeBackend.Services;
using NihomeBackend.Services.GoogleDrive;
using NihomeBackend.Services.HardDelete;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public sealed class HardDeleteOperationServiceTests : IDisposable
{
    private readonly NihomeBackend.Data.AppDbContext db = DbContextFactory.Create();
    private readonly Mock<IHardDeleteFileService> files = new();
    private readonly Mock<IGoogleDriveAdapter> drive = new();
    private readonly Mock<IHardDeleteResourceHandler> handler = new();
    private readonly Mock<IHardDeleteResourceHandlerRegistry> registry = new();
    private readonly HardDeleteOperationService service;

    public HardDeleteOperationServiceTests()
    {
        handler.SetupGet(item => item.ResourceType).Returns("test-resource");
        registry.Setup(item => item.Find("test-resource")).Returns(handler.Object);
        service = new HardDeleteOperationService(
            db, files.Object, drive.Object, registry.Object, Mock.Of<ILogger<HardDeleteOperationService>>());
    }

    [Fact]
    public async Task ProcessAsync_ValidPlan_CompletesInExternalDatabasePurgeOrder()
    {
        var calls = new List<string>();
        files.Setup(item => item.QuarantineAsync(It.IsAny<Guid>(), "/files/quotes/a.pdf", default))
            .Callback(() => calls.Add("quarantine"))
            .ReturnsAsync(new HardDeleteQuarantineResult(
                "/files/quotes/a.pdf", "/files/.hard-delete-quarantine/op/a.pdf", false));
        drive.Setup(item => item.PermanentDeleteOwnedAsync(It.IsAny<DrivePermanentDeleteRequest>(), default))
            .Callback(() => calls.Add("drive"))
            .Returns(Task.CompletedTask);
        handler.Setup(item => item.FinalizeAsync(It.IsAny<HardDeleteResourceContext>(), default))
            .Callback(() => calls.Add("database"))
            .Returns(Task.CompletedTask);
        files.Setup(item => item.PurgeAsync(It.IsAny<string>(), default))
            .Callback(() => calls.Add("purge"))
            .Returns(Task.CompletedTask);
        var operation = await service.CreateAsync(Request(
            LocalItem(0), DriveItem(1, "drive-1"), DatabaseItem(2)));

        var result = await service.ProcessAsync(operation.OperationId);

        Assert.True(result.IsComplete);
        Assert.Equal(HardDeleteOperationStatus.Completed, result.Status);
        Assert.Equal(["quarantine", "drive", "database", "purge"], calls);
    }

    [Fact]
    public async Task ProcessAsync_OwnershipRejectedBeforeDriveDelete_RestoresQuarantineAndRequiresManualAction()
    {
        files.Setup(item => item.QuarantineAsync(It.IsAny<Guid>(), It.IsAny<string>(), default))
            .ReturnsAsync(new HardDeleteQuarantineResult(
                "/files/quotes/a.pdf", "/files/.hard-delete-quarantine/op/a.pdf", false));
        drive.Setup(item => item.PermanentDeleteOwnedAsync(It.IsAny<DrivePermanentDeleteRequest>(), default))
            .ThrowsAsync(new DrivePermanentDeleteRejectedException("drive_instance_mismatch", "rejected"));
        var operation = await service.CreateAsync(Request(
            LocalItem(0), DriveItem(1, "drive-1"), DatabaseItem(2)));

        var result = await service.ProcessAsync(operation.OperationId);

        Assert.Equal(HardDeleteOperationStatus.ManualActionRequired, result.Status);
        Assert.Equal("drive_instance_mismatch", result.ErrorCode);
        files.Verify(item => item.RestoreAsync(
            "/files/quotes/a.pdf", "/files/.hard-delete-quarantine/op/a.pdf", default), Times.Once);
        handler.Verify(item => item.FinalizeAsync(It.IsAny<HardDeleteResourceContext>(), default), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_AuthorizationChanged_StopsBeforeExternalSideEffects()
    {
        handler.Setup(item => item.AuthorizeAsync(It.IsAny<HardDeleteResourceContext>(), default))
            .ThrowsAsync(new HardDeleteAuthorizationException("permission changed"));
        var operation = await service.CreateAsync(Request(
            LocalItem(0), DriveItem(1, "drive-1"), DatabaseItem(2)));

        var result = await service.ProcessAsync(operation.OperationId);

        Assert.Equal(HardDeleteOperationStatus.ManualActionRequired, result.Status);
        Assert.Equal("hard_delete_authorization_changed", result.ErrorCode);
        files.Verify(item => item.QuarantineAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), default), Times.Never);
        drive.Verify(item => item.PermanentDeleteOwnedAsync(
            It.IsAny<DrivePermanentDeleteRequest>(), default), Times.Never);
        handler.Verify(item => item.FinalizeAsync(
            It.IsAny<HardDeleteResourceContext>(), default), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_PlanChangesAtFinalizer_RestoresQuarantineAndRequiresManualAction()
    {
        files.Setup(item => item.QuarantineAsync(It.IsAny<Guid>(), It.IsAny<string>(), default))
            .ReturnsAsync(new HardDeleteQuarantineResult(
                "/files/quotes/a.pdf", "/files/.hard-delete-quarantine/op/a.pdf", false));
        handler.Setup(item => item.FinalizeAsync(It.IsAny<HardDeleteResourceContext>(), default))
            .ThrowsAsync(new DeletionPlanChangedException("plan changed"));
        var operation = await service.CreateAsync(Request(LocalItem(0), DatabaseItem(1)));

        var result = await service.ProcessAsync(operation.OperationId);

        Assert.Equal(HardDeleteOperationStatus.ManualActionRequired, result.Status);
        Assert.Equal("deletion_plan_changed", result.ErrorCode);
        files.Verify(item => item.RestoreAsync(
            "/files/quotes/a.pdf", "/files/.hard-delete-quarantine/op/a.pdf", default), Times.Once);
        var persisted = await db.HardDeleteOperations.FindAsync(operation.OperationId);
        Assert.False(persisted!.HasIrreversibleStep);
    }

    [Fact]
    public async Task ProcessAsync_FailureAfterFirstDriveDelete_StaysInForwardRecoveryWithoutRestore()
    {
        files.Setup(item => item.QuarantineAsync(It.IsAny<Guid>(), It.IsAny<string>(), default))
            .ReturnsAsync(new HardDeleteQuarantineResult(
                "/files/quotes/a.pdf", "/files/.hard-delete-quarantine/op/a.pdf", false));
        drive.SetupSequence(item => item.PermanentDeleteOwnedAsync(It.IsAny<DrivePermanentDeleteRequest>(), default))
            .Returns(Task.CompletedTask)
            .ThrowsAsync(new IOException("temporary Drive outage"));
        var operation = await service.CreateAsync(Request(
            LocalItem(0), DriveItem(1, "drive-1"), DriveItem(2, "drive-2"), DatabaseItem(3)));

        var result = await service.ProcessAsync(operation.OperationId);

        Assert.Equal(HardDeleteOperationStatus.Failed, result.Status);
        Assert.False(result.IsComplete);
        var persisted = await db.HardDeleteOperations.FindAsync(operation.OperationId);
        Assert.True(persisted!.HasIrreversibleStep);
        Assert.NotNull(persisted.NextAttemptAt);
        files.Verify(item => item.RestoreAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_PurgeFailureAfterDatabaseFinalizer_DoesNotReportCompletion()
    {
        files.Setup(item => item.QuarantineAsync(It.IsAny<Guid>(), It.IsAny<string>(), default))
            .ReturnsAsync(new HardDeleteQuarantineResult(
                "/files/quotes/a.pdf", "/files/.hard-delete-quarantine/op/a.pdf", false));
        handler.Setup(item => item.FinalizeAsync(It.IsAny<HardDeleteResourceContext>(), default))
            .Returns(Task.CompletedTask);
        files.Setup(item => item.PurgeAsync(It.IsAny<string>(), default))
            .ThrowsAsync(new IOException("storage unavailable"));
        var operation = await service.CreateAsync(Request(LocalItem(0), DatabaseItem(1)));

        var result = await service.ProcessAsync(operation.OperationId);

        Assert.Equal(HardDeleteOperationStatus.Failed, result.Status);
        Assert.False(result.IsComplete);
        handler.Verify(item => item.FinalizeAsync(It.IsAny<HardDeleteResourceContext>(), default), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_PostCommitRecovery_AuthorizesMissingRootAndPurgesQuarantine()
    {
        var operation = await service.CreateAsync(Request(LocalItem(0), DatabaseItem(1)));
        var persisted = await db.HardDeleteOperations.Include(item => item.Items)
            .SingleAsync(item => item.Id == operation.OperationId);
        persisted.Status = HardDeleteOperationStatus.Failed;
        persisted.HasIrreversibleStep = true;
        var local = persisted.Items.Single(item => item.Kind == HardDeleteItemKind.LocalFile);
        local.Status = HardDeleteItemStatus.Quarantined;
        local.QuarantinePath = "/files/.hard-delete-quarantine/op/a.pdf";
        await db.SaveChangesAsync();
        handler.Setup(item => item.AuthorizeAsync(
                It.Is<HardDeleteResourceContext>(context => context.IsForwardRecovery), default))
            .Returns(Task.CompletedTask);
        handler.Setup(item => item.FinalizeAsync(
                It.Is<HardDeleteResourceContext>(context => context.IsForwardRecovery), default))
            .Returns(Task.CompletedTask);
        files.Setup(item => item.PurgeAsync(local.QuarantinePath, default))
            .Returns(Task.CompletedTask);

        var result = await service.ProcessAsync(operation.OperationId);

        Assert.True(result.IsComplete);
        handler.Verify(item => item.AuthorizeAsync(
            It.Is<HardDeleteResourceContext>(context => context.IsForwardRecovery), default), Times.Once);
        files.Verify(item => item.PurgeAsync(local.QuarantinePath, default), Times.Once);
    }

    private static CreateHardDeleteOperationRequest Request(params HardDeleteItemDefinition[] items) => new(
        "test-resource", "42", "Resource 42", "plan-token", "DELETE-42", "user-1", items);

    private static HardDeleteItemDefinition LocalItem(int sequence) =>
        new(HardDeleteItemKind.LocalFile, "/files/quotes/a.pdf", sequence);

    private static HardDeleteItemDefinition DriveItem(int sequence, string id) => new(
        HardDeleteItemKind.DriveFile,
        id,
        sequence,
        new Dictionary<string, string> { ["niconReplicaKey"] = id },
        "parent-1");

    private static HardDeleteItemDefinition DatabaseItem(int sequence) =>
        new(HardDeleteItemKind.DatabaseAggregate, "delete-root", sequence);

    public void Dispose() => db.Dispose();
}