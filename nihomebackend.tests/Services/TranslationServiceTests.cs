using Microsoft.Extensions.Caching.Memory;
using NihomeBackend.Data;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;
using Xunit;

namespace nihomebackend.tests.Services;

public class TranslationServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly TranslationService _sut;

    public TranslationServiceTests()
    {
        _db = DbContextFactory.Create();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _sut = new TranslationService(_db, _cache);
    }

    public void Dispose()
    {
        _db.Dispose();
        _cache.Dispose();
    }

    [Fact]
    public async Task UpsertPair_CreatesViAndOtherLanguages()
    {
        await _sut.UpsertPairAsync("hello.world", "Xin chào",
            new Dictionary<string, string> { ["en"] = "Hello", ["ja"] = "こんにちは" }, "common");

        var vi = await _sut.GetTranslationMapAsync("vi");
        var en = await _sut.GetTranslationMapAsync("en");
        Assert.Equal("Xin chào", vi["hello.world"]);
        Assert.Equal("Hello", en["hello.world"]);
    }

    [Fact]
    public async Task UpsertPair_OverwritesExisting()
    {
        await _sut.UpsertPairAsync("k1", "v1", null, "cat");
        await _sut.UpsertPairAsync("k1", "v2", null, "cat");

        var map = await _sut.GetTranslationMapAsync("vi");
        Assert.Equal("v2", map["k1"]);
    }

    [Fact]
    public async Task GetTranslationMap_CachesResult()
    {
        await _sut.UpsertPairAsync("k", "v", null, null);
        // first call: populates cache
        await _sut.GetTranslationMapAsync("vi");

        // direct DB write bypassing service should NOT be visible until invalidation
        var row = _db.Translations.First(t => t.Key == "k");
        row.Value = "stale";
        await _db.SaveChangesAsync();

        var map = await _sut.GetTranslationMapAsync("vi");
        Assert.Equal("v", map["k"]);
    }

    [Fact]
    public async Task UpsertPair_InvalidatesCacheAcrossLanguages()
    {
        await _sut.UpsertPairAsync("k", "v1", null, null);
        await _sut.GetTranslationMapAsync("vi");

        await _sut.UpsertPairAsync("k", "v2", null, null);

        var map = await _sut.GetTranslationMapAsync("vi");
        Assert.Equal("v2", map["k"]);
    }

    [Fact]
    public async Task BulkUpsert_CreatesAndUpdates()
    {
        await _sut.UpsertPairAsync("a", "v1", null, "cat");

        await _sut.BulkUpsertAsync(new List<BulkTranslationItem>
        {
            new() { Key = "a", LanguageCode = "vi", Value = "v1-new", Category = "cat" },
            new() { Key = "b", LanguageCode = "vi", Value = "B", Category = "cat" },
        });

        var map = await _sut.GetTranslationMapAsync("vi");
        Assert.Equal("v1-new", map["a"]);
        Assert.Equal("B", map["b"]);
    }

    [Fact]
    public async Task GetPairs_FiltersByCategoryAndSearch()
    {
        await _sut.UpsertPairAsync("food.apple", "Táo", null, "food");
        await _sut.UpsertPairAsync("food.banana", "Chuối", null, "food");
        await _sut.UpsertPairAsync("city.hcm", "TP HCM", null, "city");

        var foodOnly = await _sut.GetPairsAsync("food", null);
        Assert.Equal(2, foodOnly.Count);

        var search = await _sut.GetPairsAsync(null, "apple");
        Assert.Single(search);
        Assert.Equal("food.apple", search[0].Key);
    }

    [Fact]
    public async Task GetCategories_ReturnsDistinctSorted()
    {
        await _sut.UpsertPairAsync("a", "1", null, "zeta");
        await _sut.UpsertPairAsync("b", "2", null, "alpha");
        await _sut.UpsertPairAsync("c", "3", null, "alpha");

        var cats = await _sut.GetCategoriesAsync();
        Assert.Equal(new[] { "alpha", "zeta" }, cats);
    }

    [Fact]
    public async Task DeleteKey_RemovesAcrossAllLanguages()
    {
        await _sut.UpsertPairAsync("rm", "vi-val",
            new Dictionary<string, string> { ["en"] = "en-val" }, null);

        await _sut.DeleteKeyAsync("rm");

        Assert.Empty(_db.Translations.Where(t => t.Key == "rm"));
    }
}
