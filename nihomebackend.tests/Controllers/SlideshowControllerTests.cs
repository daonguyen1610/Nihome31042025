using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using NihomeBackend.Controllers;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;
using Xunit;

namespace nihomebackend.tests.Controllers;

public class SlideshowControllerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly SlideshowService _service;
    private readonly SlideshowController _sut;

    public SlideshowControllerTests()
    {
        _db = DbContextFactory.Create();

        var entityTranslationSvc = new EntityTranslationService(
            _db, Mock.Of<IMemoryCache>());

        _service = new SlideshowService(_db, entityTranslationSvc);
        _sut = new SlideshowController(_service);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    async Task GetAll_ReturnsEmptyList_WhenNoSlides()
    {
        var result = await _sut.GetAll();

        var ok = Assert.IsType<OkObjectResult>(result);
        var slides = Assert.IsType<List<SlideshowResponse>>(ok.Value);
        Assert.Empty(slides);
    }

    [Fact]
    async Task GetAll_ReturnsOnlyActiveSlides_ByDefault()
    {
        // Arrange
        _db.SlideshowItems.AddRange(
            new SlideshowItem { Slug = "active-1", ImageUrl = "/img/1.jpg", Title = "Active", IsActive = true, SortOrder = 0 },
            new SlideshowItem { Slug = "inactive-1", ImageUrl = "/img/2.jpg", Title = "Inactive", IsActive = false, SortOrder = 1 }
        );
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetAll();

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var slides = Assert.IsType<List<SlideshowResponse>>(ok.Value);
        Assert.Single(slides);
        Assert.Equal("active-1", slides[0].Slug);
    }

    [Fact]
    async Task GetAll_ReturnsAllSlides_WhenActiveOnlyFalse()
    {
        // Arrange
        _db.SlideshowItems.AddRange(
            new SlideshowItem { Slug = "a", ImageUrl = "/img/1.jpg", Title = "A", IsActive = true, SortOrder = 0 },
            new SlideshowItem { Slug = "b", ImageUrl = "/img/2.jpg", Title = "B", IsActive = false, SortOrder = 1 }
        );
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetAll(activeOnly: false);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var slides = Assert.IsType<List<SlideshowResponse>>(ok.Value);
        Assert.Equal(2, slides.Count);
    }

    [Fact]
    async Task GetAll_ReturnsSlidesOrderedBySortOrder()
    {
        // Arrange
        _db.SlideshowItems.AddRange(
            new SlideshowItem { Slug = "second", ImageUrl = "/img/2.jpg", Title = "Second", IsActive = true, SortOrder = 1 },
            new SlideshowItem { Slug = "first", ImageUrl = "/img/1.jpg", Title = "First", IsActive = true, SortOrder = 0 }
        );
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetAll();

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var slides = Assert.IsType<List<SlideshowResponse>>(ok.Value);
        Assert.Equal("first", slides[0].Slug);
        Assert.Equal("second", slides[1].Slug);
    }

    [Fact]
    async Task GetBySlug_ReturnsSlide_WhenSlugExists()
    {
        // Arrange
        _db.SlideshowItems.Add(new SlideshowItem
        {
            Slug = "hero-factory",
            ImageUrl = "/images/hero.jpg",
            Title = "Factory Hero",
            Subtitle = "Industrial leader",
            LinkUrl = "/projects",
            LinkText = "View",
            IsActive = true,
            SortOrder = 0,
        });
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetBySlug("hero-factory");

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var slide = Assert.IsType<SlideshowResponse>(ok.Value);
        Assert.Equal("hero-factory", slide.Slug);
        Assert.Equal("Factory Hero", slide.Title);
        Assert.Equal("Industrial leader", slide.Subtitle);
        Assert.Equal("/projects", slide.LinkUrl);
    }

    [Fact]
    async Task GetBySlug_ReturnsNotFound_WhenSlugDoesNotExist()
    {
        var result = await _sut.GetBySlug("nonexistent");
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    async Task Create_ReturnsCreatedAtAction_WithValidRequest()
    {
        // Arrange
        var request = new UpsertSlideshowRequest
        {
            Slug = "new-slide",
            ImageUrl = "/images/new.jpg",
            Title = "New Slide",
            Subtitle = "Subtitle",
            LinkUrl = "/about",
            LinkText = "Learn More",
            IsActive = true,
            SortOrder = 0,
        };

        // Act
        var result = await _sut.Create(request);

        // Assert
        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(SlideshowController.GetBySlug), created.ActionName);
        var slide = Assert.IsType<SlideshowResponse>(created.Value);
        Assert.Equal("new-slide", slide.Slug);
        Assert.Equal("New Slide", slide.Title);
    }

    [Fact]
    async Task Create_SavesSlideToDatabase()
    {
        // Arrange
        var request = new UpsertSlideshowRequest
        {
            Slug = "db-test",
            ImageUrl = "/images/test.jpg",
            Title = "DB Test",
            IsActive = true,
            SortOrder = 0,
        };

        // Act
        await _sut.Create(request);

        // Assert
        var saved = _db.SlideshowItems.FirstOrDefault(s => s.Slug == "db-test");
        Assert.NotNull(saved);
        Assert.Equal("DB Test", saved.Title);
    }

    [Fact]
    async Task Update_ReturnsOk_WhenSlideExists()
    {
        // Arrange
        var slide = new SlideshowItem
        {
            Slug = "update-test",
            ImageUrl = "/images/old.jpg",
            Title = "Old Title",
            IsActive = true,
            SortOrder = 0,
        };
        _db.SlideshowItems.Add(slide);
        await _db.SaveChangesAsync();

        var updateRequest = new UpsertSlideshowRequest
        {
            Slug = "updated-slug",
            ImageUrl = "/images/new.jpg",
            Title = "New Title",
            Subtitle = "New Subtitle",
            LinkUrl = "/new",
            LinkText = "Click",
            IsActive = false,
            SortOrder = 5,
        };

        // Act
        var result = await _sut.Update(slide.Id, updateRequest);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<SlideshowResponse>(ok.Value);
        Assert.Equal("New Title", response.Title);
        Assert.False(response.IsActive);
        Assert.Equal(5, response.SortOrder);
    }

    [Fact]
    async Task Update_ReturnsNotFound_WhenSlideDoesNotExist()
    {
        var updateRequest = new UpsertSlideshowRequest
        {
            Slug = "test",
            ImageUrl = "/images/test.jpg",
            Title = "Test",
            SortOrder = 0,
        };

        var result = await _sut.Update(999, updateRequest);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    async Task Delete_ReturnsNoContent_WhenSlideExists()
    {
        // Arrange
        var slide = new SlideshowItem
        {
            Slug = "delete-test",
            ImageUrl = "/images/test.jpg",
            Title = "Delete Test",
            IsActive = true,
            SortOrder = 0,
        };
        _db.SlideshowItems.Add(slide);
        await _db.SaveChangesAsync();
        int id = slide.Id;

        // Act
        var result = await _sut.Delete(id);

        // Assert
        Assert.IsType<NoContentResult>(result);
        var deleted = _db.SlideshowItems.FirstOrDefault(s => s.Id == id);
        Assert.Null(deleted);
    }

    [Fact]
    async Task Delete_ReturnsNotFound_WhenSlideDoesNotExist()
    {
        var result = await _sut.Delete(999);
        Assert.IsType<NotFoundResult>(result);
    }
}
