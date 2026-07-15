using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using System.Text.Json;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace nihomebackend.tests.Services;

public class NewsServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly NewsService _sut;
    private readonly EntityTranslationService _translationSvc;

    public NewsServiceTests()
    {
        _db = DbContextFactory.Create();
        _translationSvc = new EntityTranslationService(_db, new MemoryCache(new MemoryCacheOptions()));
        var hosted = new HostedImageService(Mock.Of<IWebHostEnvironment>(e => e.ContentRootPath == "/tmp"));
        var categorySvc = new NewsCategoryService(_db, NullLogger<NewsCategoryService>.Instance);
        _sut = new NewsService(_db, _translationSvc, hosted, categorySvc, NullLogger<NewsService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private static UpsertNewsRequest BasePayload(string slug = "n1") => new()
    {
        Slug = slug,
        Date = "2026-01-01",
        ImageUrl = "/images/news/cover.png",
        Category = "company",
        Title = "Hello",
        Excerpt = "World",
        Content = new[] { "p1", "p2" },
        SortOrder = 0,
    };

    [Fact]
    public async Task Create_PersistsAndReturns_Mapped()
    {
        var res = await _sut.CreateAsync(BasePayload());
        Assert.Equal("n1", res.Slug);
        Assert.Single(_db.NewsArticles);
    }

    [Fact]
    public async Task GetAll_OrdersBySortOrder_AndAppliesViShortcut()
    {
        await _sut.CreateAsync(BasePayload("a"));
        await _sut.CreateAsync(new UpsertNewsRequest
        {
            Slug = "b",
            Date = "2026-01-02",
            ImageUrl = "",
            Category = "x",
            Title = "T",
            Excerpt = "E",
            Content = new[] { "c" },
            SortOrder = -1,
        });

        var list = await _sut.GetAllAsync();
        Assert.Equal(new[] { "b", "a" }, list.Select(x => x.Slug));
    }

    [Fact]
    public async Task GetBySlug_AppliesEntityTranslation_WhenLangNotVi()
    {
        var created = await _sut.CreateAsync(BasePayload("hello"));
        await _translationSvc.SetTranslationsAsync(EntityTypes.News, created.Id, "en",
            new Dictionary<string, string> { ["Title"] = "Hello-EN", ["Excerpt"] = "World-EN" });

        var en = await _sut.GetBySlugAsync("hello", "en");
        Assert.NotNull(en);
        Assert.Equal("Hello-EN", en!.Title);
        Assert.Equal("World-EN", en.Excerpt);

        var vi = await _sut.GetBySlugAsync("hello", "vi");
        Assert.Equal("Hello", vi!.Title);
    }

    [Fact]
    public async Task Update_NonExistingId_ReturnsNull()
    {
        var res = await _sut.UpdateAsync(999, BasePayload());
        Assert.Null(res);
    }

    [Fact]
    public async Task Update_PersistsGalleryAndContent()
    {
        var created = await _sut.CreateAsync(BasePayload("g1"));
        var req = BasePayload("g1");
        req.Gallery = new[] { "/images/news/a.jpg", "/images/news/b.jpg" };
        req.Title = "Updated";

        var updated = await _sut.UpdateAsync(created.Id, req);
        Assert.NotNull(updated);
        Assert.Equal("Updated", updated!.Title);
        Assert.Equal(2, updated.Gallery!.Length);
    }

    [Fact]
    public async Task Create_PreservesBlockContentJson()
    {
        var req = BasePayload("blocks");
        req.Content =
        [
            new Dictionary<string, object> { ["type"] = "text", ["value"] = "Lead" },
            new Dictionary<string, object> { ["type"] = "image", ["url"] = "/images/news/inside.jpg" },
        ];

        var created = await _sut.CreateAsync(req);

        var first = Assert.IsType<JsonElement>(created.Content[0]);
        Assert.Equal("text", first.GetProperty("type").GetString());
        Assert.Equal("Lead", first.GetProperty("value").GetString());
        Assert.Contains("\"type\":\"image\"", _db.NewsArticles.Single(n => n.Id == created.Id).ContentJson);
    }

    [Fact]
    public async Task Create_AutoCreatesMissingNewsCategory()
    {
        var created = await _sut.CreateAsync(BasePayload("category-sync"));

        Assert.Equal("company", created.Category);
        Assert.NotNull(created.NewsCategoryId);
        Assert.Single(_db.NewsCategories);
    }

    [Fact]
    public async Task Delete_RemovesEntity_AndTranslations()
    {
        var created = await _sut.CreateAsync(BasePayload("del"));
        await _translationSvc.SetTranslationsAsync(EntityTypes.News, created.Id, "en",
            new Dictionary<string, string> { ["Title"] = "DEL-EN" });

        var ok = await _sut.DeleteAsync(created.Id);
        Assert.True(ok);
        Assert.Empty(_db.NewsArticles);
        Assert.Empty(_db.EntityTranslations.Where(t => t.EntityType == EntityTypes.News && t.EntityId == created.Id));
    }

    [Fact]
    public async Task Delete_NonExisting_ReturnsFalse()
    {
        Assert.False(await _sut.DeleteAsync(123));
    }

    [Fact]
    public async Task GetBySlug_NotFound_ReturnsNull()
    {
        Assert.Null(await _sut.GetBySlugAsync("missing"));
    }
}
