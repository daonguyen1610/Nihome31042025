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

public class ActivityServiceCrudTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ActivityService _sut;
    private readonly EntityTranslationService _translationSvc;

    public ActivityServiceCrudTests()
    {
        _db = DbContextFactory.Create();
        _translationSvc = new EntityTranslationService(_db, new MemoryCache(new MemoryCacheOptions()));
        var hosted = new HostedImageService(Mock.Of<IWebHostEnvironment>(e => e.ContentRootPath == "/tmp"));
        var categorySvc = new ActivityCategoryService(_db, NullLogger<ActivityCategoryService>.Instance);
        _sut = new ActivityService(_db, _translationSvc, hosted, categorySvc, NullLogger<ActivityService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private static UpsertActivityRequest BasePayload(string slug = "a1") => new()
    {
        Slug = slug,
        Date = "2026-01-01",
        ImageUrl = "/images/activities/x.jpg",
        Category = "Events",
        Title = "Title",
        Excerpt = "Excerpt",
        Content = new[] { "p1" },
        SortOrder = 0,
    };

    [Fact]
    public async Task GetAll_OrdersBySortOrder()
    {
        var a = BasePayload("a"); a.SortOrder = 5; await _sut.CreateAsync(a);
        var b = BasePayload("b"); b.SortOrder = 1; await _sut.CreateAsync(b);

        var list = await _sut.GetAllAsync();
        Assert.Equal(new[] { "b", "a" }, list.Select(x => x.Slug));
    }

    [Fact]
    public async Task GetBySlug_AppliesEntityTranslation()
    {
        var created = await _sut.CreateAsync(BasePayload("hi"));
        await _translationSvc.SetTranslationsAsync(EntityTypes.Activity, created.Id, "en",
            new Dictionary<string, string> { ["Title"] = "EN-Title" });

        var en = await _sut.GetBySlugAsync("hi", "en");
        Assert.Equal("EN-Title", en!.Title);
    }

    [Fact]
    public async Task Create_WithUnknownCategoryId_Throws()
    {
        var req = BasePayload();
        req.Category = null;
        req.CategoryId = 999;

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.CreateAsync(req));
    }

    [Fact]
    public async Task Update_NonExisting_ReturnsNull()
    {
        Assert.Null(await _sut.UpdateAsync(404, BasePayload()));
    }

    [Fact]
    public async Task Update_ChangesGalleryAndTitle()
    {
        var created = await _sut.CreateAsync(BasePayload("u"));

        var req = BasePayload("u");
        req.Title = "Updated Title";
        req.Gallery = new[] { "/images/activities/g1.jpg" };

        var updated = await _sut.UpdateAsync(created.Id, req);
        Assert.Equal("Updated Title", updated!.Title);
        Assert.Single(updated.Gallery!);
    }

    [Fact]
    public async Task Create_PreservesLegacyStringContent()
    {
        var created = await _sut.CreateAsync(BasePayload("legacy"));

        var content = Assert.IsType<JsonElement>(created.Content[0]);
        Assert.Equal("p1", content.GetString());
    }

    [Fact]
    public async Task Create_PreservesBlockContentJson()
    {
        var req = BasePayload("blocks");
        req.Content =
        [
            new Dictionary<string, object> { ["type"] = "text", ["value"] = "Intro" },
            new Dictionary<string, object> { ["type"] = "image", ["url"] = "/images/activities/inside.jpg" },
        ];

        var created = await _sut.CreateAsync(req);

        var first = Assert.IsType<JsonElement>(created.Content[0]);
        Assert.Equal("text", first.GetProperty("type").GetString());
        Assert.Equal("Intro", first.GetProperty("value").GetString());
        Assert.Contains("\"type\":\"image\"", _db.Activities.Single(a => a.Id == created.Id).ContentJson);
    }

    [Fact]
    public async Task Delete_RemovesEntityAndTranslations()
    {
        var created = await _sut.CreateAsync(BasePayload("del"));
        await _translationSvc.SetTranslationsAsync(EntityTypes.Activity, created.Id, "en",
            new Dictionary<string, string> { ["Title"] = "X" });

        Assert.True(await _sut.DeleteAsync(created.Id));
        Assert.Empty(_db.Activities);
        Assert.Empty(_db.EntityTranslations.Where(t => t.EntityType == EntityTypes.Activity && t.EntityId == created.Id));
    }

    [Fact]
    public async Task Delete_NonExisting_ReturnsFalse()
    {
        Assert.False(await _sut.DeleteAsync(404));
    }
}
