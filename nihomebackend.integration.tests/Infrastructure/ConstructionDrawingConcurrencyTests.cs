using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;

namespace NihomeBackend.IntegrationTests.Infrastructure;

public sealed class ConstructionDrawingConcurrencyTests
{
    [SqlEvidenceFact]
    public async Task ConcurrentIdenticalAdd_ReturnsConflictAndPreservesWinner()
    {
        await using var database = await DrawingDatabase.CreateAsync();
        var barrier = new BeforeSave();
        await using var stale = database.Context(barrier);
        var staleService = new ConstructionTaskService(stale, NullLogger<ConstructionTaskService>.Instance);
        var initial = (await staleService.GetDrawingsAsync(database.TaskId))!;
        var request = new SetConstructionTaskDrawingsRequest
        {
            Version = initial.Version,
            IfcReleaseItemIds = [database.ItemId],
        };
        var pending = staleService.SetDrawingsAsync(database.TaskId, request, database.UserId);
        Guid winnerVersion;
        try
        {
            await barrier.Reached.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await using var winning = database.Context();
            var winner = await new ConstructionTaskService(winning, NullLogger<ConstructionTaskService>.Instance)
                .SetDrawingsAsync(database.TaskId, request, database.UserId);
            winnerVersion = winner!.Version;
        }
        finally { barrier.Release.TrySetResult(); }

        // A SQL unique-key violation must not escape as an unhandled 500.
        await Assert.ThrowsAsync<CrmConcurrencyException>(() => pending);
        await using var verify = database.Context();
        var service = new ConstructionTaskService(verify, NullLogger<ConstructionTaskService>.Instance);
        var current = (await service.GetDrawingsAsync(database.TaskId))!;
        current.Version.Should().Be(winnerVersion);
        current.Linked.Should().ContainSingle(x => x.IfcReleaseItemId == database.ItemId);
        var retry = (await service.SetDrawingsAsync(database.TaskId, request, database.UserId))!;
        retry.Version.Should().Be(winnerVersion);
        (await verify.ConstructionTaskDrawings.CountAsync()).Should().Be(1);
    }

    [SqlEvidenceFact]
    public async Task ConcurrentTaskDeletion_ReturnsConflictAndPreservesDrawing()
    {
        await using var database = await DrawingDatabase.CreateAsync();
        var barrier = new BeforeSave();
        await using var stale = database.Context(barrier);
        var service = new ConstructionTaskService(stale, NullLogger<ConstructionTaskService>.Instance);
        var initial = (await service.GetDrawingsAsync(database.TaskId))!;
        var pending = service.SetDrawingsAsync(database.TaskId, new SetConstructionTaskDrawingsRequest
        {
            Version = initial.Version,
            IfcReleaseItemIds = [database.ItemId],
        }, database.UserId);
        try
        {
            await barrier.Reached.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await using var deleting = database.Context();
            (await new ConstructionTaskService(deleting, NullLogger<ConstructionTaskService>.Instance)
                .DeleteAsync(database.TaskId)).Should().BeTrue();
        }
        finally { barrier.Release.TrySetResult(); }
        await Assert.ThrowsAsync<CrmConcurrencyException>(() => pending);
        await using var verify = database.Context();
        (await verify.ConstructionTasks.AnyAsync()).Should().BeFalse();
        (await verify.ConstructionTaskDrawings.AnyAsync()).Should().BeFalse();
        (await verify.IfcReleaseItems.AnyAsync(x => x.Id == database.ItemId)).Should().BeTrue();
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

    private sealed class DrawingDatabase(string connection) : IAsyncDisposable
    {
        private const string CatalogPrefix = "NiconDrawingTest_";
        public int TaskId { get; private set; }
        public int ItemId { get; private set; }
        public int UserId { get; private set; }

        public AppDbContext Context(SaveChangesInterceptor? interceptor = null)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection);
            if (interceptor is not null) options.AddInterceptors(interceptor);
            return new AppDbContext(options.Options);
        }

        public static async Task<DrawingDatabase> CreateAsync()
        {
            var builder = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("NICON_SQL_TEST_CONNECTION"))
            { InitialCatalog = $"{CatalogPrefix}{Guid.NewGuid():N}", Pooling = false };
            var database = new DrawingDatabase(builder.ConnectionString);
            try
            {
                await using var db = database.Context();
                await db.Database.EnsureCreatedAsync();
                var user = new ApplicationUser { PhoneNumber = "0911000019", FullName = "NICON site commander", IsActive = true };
                var customer = new Customer { Name = "An Phu industrial project owner" };
                db.AddRange(user, customer);
                await db.SaveChangesAsync();
                database.UserId = user.Id;
                var project = new DesignProject
                {
                    ProjectCode = "DP-IFC-CONCURRENCY",
                    Name = "An Phu foundation construction",
                    CustomerId = customer.Id,
                    OperationalProject = new OperationalProject
                    {
                        Code = "PJ-IFC-CONCURRENCY",
                        Name = "An Phu industrial building",
                        CustomerId = customer.Id,
                    },
                };
                var task = new ConstructionTask
                {
                    DesignProject = project,
                    TaskCode = "FOUNDATION-F1",
                    Wbs = "1.1.1",
                    Name = "Install foundation reinforcement F1",
                    PlannedStart = new DateOnly(2026, 11, 1),
                    PlannedEnd = new DateOnly(2026, 11, 10),
                };
                var item = new IfcReleaseItem
                {
                    IfcRelease = new IfcRelease
                    {
                        DesignProject = project,
                        ReleaseNumber = "IFC-FOUNDATION-01",
                        Title = "Foundation reinforcement IFC",
                        Status = IfcReleaseStatus.Released,
                        ReleaseDate = DateTime.UtcNow,
                    },
                    ShopDrawing = new ShopDrawing
                    {
                        DesignProject = project,
                        DrawingCode = "KC-F1-001",
                        DisciplineCode = "structure",
                        ConstructionItem = "Foundation F1",
                        Title = "Foundation F1 reinforcement detail",
                        Status = ShopDrawingStatus.Released,
                    },
                };
                db.AddRange(task, item);
                await db.SaveChangesAsync();
                database.TaskId = task.Id;
                database.ItemId = item.Id;
                return database;
            }
            catch { await database.DisposeAsync(); throw; }
        }

        public async ValueTask DisposeAsync()
        {
            var catalog = new SqlConnectionStringBuilder(connection).InitialCatalog;
            if (!catalog.StartsWith(CatalogPrefix, StringComparison.Ordinal) ||
                !Guid.TryParseExact(catalog[CatalogPrefix.Length..], "N", out _))
                throw new InvalidOperationException("Refusing cleanup outside the generated drawing test database.");
            await using var db = Context();
            await db.Database.EnsureDeletedAsync();
        }
    }
}
