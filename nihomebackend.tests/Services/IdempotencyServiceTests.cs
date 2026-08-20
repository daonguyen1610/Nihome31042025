using Microsoft.Extensions.Logging;
using Moq;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public class IdempotencyServiceTests : IDisposable
{
    private readonly NihomeBackend.Data.AppDbContext _db = DbContextFactory.Create();

    public void Dispose() => _db.Dispose();

    private IdempotencyService BuildSut()
        => new(_db, Mock.Of<ILogger<IdempotencyService>>());

    [Fact]
    public async Task SaveAsync_ThenTryGetCached_ReturnsSamePayload()
    {
        var sut = BuildSut();
        var payload = new { Hello = "world" };

        await sut.TryBeginAsync("scope1", "key-1", "fp", userId: 42);
        await sut.SaveAsync("scope1", "key-1", "fp", userId: 42, statusCode: 200, payload);

        var cached = await sut.TryGetCachedAsync("scope1", "key-1", "fp", userId: 42);
        Assert.NotNull(cached);
        Assert.Equal(200, cached!.Value.StatusCode);
        Assert.Contains("world", cached.Value.ResponseJson);
    }

    [Fact]
    public async Task TryGetCached_ReturnsNull_WhenKeyMissing()
    {
        var sut = BuildSut();
        var cached = await sut.TryGetCachedAsync("scope1", "unknown");
        Assert.Null(cached);
    }

    [Fact]
    public async Task SaveAsync_NoOp_WhenKeyMissingOrTooLong()
    {
        var sut = BuildSut();
        await sut.SaveAsync("scope1", null, "fp", null, 200, new { x = 1 });
        await sut.SaveAsync("scope1", new string('a', IdempotencyService.MaxKeyLength + 1), "fp", null, 200, new { x = 1 });

        Assert.Empty(_db.IdempotencyRecords);
    }

    [Fact]
    public async Task TryGetCached_ReturnsNull_WhenExpired()
    {
        var sut = BuildSut();

        _db.IdempotencyRecords.Add(new NihomeBackend.Models.IdempotencyRecord
        {
            Scope = "scope1",
            Key = "old-key",
            StatusCode = 200,
            ResponseJson = "{}",
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
        });
        await _db.SaveChangesAsync();

        var cached = await sut.TryGetCachedAsync("scope1", "old-key");
        Assert.Null(cached);
    }

    [Fact]
    public async Task TryBeginAsync_ReserveThenSave_ReplaysCompletedResponse()
    {
        var sut = BuildSut();

        var first = await sut.TryBeginAsync("scope1", "key-1", "fp", userId: 42);
        await sut.SaveAsync("scope1", "key-1", "fp", userId: 42, 201, new { Id = 7 });
        var second = await sut.TryBeginAsync("scope1", "key-1", "fp", userId: 42);

        Assert.Equal(IdempotencyService.BeginResult.Execute, first);
        Assert.Equal(IdempotencyService.BeginResult.Replay, second);
        var cached = await sut.TryGetCachedAsync("scope1", "key-1", "fp", userId: 42);
        Assert.Equal(201, cached!.Value.StatusCode);
        Assert.Contains("\"id\":7", cached.Value.ResponseJson);
    }

    [Fact]
    public async Task TryBeginAsync_SamePendingRequest_ReturnsInProgress()
    {
        var sut = BuildSut();

        await sut.TryBeginAsync("scope1", "key-1", "fp", userId: 42);
        var result = await sut.TryBeginAsync("scope1", "key-1", "fp", userId: 42);

        Assert.Equal(IdempotencyService.BeginResult.InProgress, result);
    }

    [Theory]
    [InlineData("different-fingerprint", 42)]
    [InlineData("fp", 43)]
    public async Task TryBeginAsync_ReusedForDifferentRequestOrActor_ThrowsConflict(
        string fingerprint,
        int userId)
    {
        var sut = BuildSut();
        await sut.TryBeginAsync("scope1", "key-1", "fp", userId: 42);

        await Assert.ThrowsAsync<IdempotencyConflictException>(() =>
            sut.TryBeginAsync("scope1", "key-1", fingerprint, userId));
    }

    [Fact]
    public async Task AbandonAsync_RemovesPendingReservation()
    {
        var sut = BuildSut();
        await sut.TryBeginAsync("scope1", "key-1", "fp", userId: 42);

        await sut.AbandonAsync("scope1", "key-1", "fp", userId: 42);

        var retry = await sut.TryBeginAsync("scope1", "key-1", "fp", userId: 42);
        Assert.Equal(IdempotencyService.BeginResult.Execute, retry);
    }

    [Fact]
    public async Task SaveAsync_PreservesStringEnumsAndReplayHeaders()
    {
        var sut = BuildSut();
        await sut.TryBeginAsync("scope1", "key-1", "fp", userId: 42);

        await sut.SaveAsync(
            "scope1",
            "key-1",
            "fp",
            userId: 42,
            statusCode: 200,
            new { Status = DayOfWeek.Monday },
            new Dictionary<string, string> { ["ETag"] = "\"token\"" });

        var cached = await sut.TryGetCachedAsync("scope1", "key-1", "fp", userId: 42);
        Assert.NotNull(cached);
        Assert.Contains("\"status\":\"Monday\"", cached.Value.ResponseJson);
        Assert.Equal("\"token\"", cached.Value.Headers["ETag"]);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("abc", true)]
    public void IsValidKey_VariousInputs(string? key, bool expected)
    {
        Assert.Equal(expected, IdempotencyService.IsValidKey(key));
    }
}
