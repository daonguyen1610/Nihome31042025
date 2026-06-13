using Microsoft.Extensions.Caching.Memory;
using NihomeBackend.Data;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;
using Xunit;

namespace nihomebackend.tests.Services;

public class EntityTranslationServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly EntityTranslationService _sut;

    private const string EType = "TestEntity";

    public EntityTranslationServiceTests()
    {
        _db = DbContextFactory.Create();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _sut = new EntityTranslationService(_db, _cache);
    }

    public void Dispose()
    {
        _db.Dispose();
        _cache.Dispose();
    }

    [Fact]
    public async Task GetEntityTranslations_ReturnsEmptyForVi()
    {
        var dict = await _sut.GetEntityTranslationsAsync(EType, 1, "vi");
        Assert.Empty(dict);
    }

    [Fact]
    public async Task SetTranslations_CreatesAndReadsBack()
    {
        await _sut.SetTranslationsAsync(EType, 7, "en",
            new Dictionary<string, string> { ["Title"] = "T", ["Excerpt"] = "E" });

        var dict = await _sut.GetEntityTranslationsAsync(EType, 7, "en");
        Assert.Equal("T", dict["Title"]);
        Assert.Equal("E", dict["Excerpt"]);
    }

    [Fact]
    public async Task SetTranslations_UpdatesExistingFields()
    {
        await _sut.SetTranslationsAsync(EType, 1, "en",
            new Dictionary<string, string> { ["Title"] = "v1" });
        await _sut.SetTranslationsAsync(EType, 1, "en",
            new Dictionary<string, string> { ["Title"] = "v2" });

        var dict = await _sut.GetEntityTranslationsAsync(EType, 1, "en");
        Assert.Equal("v2", dict["Title"]);
        Assert.Single(_db.EntityTranslations);
    }

    [Fact]
    public async Task SetTranslations_InvalidatesCache()
    {
        await _sut.SetTranslationsAsync(EType, 1, "en",
            new Dictionary<string, string> { ["Title"] = "old" });
        await _sut.GetEntityTranslationsAsync(EType, 1, "en"); // populates cache

        await _sut.SetTranslationsAsync(EType, 1, "en",
            new Dictionary<string, string> { ["Title"] = "new" });

        var dict = await _sut.GetEntityTranslationsAsync(EType, 1, "en");
        Assert.Equal("new", dict["Title"]);
    }

    [Fact]
    public async Task GetBatchTranslations_ReturnsEmptyForVi()
    {
        var ids = new[] { 1, 2, 3 };
        var batch = await _sut.GetBatchTranslationsAsync(EType, ids, "vi");
        Assert.Equal(3, batch.Count);
        Assert.All(batch.Values, d => Assert.Empty(d));
    }

    [Fact]
    public async Task GetBatchTranslations_LoadsForMultipleEntitiesInOneShot()
    {
        await _sut.SetTranslationsAsync(EType, 1, "en", new Dictionary<string, string> { ["Title"] = "A" });
        await _sut.SetTranslationsAsync(EType, 2, "en", new Dictionary<string, string> { ["Title"] = "B" });

        var batch = await _sut.GetBatchTranslationsAsync(EType, new[] { 1, 2, 3 }, "en");
        Assert.Equal("A", batch[1]["Title"]);
        Assert.Equal("B", batch[2]["Title"]);
        Assert.Empty(batch[3]);
    }

    [Fact]
    public async Task DeleteEntityTranslations_RemovesAllLanguagesAndFields()
    {
        await _sut.SetTranslationsAsync(EType, 5, "en", new Dictionary<string, string> { ["A"] = "a" });
        await _sut.SetTranslationsAsync(EType, 5, "ja", new Dictionary<string, string> { ["A"] = "a" });

        await _sut.DeleteEntityTranslationsAsync(EType, 5);

        Assert.Empty(_db.EntityTranslations.Where(t => t.EntityType == EType && t.EntityId == 5));
    }

    [Fact]
    public async Task GetAllTranslationsForEntity_ReturnsOrderedRows()
    {
        await _sut.SetTranslationsAsync(EType, 9, "ja", new Dictionary<string, string> { ["B"] = "b" });
        await _sut.SetTranslationsAsync(EType, 9, "en", new Dictionary<string, string> { ["A"] = "a" });

        var rows = await _sut.GetAllTranslationsForEntityAsync(EType, 9);
        Assert.Equal(2, rows.Count);
        Assert.Equal("en", rows[0].LanguageCode); // alphabetic
    }
}
