using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NihomeBackend.Data;
using NihomeBackend.Services;

namespace NihomeBackend.IntegrationTests.Infrastructure;

public sealed class IdempotencyReservationConcurrencyTests
{
    [Fact]
    public async Task SameActorAndRequest_ParallelReservations_ExecuteOnce()
    {
        await using var database = await RelationalIdempotencyDatabase.CreateAsync();
        await using var firstContext = database.CreateContext();
        await using var secondContext = database.CreateContext();
        var firstService = CreateService(firstContext);
        var secondService = CreateService(secondContext);

        var results = await Task.WhenAll(
            firstService.TryBeginAsync("customers.create", "same-actor-key", "fingerprint", 41),
            secondService.TryBeginAsync("customers.create", "same-actor-key", "fingerprint", 41));

        results.Count(result => result == IdempotencyService.BeginResult.Execute).Should().Be(1);
        results.Count(result => result == IdempotencyService.BeginResult.InProgress).Should().Be(1);
        (await database.CountRecordsAsync()).Should().Be(1);
    }

    [Fact]
    public async Task DifferentActors_ParallelReservations_BindsKeyToOneActor()
    {
        await using var database = await RelationalIdempotencyDatabase.CreateAsync();
        await using var firstContext = database.CreateContext();
        await using var secondContext = database.CreateContext();
        var firstService = CreateService(firstContext);
        var secondService = CreateService(secondContext);

        var attempts = await Task.WhenAll(
            CaptureBeginAsync(firstService, 41),
            CaptureBeginAsync(secondService, 84));

        attempts.Count(attempt => attempt.Result == IdempotencyService.BeginResult.Execute).Should().Be(1);
        attempts.Count(attempt => attempt.Exception is IdempotencyConflictException).Should().Be(1);
        (await database.CountRecordsAsync()).Should().Be(1);
        (await database.GetOwnerUserIdAsync()).Should().BeOneOf(41, 84);
    }

    private static IdempotencyService CreateService(AppDbContext context) =>
        new(context, NullLogger<IdempotencyService>.Instance);

    private static async Task<BeginAttempt> CaptureBeginAsync(IdempotencyService service, int userId)
    {
        try
        {
            var result = await service.TryBeginAsync(
                "customers.create", "different-actor-key", "fingerprint", userId);
            return new BeginAttempt(result, null);
        }
        catch (Exception exception)
        {
            return new BeginAttempt(null, exception);
        }
    }

    private sealed record BeginAttempt(IdempotencyService.BeginResult? Result, Exception? Exception);

    private sealed class RelationalIdempotencyDatabase : IAsyncDisposable
    {
        private readonly string _databasePath;
        private readonly string _connectionString;

        private RelationalIdempotencyDatabase(string databasePath)
        {
            _databasePath = databasePath;
            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false,
            }.ToString();
        }

        public static async Task<RelationalIdempotencyDatabase> CreateAsync()
        {
            var database = new RelationalIdempotencyDatabase(
                Path.Combine(Path.GetTempPath(), $"nihome-idempotency-{Guid.NewGuid():N}.db"));
            await using var connection = new SqliteConnection(database._connectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                PRAGMA journal_mode = WAL;
                PRAGMA busy_timeout = 5000;
                CREATE TABLE idempotency_records (
                    Id INTEGER NOT NULL CONSTRAINT PK_idempotency_records PRIMARY KEY AUTOINCREMENT,
                    Scope TEXT NOT NULL,
                    Key TEXT NOT NULL,
                    Fingerprint TEXT NULL,
                    UserId INTEGER NULL,
                    StatusCode INTEGER NOT NULL,
                    ResponseJson TEXT NULL,
                    ResponseHeadersJson TEXT NULL,
                    CreatedAt TEXT NOT NULL,
                    ExpiresAt TEXT NOT NULL
                );
                CREATE UNIQUE INDEX IX_idempotency_records_Scope_Key
                    ON idempotency_records (Scope, Key);
                """;
            await command.ExecuteNonQueryAsync();
            return database;
        }

        public AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connectionString)
                .Options;
            return new AppDbContext(options);
        }

        public async Task<int> CountRecordsAsync()
        {
            await using var context = CreateContext();
            return await context.IdempotencyRecords.CountAsync();
        }

        public async Task<int?> GetOwnerUserIdAsync()
        {
            await using var context = CreateContext();
            return await context.IdempotencyRecords.Select(record => record.UserId).SingleAsync();
        }

        public ValueTask DisposeAsync()
        {
            if (File.Exists(_databasePath)) File.Delete(_databasePath);
            if (File.Exists(_databasePath + "-shm")) File.Delete(_databasePath + "-shm");
            if (File.Exists(_databasePath + "-wal")) File.Delete(_databasePath + "-wal");
            return ValueTask.CompletedTask;
        }
    }
}
