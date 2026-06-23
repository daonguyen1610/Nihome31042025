using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace nihomebackend.tests.Services;

public class SlideshowServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly SlideshowService _sut;

    public SlideshowServiceTests()
    {
        _db = DbContextFactory.Create();
        var entityTranslationSvc = new EntityTranslationService(_db, Mock.Of<IMemoryCache>());
        var hostedImageService = new HostedImageService(
            Mock.Of<IWebHostEnvironment>(env => env.ContentRootPath == "/tmp"));
        _sut = new SlideshowService(_db, entityTranslationSvc, hostedImageService, NullLogger<SlideshowService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetAllAsync_ReturnsEmpty_WhenNoItems()
    {
        var result = await _sut.GetAllAsync();
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyActiveItems_ByDefault()
    {
        _db.SlideshowItems.AddRange(
            new SlideshowItem { Slug = "active", ImageUrl = "/a.jpg", Title = "Active", IsActive = true, SortOrder = 0 },
            new SlideshowItem { Slug = "inactive", ImageUrl = "/b.jpg", Title = "Inactive", IsActive = false, SortOrder = 1 }
        );
        await _db.SaveChangesAsync();

        var result = await _sut.GetAllAsync();

        Assert.Single(result);
        Assert.Equal("active", result[0].Slug);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllItems_WhenActiveOnlyFalse()
    {
        _db.SlideshowItems.AddRange(
            new SlideshowItem { Slug = "a", ImageUrl = "/a.jpg", Title = "A", IsActive = true, SortOrder = 0 },
            new SlideshowItem { Slug = "b", ImageUrl = "/b.jpg", Title = "B", IsActive = false, SortOrder = 1 }
        );
        await _db.SaveChangesAsync();

        var result = await _sut.GetAllAsync(activeOnly: false);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsItemsOrderedBySortOrder()
    {
        _db.SlideshowItems.AddRange(
            new SlideshowItem { Slug = "second", ImageUrl = "/b.jpg", Title = "B", IsActive = true, SortOrder = 1 },
            new SlideshowItem { Slug = "first", ImageUrl = "/a.jpg", Title = "A", IsActive = true, SortOrder = 0 }
        );
        await _db.SaveChangesAsync();

        var result = await _sut.GetAllAsync();

        Assert.Equal("first", result[0].Slug);
        Assert.Equal("second", result[1].Slug);
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsItem_WhenSlugExists()
    {
        _db.SlideshowItems.Add(new SlideshowItem
        {
            Slug = "hero-slide",
            ImageUrl = "/img/hero.jpg",
            Title = "Hero",
            Subtitle = "Sub",
            LinkUrl = "/projects",
            LinkText = "View",
            IsActive = true,
            SortOrder = 0,
        });
        await _db.SaveChangesAsync();

        var result = await _sut.GetBySlugAsync("hero-slide");

        Assert.NotNull(result);
        Assert.Equal("hero-slide", result.Slug);
        Assert.Equal("Hero", result.Title);
        Assert.Equal("Sub", result.Subtitle);
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsNull_WhenSlugDoesNotExist()
    {
        var result = await _sut.GetBySlugAsync("nonexistent");
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateAsync_PersistsItem()
    {
        var req = new UpsertSlideshowRequest
        {
            Slug = "new-slide",
            ImageUrl = "/img/new.jpg",
            Title = "New",
            Subtitle = "Sub",
            LinkUrl = "/about",
            LinkText = "More",
            IsActive = true,
            SortOrder = 0,
        };

        var result = await _sut.CreateAsync(req);

        Assert.Equal("new-slide", result.Slug);
        Assert.Equal("New", result.Title);
        var saved = _db.SlideshowItems.FirstOrDefault(s => s.Slug == "new-slide");
        Assert.NotNull(saved);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesFields_WhenItemExists()
    {
        var item = new SlideshowItem
        {
            Slug = "old-slug",
            ImageUrl = "/img/old.jpg",
            Title = "Old",
            IsActive = true,
            SortOrder = 0,
        };
        _db.SlideshowItems.Add(item);
        await _db.SaveChangesAsync();

        var req = new UpsertSlideshowRequest
        {
            Slug = "new-slug",
            ImageUrl = "/img/new.jpg",
            Title = "New",
            IsActive = false,
            SortOrder = 5,
        };

        var result = await _sut.UpdateAsync(item.Id, req);

        Assert.NotNull(result);
        Assert.Equal("New", result.Title);
        Assert.False(result.IsActive);
        Assert.Equal(5, result.SortOrder);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenItemDoesNotExist()
    {
        var req = new UpsertSlideshowRequest
        {
            Slug = "x",
            ImageUrl = "/x.jpg",
            Title = "X",
            SortOrder = 0,
        };

        var result = await _sut.UpdateAsync(999, req);
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_RemovesItem_WhenExists()
    {
        var item = new SlideshowItem
        {
            Slug = "to-delete",
            ImageUrl = "/img/d.jpg",
            Title = "Del",
            IsActive = true,
            SortOrder = 0,
        };
        _db.SlideshowItems.Add(item);
        await _db.SaveChangesAsync();
        int id = item.Id;

        var deleted = await _sut.DeleteAsync(id);

        Assert.True(deleted);
        Assert.Null(_db.SlideshowItems.FirstOrDefault(s => s.Id == id));
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenItemDoesNotExist()
    {
        var result = await _sut.DeleteAsync(999);
        Assert.False(result);
    }
}
