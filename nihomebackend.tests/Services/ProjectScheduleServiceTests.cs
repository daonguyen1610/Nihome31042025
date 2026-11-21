using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.Rbac;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

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
        Assert.Contains("bản tiến độ khác", error.Message);
    }

    [Fact]
    public async Task Submit_does_not_leave_pending_revision_when_email_cannot_be_queued()
    {
        await using var db = DbContextFactory.CreateRelational();
        var (parentNode, _) = SeedProject(db);
        var roleGroup = new RoleGroup { Code = RoleGroupCodes.Crm, LabelKey = "crm" };
        var permission = new Permission { Module = "crm.opportunities", Action = "manage" };
        var role = new Role { Code = RbacRoleCodes.SalesManager, Name = "Sales Manager", RoleGroup = roleGroup };
        role.RolePermissions.Add(new RolePermission { Permission = permission });
        db.Users.Add(new ApplicationUser
        {
            Id = 85,
            RoleEntity = role,
            PhoneNumber = "0900000085",
            Email = "approver@nihome.test",
        });
        db.SiteSettings.Add(new SiteSettings { ProjectScheduleEmailSubjectTemplate = new string('X', 201) });
        var revision = new ProjectScheduleRevision
        {
            OperationalProjectId = ProjectId,
            Version = 1,
            Status = ProjectScheduleRevisionStatus.Draft,
            CreatedByUserId = CallerId,
            RowVersion = [1, 2, 3],
            Items = [Item(parentNode, null, "1", "Móng", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30))],
        };
        db.ProjectScheduleRevisions.Add(revision);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await Assert.ThrowsAsync<ArgumentException>(() => service.SubmitAsync(ProjectId, revision.Id,
            new SubmitProjectScheduleRevisionRequest
            {
                ApproverUserId = 85,
                RowVersion = Convert.ToBase64String(revision.RowVersion),
            }, CallerId, default));

        db.ChangeTracker.Clear();
        var persisted = await db.ProjectScheduleRevisions.AsNoTracking().SingleAsync(item => item.Id == revision.Id);
        Assert.Equal(ProjectScheduleRevisionStatus.Draft, persisted.Status);
        Assert.Null(persisted.SubmittedAt);
        Assert.False(await db.ProjectScheduleEmails.AnyAsync(item => item.RevisionId == revision.Id));
    }

    [Fact]
    public async Task Submit_to_same_approver_does_not_queue_a_duplicate_notice()
    {
        await using var db = DbContextFactory.CreateRelational();
        var (parentNode, _) = SeedProject(db);
        var roleGroup = new RoleGroup { Code = RoleGroupCodes.Crm, LabelKey = "crm" };
        var permission = new Permission { Module = "crm.opportunities", Action = "manage" };
        var role = new Role { Code = RbacRoleCodes.SalesManager, Name = "Sales Manager", RoleGroup = roleGroup };
        role.RolePermissions.Add(new RolePermission { Permission = permission });
        db.Users.Add(new ApplicationUser
        {
            Id = 85,
            RoleEntity = role,
            PhoneNumber = "0900000085",
            Email = "approver@nihome.test",
        });
        var revision = new ProjectScheduleRevision
        {
            OperationalProjectId = ProjectId,
            Version = 1,
            Status = ProjectScheduleRevisionStatus.Draft,
            CreatedByUserId = CallerId,
            RowVersion = [1, 2, 3],
            Items = [Item(parentNode, null, "1", "Móng", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30))],
        };
        db.ProjectScheduleRevisions.Add(revision);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var preview = await service.PreviewEmailAsync(ProjectId, revision.Id, "Submit", 85, CallerId, default);
        await service.SubmitAsync(ProjectId, revision.Id, new SubmitProjectScheduleRevisionRequest
        {
            ApproverUserId = 85,
            RowVersion = Convert.ToBase64String(revision.RowVersion),
            PreviewToken = preview.PreviewToken,
        }, CallerId, default);

        db.ChangeTracker.Clear();
        var pending = await db.ProjectScheduleRevisions.SingleAsync(item => item.Id == revision.Id);
        var reassignedPreview = await service.PreviewEmailAsync(ProjectId, revision.Id, "Submit", 85, CallerId, default);
        await service.SubmitAsync(ProjectId, revision.Id, new SubmitProjectScheduleRevisionRequest
        {
            ApproverUserId = 85,
            RowVersion = Convert.ToBase64String(pending.RowVersion),
            PreviewToken = reassignedPreview.PreviewToken,
        }, CallerId, default);

        Assert.Equal(1, await db.ProjectScheduleEmails.CountAsync(item => item.RevisionId == revision.Id));
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

    [Fact]
    public async Task ImportedMsProjectSchedule_SavesAsADraftWithItsLinks()
    {
        await using var db = CreateDb();
        SeedProject(db);
        var service = CreateService(db);
        await using var file = File.OpenRead(Path.Combine(
            AppContext.BaseDirectory, "TestData", "MsProject", "nicon-bma-design-schedule.xml"));

        var preview = await service.PreviewImportAsync(ProjectId, file, "BMA.xml", CallerId, default);
        // Nothing is saved until the user saves the draft.
        Assert.Empty(db.ProjectScheduleRevisions);
        var saved = await service.CreateRevisionAsync(ProjectId, new SaveProjectScheduleRevisionRequest
        {
            ChangeReason = "Nhập từ MS Project: BMA.xml",
            Items = preview.Items.Select(item => new SaveProjectScheduleItemRequest
            {
                NodeId = item.NodeId,
                ParentNodeId = item.ParentNodeId,
                WbsCode = item.WbsCode,
                Title = item.Title,
                PlannedStart = item.PlannedStart,
                PlannedEnd = item.PlannedEnd,
                SortOrder = item.SortOrder,
                IsMilestone = item.IsMilestone,
                Predecessors = item.Predecessors.Select(link => new SaveProjectScheduleLinkRequest
                {
                    PredecessorNodeId = link.PredecessorNodeId,
                    Type = link.Type,
                    LagDays = link.LagDays,
                }).ToList(),
            }).ToList(),
        }, CallerId, default);

        var draft = Assert.Single(saved.Revisions);
        Assert.Equal(46, draft.Items.Count);
        Assert.Equal(preview.Items.Sum(item => item.Predecessors.Count), draft.Items.Sum(item => item.Predecessors.Count));
    }

    [Fact]
    public async Task ReimportedSchedule_KeepsTheNodesOfMatchingWbsCodes()
    {
        await using var db = CreateDb();
        var (parentNode, childNode) = SeedProject(db);
        db.ProjectScheduleRevisions.Add(new ProjectScheduleRevision
        {
            OperationalProjectId = ProjectId,
            Version = 1,
            Status = ProjectScheduleRevisionStatus.Published,
            CreatedByUserId = CallerId,
            RowVersion = [1],
            Items =
            [
                Item(parentNode, null, "1", "Móng", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30)),
                Item(childNode, parentNode, "1.1", "Ép cọc", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 10)),
            ],
        });
        await db.SaveChangesAsync();
        const string xml = """
            <Project xmlns="http://schemas.microsoft.com/project"><Tasks>
              <Task><UID>10</UID><OutlineLevel>1</OutlineLevel><WBS>1</WBS><Name>Móng</Name><Start>2026-06-01T08:00:00</Start><Finish>2026-07-05T17:00:00</Finish></Task>
              <Task><UID>11</UID><OutlineLevel>2</OutlineLevel><WBS>1.1</WBS><Name>Ép cọc</Name><Start>2026-06-01T08:00:00</Start><Finish>2026-06-15T17:00:00</Finish></Task>
              <Task><UID>12</UID><OutlineLevel>2</OutlineLevel><WBS>1.2</WBS><Name>Đài móng</Name><Start>2026-06-16T08:00:00</Start><Finish>2026-07-05T17:00:00</Finish>
                <PredecessorLink><PredecessorUID>11</PredecessorUID><Type>1</Type></PredecessorLink></Task>
            </Tasks></Project>
            """;

        var preview = await CreateService(db).PreviewImportAsync(
            ProjectId, new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml)), "v2.xml", CallerId, default);

        // The updated plan keeps the existing nodes, so work linked to them stays linked.
        Assert.Equal(parentNode, preview.Items.Single(item => item.WbsCode == "1").NodeId);
        Assert.Equal(childNode, preview.Items.Single(item => item.WbsCode == "1.1").NodeId);
        var newTask = preview.Items.Single(item => item.WbsCode == "1.2");
        Assert.Equal(parentNode, newTask.ParentNodeId);
        Assert.Equal(childNode, newTask.Predecessors.Single().PredecessorNodeId);
    }

    [Theory]
    [InlineData(true, "vòng lặp")]
    [InlineData(false, "hai liên kết")]
    public async Task Links_ThatRepeatOrGoRound_AreRefused(bool goesRound, string reason)
    {
        await using var db = CreateDb();
        var (first, second) = SeedProject(db);
        db.ProjectScheduleRevisions.RemoveRange(db.ProjectScheduleRevisions);
        await db.SaveChangesAsync();
        var request = new SaveProjectScheduleRevisionRequest
        {
            ChangeReason = "Liên kết",
            Items =
            [
                SaveItem(first, null, "1", "Móng", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 10)),
                SaveItem(second, null, "2", "Kết cấu", new DateOnly(2026, 6, 11), new DateOnly(2026, 6, 20)),
            ],
        }.WithLinks(goesRound ? [(first, second), (second, first)] : [(first, second), (first, second)]);

        var error = await Assert.ThrowsAsync<ProjectScheduleOperationException>(() =>
            CreateService(db).CreateRevisionAsync(ProjectId, request, CallerId, default));
        Assert.Contains(reason, error.Message);
    }

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
            Mock.Of<INotificationService>(), new EphemeralDataProtectionProvider(), NullLogger<ProjectScheduleService>.Instance);
    }
}

internal static class ScheduleRequestLinks
{
    /// <summary>Adds "To after From" finish-to-start links.</summary>
    public static SaveProjectScheduleRevisionRequest WithLinks(
        this SaveProjectScheduleRevisionRequest request, params (Guid From, Guid To)[] links)
    {
        foreach (var (from, to) in links)
            request.Items.Single(item => item.NodeId == to).Predecessors.Add(
                new SaveProjectScheduleLinkRequest { PredecessorNodeId = from, Type = "FS" });
        return request;
    }
}
