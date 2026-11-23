using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.Rbac;
using NihomeBackend.Services;

namespace NihomeBackend.IntegrationTests.Infrastructure;

// SQL Server enforces the disabled-metadata check constraint and owns rowversion
// generation; InMemory cannot prove either invariant.
public sealed class DepartmentWorkTaskDeletionConcurrencyTests
{
    [SqlEvidenceFact]
    public async Task CancellingParentWithDisabledSubtree_KeepsRetentionMetadata()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var db = fixture.Context();
        var service = fixture.Service(db);

        await service.UpdateProgressAsync(fixture.PackageId, fixture.ParentTaskId,
            new UpdateDepartmentWorkTaskProgressRequest
            {
                Status = "Cancelled",
                ProgressPercent = 0,
                RowVersion = fixture.ParentRowVersion,
            }, fixture.CallerId);

        await using var check = fixture.Context();
        (await check.DepartmentWorkTasks.SingleAsync(item => item.Id == fixture.ParentTaskId)).Status
            .Should().Be(WorkTaskStatus.Cancelled);
        (await check.DepartmentWorkTasks.SingleAsync(item => item.Id == fixture.OpenChildId)).Status
            .Should().Be(WorkTaskStatus.Cancelled);
        var disabled = await check.DepartmentWorkTasks.SingleAsync(item => item.Id == fixture.DisabledChildId);
        disabled.Status.Should().Be(WorkTaskStatus.Disabled);
        disabled.DisabledAt.Should().NotBeNull();
        disabled.DisabledByUserId.Should().Be(fixture.CallerId);
        disabled.DisabledRootTaskId.Should().Be(fixture.DisabledChildId);
        disabled.StatusBeforeDisabled.Should().Be(WorkTaskStatus.Planned);
    }

    [SqlEvidenceFact]
    public async Task PermanentlyDeletingWithStaleRowVersion_RollsBackTaskAndAttachment()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var db = fixture.Context();
        var service = fixture.Service(db);
        var impact = await service.GetTaskDeletionImpactAsync(fixture.PackageId, fixture.DisabledChildId,
            fixture.CallerId);
        impact.Should().NotBeNull();
        impact!.CanDelete.Should().BeTrue();

        var act = () => service.PermanentlyDeleteTaskAsync(fixture.PackageId, fixture.DisabledChildId,
            new ConfirmDeletionRequest
            {
                Confirmation = impact.RequiredConfirmation,
                PlanToken = impact.PlanToken,
                RowVersion = Convert.ToBase64String(new byte[8]),
            }, fixture.CallerId);

        await act.Should().ThrowAsync<CrmConcurrencyException>();

        await using var check = fixture.Context();
        (await check.DepartmentWorkTasks.AnyAsync(item => item.Id == fixture.DisabledChildId)).Should().BeTrue();
        (await check.DepartmentWorkTaskAttachments.AnyAsync(item => item.WorkTaskId == fixture.DisabledChildId))
            .Should().BeTrue();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string _connection;

        private Fixture(string connection)
        {
            _connection = connection;
        }

        public int CallerId { get; private set; }
        public int PackageId { get; private set; }
        public int ParentTaskId { get; private set; }
        public int OpenChildId { get; private set; }
        public int DisabledChildId { get; private set; }
        public string ParentRowVersion { get; private set; } = string.Empty;

        public AppDbContext Context() =>
            new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(_connection).Options);

        // Only the database-backed task rules are exercised here; the unused
        // collaborators are not reached by cancellation or permanent deletion.
        public DepartmentWorkPackageService Service(AppDbContext db) =>
            new(db, new ProjectAccessService(db, new PermissionService(db)), null!,
                NullLogger<DepartmentWorkPackageService>.Instance, null!, null!);

        public async ValueTask DisposeAsync()
        {
            await using var db = Context();
            await db.Database.EnsureDeletedAsync();
        }

        public static async Task<Fixture> CreateAsync()
        {
            var builder = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("NICON_SQL_TEST_CONNECTION"))
            {
                InitialCatalog = "NiconWorkTaskDeletion_" + Guid.NewGuid().ToString("N"),
                Pooling = false,
            };
            var fixture = new Fixture(builder.ConnectionString);
            await using var db = fixture.Context();
            await db.Database.EnsureCreatedAsync();

            var group = new RoleGroup { Code = "PROCUREMENT", LabelKey = "rbac.group.procurement" };
            var caller = new ApplicationUser
            {
                PhoneNumber = "0911000031",
                Email = "warehouse.lead@nicon.test",
                FullName = "Trưởng ca kho NICON",
                IsActive = true,
            };
            db.AddRange(group, caller);
            await db.SaveChangesAsync();
            fixture.CallerId = caller.Id;

            var package = new DepartmentWorkPackage
            {
                Code = "WP-SQL-01",
                Kind = WorkPackageKind.Internal,
                Purpose = WorkPackagePurpose.General,
                Name = "Điều phối vật tư nhà máy NICON",
                DepartmentRoleGroupId = group.Id,
                LeadUserId = caller.Id,
                Status = WorkPackageStatus.Active,
                PlannedStart = DateTime.UtcNow.Date,
                PlannedEnd = DateTime.UtcNow.Date.AddDays(14),
                CreatedByUserId = caller.Id,
                UpdatedByUserId = caller.Id,
            };
            db.DepartmentWorkPackages.Add(package);
            await db.SaveChangesAsync();
            fixture.PackageId = package.Id;

            var parent = AddTask(package.Id, group.Id, caller.Id, null, "SUP-01", "Chuẩn bị vật tư");
            db.DepartmentWorkTasks.Add(parent);
            await db.SaveChangesAsync();
            fixture.ParentTaskId = parent.Id;

            var openChild = AddTask(package.Id, group.Id, caller.Id, parent.Id, "SUP-01.1", "Đối chiếu khối lượng");
            var disabledChild = AddTask(package.Id, group.Id, caller.Id, parent.Id, "SUP-01.2", "Hồ sơ vật tư đã hủy");
            db.DepartmentWorkTasks.AddRange(openChild, disabledChild);
            await db.SaveChangesAsync();
            fixture.OpenChildId = openChild.Id;
            fixture.DisabledChildId = disabledChild.Id;

            disabledChild.Status = WorkTaskStatus.Disabled;
            disabledChild.DisabledAt = DateTime.UtcNow;
            disabledChild.DisabledByUserId = caller.Id;
            disabledChild.DisabledRootTaskId = disabledChild.Id;
            disabledChild.StatusBeforeDisabled = WorkTaskStatus.Planned;
            db.DepartmentWorkTaskAttachments.Add(new DepartmentWorkTaskAttachment
            {
                WorkTaskId = disabledChild.Id,
                Path = $"/files/business-documents/work-tasks/{Guid.NewGuid():N}.pdf",
                OriginalFileName = "bien-ban-doi-chieu.pdf",
                FileSize = 128,
                ContentType = "application/pdf",
                CreatedByUserId = caller.Id,
                DeletedAt = DateTime.UtcNow,
                StorageDeletedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();

            fixture.ParentRowVersion = CrmConcurrency.Encode(
                await db.DepartmentWorkTasks.AsNoTracking()
                    .Where(item => item.Id == parent.Id)
                    .Select(item => item.RowVersion)
                    .SingleAsync());
            return fixture;
        }

        private static DepartmentWorkTask AddTask(
            int packageId, int groupId, int callerId, int? parentId, string wbsCode, string title) => new()
            {
                WorkPackageId = packageId,
                ParentTaskId = parentId,
                WbsCode = wbsCode,
                Title = title,
                DepartmentRoleGroupId = groupId,
                AssigneeUserId = callerId,
                Status = WorkTaskStatus.Planned,
                PlannedStart = DateTime.UtcNow.Date,
                PlannedEnd = DateTime.UtcNow.Date.AddDays(5),
                CreatedByUserId = callerId,
                UpdatedByUserId = callerId,
            };
    }
}
