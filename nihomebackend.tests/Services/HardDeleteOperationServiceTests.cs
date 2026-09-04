using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using NihomeBackend.Constants;
using NihomeBackend.Data;
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
        Assert.Single(db.AuditLogs, item => item.AuditId == operation.OperationId.ToString("N") &&
            item.Action == "test-resource.delete" && item.Status == "success");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task CreateAsync_RequiresExactlyOneDatabaseFinalizerBeforeAnySideEffect(int databaseItemCount)
    {
        var items = new List<HardDeleteItemDefinition> { LocalItem(0), DriveItem(1, "drive-1") };
        for (var index = 0; index < databaseItemCount; index++)
            items.Add(new HardDeleteItemDefinition(
                HardDeleteItemKind.DatabaseAggregate, $"delete-root-{index}", index + 2));

        var exception = await Assert.ThrowsAsync<HardDeleteOperationException>(
            () => service.CreateAsync(Request(items.ToArray())));

        Assert.Equal("invalid_operation_plan", exception.Code);
        Assert.Empty(db.HardDeleteOperations);
        files.Verify(item => item.QuarantineAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), default), Times.Never);
        drive.Verify(item => item.PermanentDeleteOwnedAsync(
            It.IsAny<DrivePermanentDeleteRequest>(), default), Times.Never);
    }

    [Theory]
    [InlineData(EntityTypes.Quote, "quote.delete")]
    [InlineData(EntityTypes.Contract, "contract.delete")]
    [InlineData(EntityTypes.Tender, "tender.delete")]
    [InlineData(EntityTypes.Opportunity, "opportunity.delete")]
    [InlineData(EntityTypes.Survey, "survey.delete")]
    [InlineData(EntityTypes.CapabilityDocument, "capability-doc.delete")]
    [InlineData(EntityTypes.Customer, "customer.delete")]
    [InlineData(EntityTypes.Lead, "lead.delete")]
    [InlineData(EntityTypes.DesignProject, "design-project.delete")]
    [InlineData(EntityTypes.OperationalProject, "operational-project.delete")]
    public async Task ProcessAsync_CompletedBusinessRoot_WritesExactlyOneCompletionAudit(
        string resourceType, string expectedAction)
    {
        var resourceHandler = new Mock<IHardDeleteResourceHandler>();
        resourceHandler.SetupGet(item => item.ResourceType).Returns(resourceType);
        resourceHandler.Setup(item => item.FinalizeAsync(It.IsAny<HardDeleteResourceContext>(), default))
            .Returns(Task.CompletedTask);
        var operationService = new HardDeleteOperationService(
            db,
            files.Object,
            drive.Object,
            new HardDeleteResourceHandlerRegistry([resourceHandler.Object]),
            Mock.Of<ILogger<HardDeleteOperationService>>());
        var operation = await operationService.CreateAsync(new CreateHardDeleteOperationRequest(
            resourceType, "42", "Root 42", "plan-token", "DELETE-42", "77", [DatabaseItem(0)]));

        var first = await operationService.ProcessAsync(operation.OperationId);
        var repeated = await operationService.ProcessAsync(operation.OperationId);

        Assert.True(first.IsComplete);
        Assert.True(repeated.IsComplete);
        Assert.Single(db.AuditLogs, item => item.AuditId == operation.OperationId.ToString("N") &&
            item.ResourceType == resourceType && item.Action == expectedAction && item.Status == "success");
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
    public async Task ProcessAsync_TenderPermissionRevoked_StopsBeforeEveryDeleteEffect()
    {
        var plans = new Mock<ICrmHardDeletePlanService>();
        var permissions = new Mock<IPermissionService>();
        permissions.Setup(item => item.HasAsync(77, "crm.tenders.manage", default))
            .ReturnsAsync(false);
        var tenderHandler = new TenderHardDeleteHandler(db, plans.Object, permissions.Object);
        var tenderService = new HardDeleteOperationService(
            db,
            files.Object,
            drive.Object,
            new HardDeleteResourceHandlerRegistry([tenderHandler]),
            Mock.Of<ILogger<HardDeleteOperationService>>());
        var operation = await tenderService.CreateAsync(new CreateHardDeleteOperationRequest(
            EntityTypes.Tender,
            "42",
            "TD-42",
            "plan-token",
            "TD-42",
            "77",
            [LocalItem(0), DriveItem(1, "drive-1"), DatabaseItem(2)]));

        var result = await tenderService.ProcessAsync(operation.OperationId);

        Assert.Equal(HardDeleteOperationStatus.ManualActionRequired, result.Status);
        Assert.Equal("hard_delete_authorization_changed", result.ErrorCode);
        permissions.Verify(item => item.HasAsync(77, "crm.tenders.manage", default), Times.Once);
        plans.Verify(item => item.ForTenderAsync(It.IsAny<int>(), default), Times.Never);
        files.Verify(item => item.QuarantineAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), default), Times.Never);
        drive.Verify(item => item.PermanentDeleteOwnedAsync(
            It.IsAny<DrivePermanentDeleteRequest>(), default), Times.Never);
        Assert.DoesNotContain(db.AuditLogs, item => item.AuditId == operation.OperationId.ToString("N"));
    }

    [Fact]
    public async Task ProcessAsync_TenderMissingBeforeDatabaseFinalization_StopsForwardRecovery()
    {
        var plans = new Mock<ICrmHardDeletePlanService>();
        plans.Setup(item => item.ForTenderAsync(42, default)).ReturnsAsync((CrmHardDeletePlan?)null);
        var permissions = new Mock<IPermissionService>();
        permissions.Setup(item => item.HasAsync(77, "crm.tenders.manage", default)).ReturnsAsync(true);
        var tenderHandler = new TenderHardDeleteHandler(db, plans.Object, permissions.Object);
        var tenderService = new HardDeleteOperationService(
            db, files.Object, drive.Object,
            new HardDeleteResourceHandlerRegistry([tenderHandler]),
            Mock.Of<ILogger<HardDeleteOperationService>>());
        var operation = await tenderService.CreateAsync(new CreateHardDeleteOperationRequest(
            EntityTypes.Tender, "42", "TD-42", "plan-token", "TD-42", "77",
            [DriveItem(0, "drive-1"), DatabaseItem(1)]));
        var persisted = await db.HardDeleteOperations.Include(item => item.Items)
            .SingleAsync(item => item.Id == operation.OperationId);
        persisted.HasIrreversibleStep = true;
        var driveItem = persisted.Items.Single(item => item.Kind == HardDeleteItemKind.DriveFile);
        driveItem.Status = HardDeleteItemStatus.Completed;
        driveItem.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var result = await tenderService.ProcessAsync(operation.OperationId);

        Assert.Equal(HardDeleteOperationStatus.ManualActionRequired, result.Status);
        Assert.Equal("hard_delete_authorization_changed", result.ErrorCode);
        files.Verify(item => item.QuarantineAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), default), Times.Never);
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
        Assert.DoesNotContain(db.AuditLogs, item => item.AuditId == operation.OperationId.ToString("N"));

        files.Setup(item => item.PurgeAsync(It.IsAny<string>(), default)).Returns(Task.CompletedTask);
        var retry = await service.ProcessAsync(operation.OperationId);

        Assert.True(retry.IsComplete);
        Assert.Single(db.AuditLogs, item => item.AuditId == operation.OperationId.ToString("N") &&
            item.Action == "test-resource.delete" && item.Status == "success");

        var repeated = await service.ProcessAsync(operation.OperationId);
        Assert.True(repeated.IsComplete);
        Assert.Single(db.AuditLogs, item => item.AuditId == operation.OperationId.ToString("N"));
    }

    [Fact]
    public async Task ProcessAsync_CompletionSaveFailure_IsPersistedAsRetryableWithoutAudit()
    {
        var interceptor = new CompletionSaveFailureInterceptor();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;
        await using var faultingDb = new AppDbContext(options);
        await faultingDb.Database.EnsureCreatedAsync();
        var resourceHandler = new Mock<IHardDeleteResourceHandler>();
        resourceHandler.SetupGet(item => item.ResourceType).Returns("completion-fault");
        resourceHandler.Setup(item => item.FinalizeAsync(It.IsAny<HardDeleteResourceContext>(), default))
            .Returns(Task.CompletedTask);
        var operationService = new HardDeleteOperationService(
            faultingDb,
            Mock.Of<IHardDeleteFileService>(),
            Mock.Of<IGoogleDriveAdapter>(),
            new HardDeleteResourceHandlerRegistry([resourceHandler.Object]),
            Mock.Of<ILogger<HardDeleteOperationService>>());
        var operation = await operationService.CreateAsync(new CreateHardDeleteOperationRequest(
            "completion-fault", "42", "Root 42", "plan-token", "DELETE-42", "77", [DatabaseItem(0)]));

        var failed = await operationService.ProcessAsync(operation.OperationId);

        Assert.Equal(HardDeleteOperationStatus.Failed, failed.Status);
        Assert.False(failed.IsComplete);
        Assert.Equal("hard_delete_processing_failed", failed.ErrorCode);
        var persisted = await faultingDb.HardDeleteOperations.AsNoTracking()
            .SingleAsync(item => item.Id == operation.OperationId);
        Assert.Equal(HardDeleteOperationStatus.Failed, persisted.Status);
        Assert.Null(persisted.CompletedAt);
        Assert.DoesNotContain(faultingDb.AuditLogs, item => item.AuditId == operation.OperationId.ToString("N"));
    }

    [Fact]
    public async Task ProcessAsync_PostFinalizerRecovery_SkipsAuthorizationAndPurgesQuarantine()
    {
        var operation = await service.CreateAsync(Request(LocalItem(0), DatabaseItem(1)));
        var persisted = await db.HardDeleteOperations.Include(item => item.Items)
            .SingleAsync(item => item.Id == operation.OperationId);
        persisted.Status = HardDeleteOperationStatus.Failed;
        persisted.HasIrreversibleStep = true;
        var local = persisted.Items.Single(item => item.Kind == HardDeleteItemKind.LocalFile);
        local.Status = HardDeleteItemStatus.Quarantined;
        local.QuarantinePath = "/files/.hard-delete-quarantine/op/a.pdf";
        var databaseItem = persisted.Items.Single(item => item.Kind == HardDeleteItemKind.DatabaseAggregate);
        databaseItem.Status = HardDeleteItemStatus.Completed;
        databaseItem.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        files.Setup(item => item.PurgeAsync(local.QuarantinePath, default))
            .Returns(Task.CompletedTask);

        var result = await service.ProcessAsync(operation.OperationId);

        Assert.True(result.IsComplete);
        handler.Verify(item => item.AuthorizeAsync(
            It.IsAny<HardDeleteResourceContext>(), default), Times.Never);
        handler.Verify(item => item.FinalizeAsync(
            It.IsAny<HardDeleteResourceContext>(), default), Times.Never);
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

    private sealed class CompletionSaveFailureInterceptor : SaveChangesInterceptor
    {
        private bool hasFailed;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (!hasFailed && eventData.Context is AppDbContext context &&
                context.ChangeTracker.Entries<AuditLog>().Any(entry => entry.State == EntityState.Added))
            {
                hasFailed = true;
                throw new DbUpdateException("Completion save failed.");
            }
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    public void Dispose() => db.Dispose();
}