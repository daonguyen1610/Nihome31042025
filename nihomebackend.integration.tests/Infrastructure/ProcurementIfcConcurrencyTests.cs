using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;

namespace NihomeBackend.IntegrationTests.Infrastructure;

public sealed class ProcurementIfcConcurrencyTests
{
    [SqlEvidenceTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RecipientMutationRacingRelease_PreservesReleasedDistribution(bool removing)
    {
        await using var database = await ReceiptDatabase.CreateAsync();
        var barrier = new BeforeSave();
        await using var stale = database.Context(barrier);
        var service = new IfcReleaseService(stale, NullLogger<IfcReleaseService>.Instance);
        var pending = removing
            ? service.RemoveRecipientAsync(database.ReleaseId, database.RecipientId, database.UserId)
            : service.AddRecipientAsync(database.ReleaseId, new AddIfcReleaseRecipientRequest
            {
                Name = "NICON – Giám sát bổ sung",
                RecipientTypeCode = "supervisor",
            }, database.UserId);
        try
        {
            await barrier.Reached.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await using var winner = database.Context();
            var release = await winner.IfcReleases.SingleAsync();
            release.Status = IfcReleaseStatus.Released;
            release.ReleaseDate = DateTime.UtcNow;
            release.UpdatedAt = DateTime.UtcNow;
            await winner.SaveChangesAsync();
        }
        finally { barrier.Release.TrySetResult(); }
        await Assert.ThrowsAsync<CrmConcurrencyException>(() => pending);
        await using var verify = database.Context();
        (await verify.IfcReleaseRecipients.CountAsync()).Should().Be(1);
        (await verify.IfcReleases.SingleAsync()).Status.Should().Be(IfcReleaseStatus.Released);
    }

    [SqlEvidenceFact]
    public async Task Candidates_UsesSqlProjectionAndEffectiveRoleGrants()
    {
        await using var database = await ReceiptDatabase.CreateAsync();
        await using var db = database.Context();
        RbacSeeder.Seed(db);
        var user = await db.Users.SingleAsync(x => x.Id == database.UserId);
        user.RoleEntityId = await db.Roles.Where(x => x.Code == "PROCUREMENT").Select(x => x.Id).SingleAsync();
        db.OperationalProjectMembers.Add(new OperationalProjectMember
        {
            OperationalProjectId = database.ProjectId,
            UserId = database.UserId,
            Position = "Cung ứng nhà xưởng",
            StartedAt = DateTime.UtcNow.AddDays(-1),
        });
        await db.SaveChangesAsync();
        var designId = await db.DesignProjects.Select(x => x.Id).SingleAsync();
        var candidates = await new ProcurementIfcService(db, new PermissionService(db)).CandidatesAsync(designId, default);
        candidates.Should().ContainSingle(x => x.UserId == database.UserId);
    }

    [SqlEvidenceFact]
    public async Task ConcurrentAcknowledgement_KeepsFirstActorTimestampAndNote()
    {
        await using var database = await ReceiptDatabase.CreateAsync();
        await using (var setup = database.Context())
        {
            var release = await setup.IfcReleases.SingleAsync();
            release.Status = IfcReleaseStatus.Released;
            release.ReleaseDate = DateTime.UtcNow;
            await setup.SaveChangesAsync();
        }
        var barrier = new BeforeSave();
        await using var stale = database.Context(barrier);
        var version = await stale.IfcReleaseRecipients.AsNoTracking().Select(x => x.RowVersion).SingleAsync();
        var service = new ProcurementIfcService(stale, new PermissionService(stale));
        var pending = service.AcknowledgeAsync(database.ProjectId, database.RecipientId, database.UserId,
            new AcknowledgeIfcReleaseRecipientRequest { RowVersion = Convert.ToBase64String(version), AcknowledgementNote = "Retry" }, default);
        ProcurementIfcAcknowledgement? first;
        try
        {
            await barrier.Reached.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await using var winner = database.Context();
            first = await new ProcurementIfcService(winner, new PermissionService(winner)).AcknowledgeAsync(
                database.ProjectId, database.RecipientId, database.UserId,
                new AcknowledgeIfcReleaseRecipientRequest { RowVersion = Convert.ToBase64String(version), AcknowledgementNote = "NICON – Đã nhận bản phát hành" }, default);
        }
        finally { barrier.Release.TrySetResult(); }
        var replay = await pending;
        first!.Recorded.Should().BeTrue();
        replay!.Recorded.Should().BeFalse();
        replay.Packet.AcknowledgedAt.Should().Be(first.Packet.AcknowledgedAt);
        replay.Packet.AcknowledgedAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
        replay.Packet.AcknowledgedByUserId.Should().Be(database.UserId);
        replay.Packet.AcknowledgementNote.Should().Be(first.Packet.AcknowledgementNote);
    }

    [SqlEvidenceFact]
    public async Task AcknowledgementRacingDeletion_DoesNotReportReceipt()
    {
        await using var database = await ReceiptDatabase.CreateAsync();
        await using (var setup = database.Context())
        {
            var release = await setup.IfcReleases.SingleAsync();
            release.Status = IfcReleaseStatus.Released;
            release.ReleaseDate = DateTime.UtcNow;
            await setup.SaveChangesAsync();
        }
        var barrier = new BeforeSave();
        await using var stale = database.Context(barrier);
        var version = await stale.IfcReleaseRecipients.AsNoTracking().Select(x => x.RowVersion).SingleAsync();
        var pending = new ProcurementIfcService(stale, new PermissionService(stale)).AcknowledgeAsync(
            database.ProjectId, database.RecipientId, database.UserId,
            new AcknowledgeIfcReleaseRecipientRequest { RowVersion = Convert.ToBase64String(version) }, default);
        try
        {
            await barrier.Reached.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await using var winner = database.Context();
            // Simulate removal of the owned recipient by aggregate deletion.
            winner.IfcReleaseRecipients.Remove(await winner.IfcReleaseRecipients.SingleAsync());
            await winner.SaveChangesAsync();
        }
        finally { barrier.Release.TrySetResult(); }
        await Assert.ThrowsAsync<CrmConcurrencyException>(() => pending);
    }

    private sealed class BeforeSave : SaveChangesInterceptor
    {
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Reached.TrySetResult();
            await Release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            return result;
        }
    }

