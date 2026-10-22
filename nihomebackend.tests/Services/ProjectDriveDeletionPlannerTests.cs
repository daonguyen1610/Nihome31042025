using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NihomeBackend.Constants;
using NihomeBackend.Models;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;
using NihomeBackend.Services.GoogleDrive;
using NihomeBackend.Services.HardDelete;

namespace nihomebackend.tests.Services;

public sealed class ProjectDriveDeletionPlannerTests
{
    private readonly GoogleDriveOptions options = new() { Enabled = true, InstanceId = "nicon-test", RootFolderId = "root" };

    [Fact]
    public async Task Plan_DeduplicatesBindingsAndDeletesChildrenBeforeRoot()
    {
        var drive = new Mock<IGoogleDriveAdapter>();
        drive.Setup(item => item.ListDeletionChildrenAsync("root", default)).ReturnsAsync([]);
        var root = Folder("project", "root", ProjectDriveFolderService.CreateProjectIdentity(options.InstanceId, 7));
        var child = Folder("design", "project", ProjectDriveFolderService.CreatePathIdentity(options.InstanceId, 7, "Design"));
        drive.Setup(item => item.GetMetadataAsync("design", default)).ReturnsAsync(child);
        drive.Setup(item => item.GetMetadataAsync("project", default)).ReturnsAsync(root);
        drive.Setup(item => item.ListDeletionChildrenAsync("project", default)).ReturnsAsync([child]);
        drive.Setup(item => item.ListDeletionChildrenAsync("design", default)).ReturnsAsync([]);
        var plan = await ProjectDriveDeletionPlanner.BuildAsync(7, ["design", "design"], drive.Object, options, default);
        Assert.Equal(new[] { "design", "project" }, plan.Select(item => item.ActionIdentifier));
        Assert.Equal(new[] { "project", "root" }, plan.Select(item => item.ExpectedParentId));
        Assert.Equal(new[] { 0, 1 }, plan.Select(item => item.Sequence));
    }

    [Theory]
    [InlineData(false, "nicon-test", "application/vnd.google-apps.folder")]
    [InlineData(true, "foreign-instance", "application/vnd.google-apps.folder")]
    [InlineData(true, "nicon-test", "application/pdf")]
    public async Task Plan_BlocksSharedForeignOrFileContents(bool owned, string instance, string mime)
    {
        var drive = new Mock<IGoogleDriveAdapter>();
        drive.Setup(item => item.ListDeletionChildrenAsync("root", default)).ReturnsAsync([]);
        var root = Folder("project", "root", ProjectDriveFolderService.CreateProjectIdentity(options.InstanceId, 7));
        var child = Folder("foreign", "project", ProjectDriveFolderService.CreatePathIdentity(instance, 7, "Design"))
            with
        { IsOwnedByMe = owned, MimeType = mime };
        drive.Setup(item => item.GetMetadataAsync("project", default)).ReturnsAsync(root);
        drive.Setup(item => item.ListDeletionChildrenAsync("project", default)).ReturnsAsync([child]);
        await Assert.ThrowsAnyAsync<Exception>(() =>
            ProjectDriveDeletionPlanner.BuildAsync(7, ["project"], drive.Object, options, default));
        drive.Verify(item => item.PermanentDeleteOwnedAsync(It.IsAny<DrivePermanentDeleteRequest>(), default), Times.Never);
    }

    [Fact]
    public async Task Plan_OfflineWithBindings_RequiresConnection()
    {
        options.Enabled = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => ProjectDriveDeletionPlanner.BuildAsync(
            7, ["project"], Mock.Of<IGoogleDriveAdapter>(), options, default));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Plan_MissingBinding_FindsRemainingRootOrTreatsMissingTreeAsSuccess(bool rootSurvives)
    {
        var drive = new Mock<IGoogleDriveAdapter>();
        var root = Folder("project", "root", ProjectDriveFolderService.CreateProjectIdentity(options.InstanceId, 7));
        drive.Setup(item => item.GetMetadataAsync("missing-category", default)).ReturnsAsync((DriveItem?)null);
        drive.Setup(item => item.ListDeletionChildrenAsync("root", default))
            .ReturnsAsync(rootSurvives ? [root] : []);
        drive.Setup(item => item.ListDeletionChildrenAsync("project", default)).ReturnsAsync([]);
        var plan = await ProjectDriveDeletionPlanner.BuildAsync(7, ["missing-category"], drive.Object, options, default);
        Assert.Equal(rootSurvives ? new[] { "project" } : [], plan.Select(item => item.ActionIdentifier));
    }

