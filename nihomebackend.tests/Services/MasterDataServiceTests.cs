using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public class MasterDataServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly MasterDataService _sut;

    public MasterDataServiceTests()
    {
        _db = DbContextFactory.Create();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _sut = new MasterDataService(_db, _cache, NullLogger<MasterDataService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        _cache.Dispose();
    }

    private async Task SeedAsync(params (string cat, string code, string name, int sort, bool active)[] rows)
    {
        foreach (var (cat, code, name, sort, active) in rows)
        {
            _db.MasterDataOptions.Add(new MasterDataOption
            {
                Category = cat,
                Code = code,
                Name = name,
                SortOrder = sort,
                IsActive = active,
            });
        }
        await _db.SaveChangesAsync();
    }

    private static UpsertMasterDataOptionRequest Req(string code, string name, int sort = 1, bool active = true) => new()
    {
        Code = code,
        Name = name,
        SortOrder = sort,
        IsActive = active,
    };

    // ---------------- Read ----------------

    [Fact]
    public async Task GetByCategoryAsync_FiltersOutInactive_ByDefault()
    {
        await SeedAsync(
            ("customer_type", "individual", "Cá nhân", 1, true),
            ("customer_type", "company", "Doanh nghiệp", 2, true),
            ("customer_type", "archived", "Bị ẩn", 3, false));

        var result = await _sut.GetByCategoryAsync("customer_type");

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, r => r.Code == "archived");
    }

    [Fact]
    public async Task GetByCategoryAsync_IncludesInactive_WhenRequested()
    {
        await SeedAsync(
            ("customer_type", "individual", "Cá nhân", 1, true),
            ("customer_type", "archived", "Bị ẩn", 2, false));

        var result = await _sut.GetByCategoryAsync("customer_type", includeInactive: true);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetByCategoryAsync_ReturnsInSortOrderThenName()
    {
        await SeedAsync(
            ("stage", "beta", "Beta", 2, true),
            ("stage", "alpha", "Alpha", 1, true),
            ("stage", "gamma", "Gamma", 2, true));

        var result = await _sut.GetByCategoryAsync("stage");

        Assert.Equal(new[] { "alpha", "beta", "gamma" }, result.Select(r => r.Code));
    }

    [Fact]
    public async Task GetByCategoryAsync_NormalizesCategoryToLowerCase()
    {
        await SeedAsync(("customer_type", "individual", "Cá nhân", 1, true));

        var result = await _sut.GetByCategoryAsync("Customer_Type");

        Assert.Single(result);
        Assert.Equal("individual", result[0].Code);
    }

    [Fact]
    public async Task GetCategoriesAsync_GroupsAndCounts()
    {
        await SeedAsync(
            ("a", "one", "One", 1, true),
            ("a", "two", "Two", 2, true),
            ("a", "hidden", "Hidden", 3, false),
            ("b", "solo", "Solo", 1, true));

        var summary = (await _sut.GetCategoriesAsync())
            .ToDictionary(c => c.Category);

        Assert.Equal(3, summary["a"].TotalCount);
        Assert.Equal(2, summary["a"].ActiveCount);
        Assert.Equal(1, summary["b"].TotalCount);
        Assert.Equal(1, summary["b"].ActiveCount);
    }

    // ---------------- Create ----------------

    [Fact]
    public async Task CreateAsync_PersistsNormalizedCategoryAndCode()
    {
        var created = await _sut.CreateAsync(" Customer_Source ", Req(" MARKETING ", "Marketing", 1));

        Assert.Equal("customer_source", created.Category);
        Assert.Equal("marketing", created.Code);
        Assert.Equal("Marketing", created.Name);
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenCodeExistsInSameCategory()
    {
        await SeedAsync(("customer_source", "marketing", "Marketing", 1, true));

        await Assert.ThrowsAsync<MasterDataDuplicateCodeException>(() =>
            _sut.CreateAsync("customer_source", Req("marketing", "Marketing 2")));
    }

    [Fact]
    public async Task CreateAsync_AllowsSameCode_InDifferentCategories()
    {
        await _sut.CreateAsync("customer_status", Req("other", "Khác"));

        var created = await _sut.CreateAsync("lead_status", Req("other", "Khác"));

        Assert.Equal("lead_status", created.Category);
        Assert.Equal("other", created.Code);
    }

    // ---------------- Update ----------------

    [Fact]
    public async Task UpdateAsync_RewritesFields_ButKeepsCategoryImmutable()
    {
        var created = await _sut.CreateAsync("customer_source", Req("event", "Sự kiện", 4));

        var updated = await _sut.UpdateAsync(created.Id, new UpsertMasterDataOptionRequest
        {
            Code = "EVENT",
            Name = "Hội thảo",
            IsActive = false,
            SortOrder = 10,
            Description = "Sự kiện offline",
        });

        Assert.NotNull(updated);
        Assert.Equal("customer_source", updated!.Category);
        Assert.Equal("event", updated.Code);
        Assert.Equal("Hội thảo", updated.Name);
        Assert.False(updated.IsActive);
        Assert.Equal(10, updated.SortOrder);
        Assert.Equal("Sự kiện offline", updated.Description);
    }

    [Fact]
    public async Task UpdateAsync_Throws_WhenNewCodeCollidesWithSibling()
    {
        var a = await _sut.CreateAsync("customer_source", Req("marketing", "Marketing", 1));
        var b = await _sut.CreateAsync("customer_source", Req("referral", "Giới thiệu", 2));

        await Assert.ThrowsAsync<MasterDataDuplicateCodeException>(() =>
            _sut.UpdateAsync(b.Id, Req("marketing", "Marketing", 1)));

        // Original values must remain.
        var still = await _sut.GetByIdAsync(b.Id);
        Assert.Equal("referral", still!.Code);
    }

    [Fact]
    public async Task UpdateAsync_AllowsSameCode_WhenUpdatingSameRow()
    {
        var created = await _sut.CreateAsync("customer_source", Req("marketing", "Marketing", 1));

        var updated = await _sut.UpdateAsync(created.Id, Req("marketing", "Marketing SEA", 5));

        Assert.NotNull(updated);
        Assert.Equal("Marketing SEA", updated!.Name);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenIdMissing()
    {
        var result = await _sut.UpdateAsync(99999, Req("x", "x"));
        Assert.Null(result);
    }

    // ---------------- Delete ----------------

    [Fact]
    public async Task DeleteAsync_RemovesRowAndReturnsTrue()
    {
        var created = await _sut.CreateAsync("customer_type", Req("individual", "Cá nhân"));

        var deleted = await _sut.DeleteAsync(created.Id);

        Assert.True(deleted);
        Assert.Null(await _sut.GetByIdAsync(created.Id));
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenIdMissing()
    {
        var deleted = await _sut.DeleteAsync(99999);
        Assert.False(deleted);
    }

    // ---------------- Cache ----------------

    [Fact]
    public async Task GetByCategoryAsync_UsesCache_ForSecondCall()
    {
        await SeedAsync(("customer_type", "individual", "Cá nhân", 1, true));

        // Prime the cache.
        var first = await _sut.GetByCategoryAsync("customer_type");
        Assert.Single(first);

        // Add a new row directly to the DB.
        _db.MasterDataOptions.Add(new MasterDataOption
        {
            Category = "customer_type",
            Code = "company",
            Name = "Doanh nghiệp",
            IsActive = true,
        });
        await _db.SaveChangesAsync();

        // Second call should still hit the cached list — proves the cache is engaged.
        var second = await _sut.GetByCategoryAsync("customer_type");
        Assert.Single(second);
    }

    [Fact]
    public async Task CreateAsync_InvalidatesCache()
    {
        await SeedAsync(("customer_type", "individual", "Cá nhân", 1, true));

        _ = await _sut.GetByCategoryAsync("customer_type");  // Prime.

        await _sut.CreateAsync("customer_type", Req("company", "Doanh nghiệp", 2));

        var refreshed = await _sut.GetByCategoryAsync("customer_type");
        Assert.Equal(2, refreshed.Count);
    }

    [Fact]
    public async Task UpdateAsync_InvalidatesCache()
    {
        var created = await _sut.CreateAsync("customer_type", Req("individual", "Cá nhân"));

        _ = await _sut.GetByCategoryAsync("customer_type");  // Prime.

        await _sut.UpdateAsync(created.Id, Req("individual", "Cá nhân renamed"));

        var refreshed = await _sut.GetByCategoryAsync("customer_type");
        Assert.Equal("Cá nhân renamed", refreshed[0].Name);
    }

    [Fact]
    public async Task DeleteAsync_InvalidatesCache()
    {
        var created = await _sut.CreateAsync("customer_type", Req("individual", "Cá nhân"));

        _ = await _sut.GetByCategoryAsync("customer_type");  // Prime.

        await _sut.DeleteAsync(created.Id);

        var refreshed = await _sut.GetByCategoryAsync("customer_type");
        Assert.Empty(refreshed);
    }
}
