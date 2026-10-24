using Microsoft.EntityFrameworkCore;
using Moq;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;
using NihomeBackend.Services.HardDelete;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public sealed class ProjectHardDeleteAuthorizationTests : IDisposable
{
    private readonly NihomeBackend.Data.AppDbContext db = DbContextFactory.Create();
    private readonly Mock<IPermissionService> permissions = new();

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ParentDeletion_CannotBypassDesignRecordPermission(bool operationalRoot, bool granted)
    {
        const int actor = 11;
        permissions.Setup(item => item.HasAsync(actor, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int _, string code, CancellationToken _) =>
                code is "design.projects.manage" or "operations.projects.manage" ||
                granted && code == "design.records.hard-delete");
        var root = new OperationalProject { Code = "NIC-DELETE-AUTH", Name = "NICON deletion boundary", CustomerId = 1, CreatedByUserId = actor };
        var design = new DesignProject { OperationalProject = root, ProjectCode = "DP-DELETE-AUTH", Name = root.Name, CustomerId = 1 };
        var concept = new ConceptOption { DesignProject = design, Name = "Phương án không đạt NICON", Status = ConceptOptionStatus.Discarded };
        db.Add(concept);
        await db.SaveChangesAsync();
        var plans = new Mock<IProjectHardDeletePlanService>();
        var plan = new ProjectHardDeletePlan(new DeletionImpactResponse { PlanToken = new string('a', 64) }, []);
        plans.Setup(item => item.ForDesignProjectAsync(design.Id, It.IsAny<CancellationToken>())).ReturnsAsync(plan);
        plans.Setup(item => item.ForOperationalProjectAsync(root.Id, It.IsAny<CancellationToken>())).ReturnsAsync(plan);
        var access = new Mock<IProjectAccessService>();
        access.Setup(item => item.CanManageDesignProjectAsync(actor, design.Id, It.IsAny<CancellationToken>(), null)).ReturnsAsync(true);
        var staging = new Mock<IProjectDocumentStagingService>();
        IHardDeleteResourceHandler handler = operationalRoot
            ? new OperationalProjectHardDeleteHandler(db, plans.Object, staging.Object, permissions.Object)
            : new DesignProjectHardDeleteHandler(db, plans.Object, staging.Object, permissions.Object, access.Object);
        if (granted) await handler.AuthorizeAsync(Context(operationalRoot ? root.Id : design.Id, actor));
        else await Assert.ThrowsAsync<HardDeleteAuthorizationException>(() =>
            handler.AuthorizeAsync(Context(operationalRoot ? root.Id : design.Id, actor)));
        Assert.True(await db.ConceptOptions.AnyAsync(item => item.Id == concept.Id));
        Assert.True(await db.OperationalProjects.AnyAsync(item => item.Id == root.Id));
        staging.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DesignHandler_WhenProjectScopeWasRevoked_RejectsProcessing()
    {
        const int requestedBy = 11;
        permissions.Setup(item => item.HasAsync(
                requestedBy, "design.projects.manage", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        permissions.Setup(item => item.HasAsync(
                requestedBy, "operations.projects.view.all", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var project = new DesignProject
        {
            ProjectCode = "DP-AUTH",
            Name = "Revoked design scope",
            CustomerId = 1,
            ProjectManagerUserId = 22,
            DesignLeadUserId = 22,
        };
        db.DesignProjects.Add(project);
        await db.SaveChangesAsync();
        var handler = new DesignProjectHardDeleteHandler(
            db,
            Mock.Of<IProjectHardDeletePlanService>(),
            Mock.Of<IProjectDocumentStagingService>(),
            permissions.Object,
            new ProjectAccessService(db, permissions.Object));

        var authorize = () => handler.AuthorizeAsync(Context(project.Id, requestedBy));

        await Assert.ThrowsAsync<HardDeleteAuthorizationException>(authorize);
        Assert.True(await db.DesignProjects.AnyAsync(item => item.Id == project.Id));
    }

    [Fact]
    public async Task OperationalHandler_WhenProjectScopeWasRevoked_RejectsProcessing()
    {
        const int requestedBy = 11;
        permissions.Setup(item => item.HasAsync(
                requestedBy, "operations.projects.manage", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        permissions.Setup(item => item.HasAsync(
                requestedBy, "operations.projects.view.all", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var project = new OperationalProject
        {
            Code = "OP-AUTH",
            Name = "Revoked operational scope",
            CustomerId = 1,
            ProjectManagerUserId = 22,
            CreatedByUserId = 22,
        };
        db.OperationalProjects.Add(project);
        await db.SaveChangesAsync();
        var handler = new OperationalProjectHardDeleteHandler(
            db,
            Mock.Of<IProjectHardDeletePlanService>(),
            Mock.Of<IProjectDocumentStagingService>(),
            permissions.Object);

        var authorize = () => handler.AuthorizeAsync(Context(project.Id, requestedBy));

        await Assert.ThrowsAsync<HardDeleteAuthorizationException>(authorize);
        Assert.True(await db.OperationalProjects.AnyAsync(item => item.Id == project.Id));
    }

    [Fact]
    public async Task DesignHandler_WhenPlanChanged_RejectsBeforeFinalization()
    {
        const int requestedBy = 11;
        permissions.Setup(item => item.HasAsync(
                requestedBy, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var project = new DesignProject
        {
            ProjectCode = "DP-STALE",
            Name = "Stale durable plan",
            CustomerId = 1,
        };
        db.DesignProjects.Add(project);
        await db.SaveChangesAsync();
        var plans = new Mock<IProjectHardDeletePlanService>();
        plans.Setup(item => item.ForDesignProjectAsync(project.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProjectHardDeletePlan(
                new DeletionImpactResponse { PlanToken = new string('b', 64) }, []));
        var handler = new DesignProjectHardDeleteHandler(
            db,
            plans.Object,
            Mock.Of<IProjectDocumentStagingService>(),
            permissions.Object,
            new ProjectAccessService(db, permissions.Object));

        var authorize = () => handler.AuthorizeAsync(Context(project.Id, requestedBy));

        await Assert.ThrowsAsync<DeletionPlanChangedException>(authorize);
        Assert.True(await db.DesignProjects.AnyAsync(item => item.Id == project.Id));
    }

    public void Dispose() => db.Dispose();

    private static HardDeleteResourceContext Context(int projectId, int requestedBy) => new(
        Guid.NewGuid(),
        "project",
        projectId.ToString(),
        new string('a', 64),
        requestedBy.ToString(),
        string.Empty,
        IsForwardRecovery: false);
}