    [Fact]
    public async Task DurableProjectDelete_ResumesAfterChildDeletedAndFinalizesRoot()
    {
        using var db = DbContextFactory.Create();
        var project = new OperationalProject { Code = "PJ-DRIVE-DELETE", Name = "NICON nhà mẫu", CustomerId = 10 };
        db.OperationalProjects.Add(project);
        await db.SaveChangesAsync();
        var root = Folder("project", "root", ProjectDriveFolderService.CreateProjectIdentity(options.InstanceId, project.Id));
        var child = Folder("design", "project", ProjectDriveFolderService.CreatePathIdentity(options.InstanceId, project.Id, "Design"));
        db.ProjectDriveFolders.Add(new ProjectDriveFolder
        {
            OperationalProjectId = project.Id,
            Category = ProjectDocumentCategory.DesignBasic,
            DriveFolderId = child.Id
        });
        await db.SaveChangesAsync();
        var drive = new Mock<IGoogleDriveAdapter>();
        drive.Setup(item => item.ListDeletionChildrenAsync("root", default)).ReturnsAsync([]);
        var remote = new Dictionary<string, DriveItem> { [root.Id] = root, [child.Id] = child };
        drive.Setup(item => item.GetMetadataAsync(It.IsAny<string>(), default))
            .ReturnsAsync((string id, CancellationToken _) => remote.GetValueOrDefault(id));
        drive.Setup(item => item.ListDeletionChildrenAsync("project", default)).ReturnsAsync([child]);
        drive.Setup(item => item.ListDeletionChildrenAsync("design", default)).ReturnsAsync([]);
        var attempts = 0;
        drive.Setup(item => item.PermanentDeleteOwnedAsync(It.IsAny<DrivePermanentDeleteRequest>(), default))
            .Returns((DrivePermanentDeleteRequest request, CancellationToken _) =>
            {
                if (request.FileId == root.Id && ++attempts == 1) throw new IOException("Drive unavailable");
                remote.Remove(request.FileId);
                return Task.CompletedTask;
            });
        var plans = new ProjectHardDeletePlanService(db, drive.Object, new TestGoogleDriveSettingsStore(options));
        var plan = (await plans.ForOperationalProjectAsync(project.Id))!;
        Assert.True(plan.Impact.CanDelete);
        var permissions = new Mock<IPermissionService>();
        permissions.Setup(item => item.HasAsync(It.IsAny<int>(), It.IsAny<string>(), default)).ReturnsAsync(true);
        var handler = new OperationalProjectHardDeleteHandler(db, plans,
            Mock.Of<IProjectDocumentStagingService>(), permissions.Object);
        var operations = new HardDeleteOperationService(db, Mock.Of<IHardDeleteFileService>(), drive.Object,
            new HardDeleteResourceHandlerRegistry([handler]), NullLogger<HardDeleteOperationService>.Instance);
        var operation = await operations.CreateAsync(new CreateHardDeleteOperationRequest(
            EntityTypes.OperationalProject, project.Id.ToString(), project.Name,
            plan.Impact.PlanToken, project.Code, "7", plan.Items));
        var interrupted = await operations.ProcessAsync(operation.OperationId);
        Assert.NotEqual(HardDeleteOperationStatus.Completed, interrupted.Status);
        Assert.False(remote.ContainsKey(child.Id));
        Assert.True(await db.OperationalProjects.AnyAsync(item => item.Id == project.Id));
        var completed = await operations.ProcessAsync(operation.OperationId);
        Assert.Equal(HardDeleteOperationStatus.Completed, completed.Status);
        Assert.Empty(remote);
        Assert.False(await db.OperationalProjects.AnyAsync(item => item.Id == project.Id));
    }

    private static DriveItem Folder(string id, string parent, IReadOnlyDictionary<string, string> properties) =>
        new(id, id, "application/vnd.google-apps.folder", null, "1", null, null, properties,
            false, [parent], true, true);
}
