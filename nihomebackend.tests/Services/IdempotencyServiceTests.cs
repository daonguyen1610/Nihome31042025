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

        await sut.SaveAsync("scope1", "key-1", "fp", userId: 42, statusCode: 200, payload);

        var cached = await sut.TryGetCachedAsync("scope1", "key-1");
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
