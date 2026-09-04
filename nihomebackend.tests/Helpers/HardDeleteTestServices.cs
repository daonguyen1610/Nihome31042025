using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Services;
using NihomeBackend.Services.GoogleDrive;
using NihomeBackend.Services.HardDelete;

namespace nihomebackend.tests.Helpers;

internal static class HardDeleteTestServices
{
    public static (
        IProjectHardDeletePlanService Plans,
        ICrmHardDeletePlanService CrmPlans,
        IHardDeleteOperationService Operations) Create(
        AppDbContext db,
        IProjectDocumentStagingService projectDocuments)
    {
        var settings = new Mock<IGoogleDriveSettingsStore>();
        settings.Setup(item => item.GetRuntimeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleDriveOptions { InstanceId = "unit-test" });

        var files = new Mock<IHardDeleteFileService>();
        files.Setup(item => item.ValidateManagedPath(It.IsAny<string>()))
            .Returns((string path) => path.StartsWith("/files/", StringComparison.Ordinal)
                ? path
                : throw new HardDeleteFileException("invalid_managed_path", "invalid"));
        files.Setup(item => item.QuarantineAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, string path, CancellationToken _) =>
                new HardDeleteQuarantineResult(path, null, true));

        var drive = new Mock<IGoogleDriveAdapter>();
        drive.Setup(item => item.PermanentDeleteOwnedAsync(
                It.IsAny<DrivePermanentDeleteRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
            var permissions = new Mock<IPermissionService>();
            permissions.Setup(item => item.HasAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

        var plans = new ProjectHardDeletePlanService(db, settings.Object, files.Object);
        var crmPlans = new CrmHardDeletePlanService(db, settings.Object, files.Object);
        IHardDeleteResourceHandler[] handlers =
        [
            new DesignProjectHardDeleteHandler(db, plans, projectDocuments),
            new OperationalProjectHardDeleteHandler(db, plans, projectDocuments),
            new CustomerHardDeleteHandler(db, crmPlans, permissions.Object),
            new LeadHardDeleteHandler(db, crmPlans),
            new TenderHardDeleteHandler(db, crmPlans),
            new QuoteHardDeleteHandler(db, crmPlans, permissions.Object),
        ];
        var operations = new HardDeleteOperationService(
            db,
            files.Object,
            drive.Object,
            new HardDeleteResourceHandlerRegistry(handlers),
            NullLogger<HardDeleteOperationService>.Instance);
        return (plans, crmPlans, operations);
    }
}
