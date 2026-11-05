using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;

namespace nihomebackend.tests.Services;

public sealed class ProjectScheduleServiceTests
{
    private const int ProjectId = 42;
    private const int CallerId = 7;

    [Fact]
    public async Task UpdateRevision_replaces_items_without_duplicate_key_failure()
    {
        await using var db = CreateDb();
        var (parentNode, childNode) = SeedProject(db);
        var revision = new ProjectScheduleRevision
        {
            OperationalProjectId = ProjectId,
            Version = 1,
            Status = ProjectScheduleRevisionStatus.Draft,
            CreatedByUserId = CallerId,
            RowVersion = [1, 2, 3],
            Items =
            [
                Item(parentNode, null, "1", "Móng", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30)),
                Item(childNode, parentNode, "1.1", "Ép cọc", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 10)),
            ],
        };
        db.ProjectScheduleRevisions.Add(revision);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var request = new SaveProjectScheduleRevisionRequest
        {
            ChangeReason = "Khách điều chỉnh",
            Items =
            [
                SaveItem(parentNode, null, "1", "Móng (điều chỉnh)", new DateOnly(2026, 6, 2), new DateOnly(2026, 6, 30)),
                SaveItem(childNode, parentNode, "1.1", "Ép cọc", new DateOnly(2026, 6, 2), new DateOnly(2026, 6, 12)),
            ],
        };

        var response = await service.UpdateRevisionAsync(ProjectId, revision.Id, request, CallerId, default);

        Assert.NotNull(response);
        var updated = await db.ProjectScheduleItems.AsNoTracking()
            .Where(item => item.RevisionId == revision.Id).ToListAsync();
        Assert.Equal(2, updated.Count);
        Assert.Contains(updated, item => item.NodeId == parentNode && item.Title == "Móng (điều chỉnh)");
    }

    [Fact]
    public async Task UpdateRevision_rejects_a_second_editable_revision()
    {
        await using var db = CreateDb();
        var (parentNode, childNode) = SeedProject(db);
        db.ProjectScheduleRevisions.AddRange(
            new ProjectScheduleRevision
            {
                OperationalProjectId = ProjectId,
                Version = 1,
                Status = ProjectScheduleRevisionStatus.Draft,
                CreatedByUserId = CallerId,
                RowVersion = [1],
                Items = [Item(parentNode, null, "1", "Móng", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30))],
            },
            new ProjectScheduleRevision
            {
                OperationalProjectId = ProjectId,
                Version = 2,
                Status = ProjectScheduleRevisionStatus.Draft,
                CreatedByUserId = CallerId,
                RowVersion = [2],
                Items = [Item(childNode, null, "2", "Thân", new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 30))],
            });
        await db.SaveChangesAsync();
        var second = await db.ProjectScheduleRevisions.SingleAsync(item => item.Version == 2);

        var service = CreateService(db);
        var error = await Assert.ThrowsAsync<ProjectScheduleOperationException>(() =>
            service.UpdateRevisionAsync(ProjectId, second.Id, new SaveProjectScheduleRevisionRequest
            {
                Items = [SaveItem(childNode, null, "2", "Thân", new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 30))],
            }, CallerId, default));

        Assert.Equal(409, error.StatusCode);
        Assert.Contains("revision nháp", error.Message);
    }

    private static (Guid Parent, Guid Child) SeedProject(AppDbContext db)
    {
        var parent = Guid.NewGuid();
        var child = Guid.NewGuid();
        db.OperationalProjects.Add(new OperationalProject
        {
            Id = ProjectId,
            Code = "PJ-TEST-001",
            Name = "Dự án kiểm thử",
            CustomerId = 1,
            CreatedByUserId = CallerId,
        });
        db.ProjectScheduleNodes.AddRange(
            new ProjectScheduleNode { Id = parent, OperationalProjectId = ProjectId },
            new ProjectScheduleNode { Id = child, OperationalProjectId = ProjectId });
        db.SaveChanges();
        return (parent, child);
    }

    private static ProjectScheduleItem Item(Guid nodeId, Guid? parentNodeId, string wbs, string title,
        DateOnly start, DateOnly end) => new()
        {
            NodeId = nodeId,
            ParentNodeId = parentNodeId,
            WbsCode = wbs,
            Title = title,
            PlannedStart = start,
            PlannedEnd = end,
        };

    private static SaveProjectScheduleItemRequest SaveItem(Guid nodeId, Guid? parentNodeId, string wbs,
        string title, DateOnly start, DateOnly end) => new()
        {
            NodeId = nodeId,
            ParentNodeId = parentNodeId,
            WbsCode = wbs,
            Title = title,
            PlannedStart = start,
            PlannedEnd = end,
        };

    private static AppDbContext CreateDb() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"project-schedule-{Guid.NewGuid()}")
            .Options);

    private static ProjectScheduleService CreateService(AppDbContext db)
    {
        var access = new Mock<IProjectAccessService>();
        access.Setup(service => service.CanViewOperationalProjectAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var permissions = new Mock<IPermissionService>();
        permissions.Setup(service => service.HasAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return new ProjectScheduleService(db, access.Object, permissions.Object,
            Mock.Of<INotificationService>(), NullLogger<ProjectScheduleService>.Instance);
    }
}
