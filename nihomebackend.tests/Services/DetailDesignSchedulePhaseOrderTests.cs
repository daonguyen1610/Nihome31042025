using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public sealed class DetailDesignSchedulePhaseOrderTests
{
    // A relational provider is required: the stage is stored as text, so ordering in SQL is alphabetical.
    // The InMemory provider orders by the enum value and would hide that.
    [Fact]
    public async Task GetAsync_ReturnsPhasesInDesignOrder_WhenTheDatabaseStoresStagesAsText()
    {
        const int projectId = 1;
        const int designProjectId = 1;
        const int callerId = 1;
        using var db = DbContextFactory.CreateRelational();
        foreach (var code in new[]
                 {
                     DesignSchedulePhaseCode.ShopDrawing,
                     DesignSchedulePhaseCode.BasicDesign,
                     DesignSchedulePhaseCode.Concept,
                 })
        {
            db.DesignSchedulePhases.Add(new DesignSchedulePhase
            {
                OperationalProjectId = projectId,
                DesignProjectId = designProjectId,
                Code = code,
                PlannedStart = new DateOnly(2026, 9, 1),
                PlannedEnd = new DateOnly(2026, 10, 1),
                Weight = 33,
                CreatedByUserId = callerId,
                UpdatedByUserId = callerId,
            });
        }
        db.DesignProjects.Add(new DesignProject
        {
            Id = designProjectId,
            OperationalProjectId = projectId,
            ProjectCode = "DP-ORDER-1",
            Name = "Phase order",
            CustomerId = 1,
        });
        await db.SaveChangesAsync();
        var access = new Mock<IProjectAccessService>();
        access.Setup(item => item.CanViewOperationalProjectAsync(callerId, projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        access.Setup(item => item.CanViewDesignProjectAsync(callerId, designProjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var service = new DetailDesignScheduleService(db, access.Object,
            Mock.Of<INotificationService>(), NullLogger<DetailDesignScheduleService>.Instance);

        var response = await service.GetAsync(projectId,
            new DesignScheduleQuery { DesignProjectId = designProjectId }, callerId, default);

        Assert.NotNull(response);
        Assert.Equal(["Concept", "BasicDesign", "ShopDrawing"], response.Phases.Select(phase => phase.Code));
    }
}