    private sealed class ReceiptDatabase(string connection) : IAsyncDisposable
    {
        private const string Prefix = "NiconIfcReceiptTest_";
        public int ReleaseId { get; private set; }
        public int RecipientId { get; private set; }
        public int ProjectId { get; private set; }
        public int UserId { get; private set; }
        public AppDbContext Context(SaveChangesInterceptor? interceptor = null)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection);
            if (interceptor is not null) options.AddInterceptors(interceptor);
            return new AppDbContext(options.Options);
        }
        public static async Task<ReceiptDatabase> CreateAsync()
        {
            var builder = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("NICON_SQL_TEST_CONNECTION"))
            { InitialCatalog = $"{Prefix}{Guid.NewGuid():N}", Pooling = false };
            var database = new ReceiptDatabase(builder.ConnectionString);
            try
            {
                await using var db = database.Context();
                await db.Database.EnsureCreatedAsync();
                var user = new ApplicationUser { PhoneNumber = "0911000016", FullName = "NICON – Cung ứng công trình", IsActive = true };
                var project = new OperationalProject { Code = "PJ-IFC-RECEIPT", Name = "NICON – Nhà xưởng Long Thành", Customer = new Customer { Name = "CĐT nhà xưởng Long Thành" } };
                var release = new IfcRelease
                {
                    ReleaseNumber = "IFC-KC-R01",
                    Title = "Phát hành kết cấu móng nhà xưởng",
                    DesignProject = new DesignProject { ProjectCode = "DP-IFC-RECEIPT", Name = "Hồ sơ kết cấu", Customer = project.Customer, OperationalProject = project },
                    Recipients = [new IfcReleaseRecipient { Name = user.FullName!, RecipientTypeCode = "supervisor", RecipientUser = user }],
                };
                db.Add(release);
                db.MasterDataOptions.Add(new MasterDataOption { Category = "ifc_recipient_type", Code = "supervisor", Name = "Giám sát", IsActive = true });
                await db.SaveChangesAsync();
                database.UserId = user.Id;
                database.ProjectId = project.Id;
                database.ReleaseId = release.Id;
                database.RecipientId = release.Recipients.Single().Id;
                return database;
            }
            catch { await database.DisposeAsync(); throw; }
        }
        public async ValueTask DisposeAsync()
        {
            if (!new SqlConnectionStringBuilder(connection).InitialCatalog.StartsWith(Prefix, StringComparison.Ordinal))
                throw new InvalidOperationException("Only isolated IFC receipt test databases may be deleted.");
            await using var db = Context();
            await db.Database.EnsureDeletedAsync();
        }
    }
}
