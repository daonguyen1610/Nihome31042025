using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using NihomeBackend.Controllers;
using NihomeBackend.Data;
using NihomeBackend.Mappings;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;
using Xunit;

namespace nihomebackend.tests.Controllers;

public class ActivitiesControllerTests : IDisposable
{
    private readonly AppDbContext _db;
        private readonly ActivityService _service;
        private readonly ActivitiesController _sut;

        public ActivitiesControllerTests()
        {
            _db = DbContextFactory.Create();
            
            var entityTranslationSvc = new EntityTranslationService(
                _db, Mock.Of<IMemoryCache>());
            
            _service = new ActivityService(_db, entityTranslationSvc);
            _sut = new ActivitiesController(_service);
        }

        public void Dispose() => _db.Dispose();

    [Fact]
    async Task GetAll_ReturnsEmptyList_WhenNoActivities()
    {
        var result = await _sut.GetAll();

        var ok = Assert.IsType<OkObjectResult>(result);
        var activities = Assert.IsType<List<ActivityResponse>>(ok.Value);
        Assert.Empty(activities);
    }

    [Fact]
    async Task GetAll_ReturnsActivities_WhenDataExists()
    {
        // Arrange
        var activity = new Activity
        {
            Slug = "test-activity",
            Date = "2025-01-15",
            ImageUrl = "/images/activity-test.jpg",
            Category = "Event",
            Title = "Test Activity",
            Excerpt = "Test excerpt",
            ContentJson = "[\"Content 1\", \"Content 2\"]",
            SortOrder = 1,
            CreatedAt = DateTime.UtcNow
        };
        _db.Activities.Add(activity);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetAll();

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var activities = Assert.IsType<List<ActivityResponse>>(ok.Value);
        Assert.Single(activities);
        Assert.Equal("test-activity", activities[0].Slug);
    }

    [Fact]
    async Task GetBySlug_ReturnsActivity_WhenSlugExists()
    {
        // Arrange
        var activity = new Activity
        {
            Slug = "handover-ceremony",
            Date = "2025-01-15",
            ImageUrl = "/images/activity-ceremony.jpg",
            Category = "Event",
            Title = "Handover Ceremony",
            Excerpt = "Ceremony excerpt",
            ContentJson = "[\"Content\"]",
            CreatedAt = DateTime.UtcNow
        };
        _db.Activities.Add(activity);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetBySlug("handover-ceremony");

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ActivityResponse>(ok.Value);
        Assert.Equal("handover-ceremony", response.Slug);
        Assert.Equal("Handover Ceremony", response.Title);
    }

    [Fact]
    async Task GetBySlug_ReturnsNotFound_WhenSlugDoesNotExist()
    {
        // Act
        var result = await _sut.GetBySlug("nonexistent");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    async Task Create_ReturnsCreatedAtAction_WithValidRequest()
    {
        // Arrange
        var request = new UpsertActivityRequest
        {
            Slug = "new-activity",
            Date = "2025-02-01",
            ImageUrl = "/images/new.jpg",
            Category = "Event",
            Title = "New Activity",
            Excerpt = "New excerpt",
            Content = new[] { "Content 1" },
            SortOrder = 1,
            Author = "Test Author"
        };

        // Act
        var result = await _sut.Create(request);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(ActivitiesController.GetBySlug), createdResult.ActionName);
        var response = Assert.IsType<ActivityResponse>(createdResult.Value);
        Assert.Equal("new-activity", response.Slug);
    }

    [Fact]
    async Task Create_SavesActivityToDatabase()
    {
        // Arrange
        var request = new UpsertActivityRequest
        {
            Slug = "db-test",
            Date = "2025-02-01",
            ImageUrl = "/images/test.jpg",
            Category = "Event",
            Title = "DB Test",
            Excerpt = "Test",
            Content = new[] { "Content" },
            SortOrder = 1
        };

        // Act
        await _sut.Create(request);

        // Assert
        var saved = _db.Activities.FirstOrDefault(a => a.Slug == "db-test");
        Assert.NotNull(saved);
        Assert.Equal("DB Test", saved.Title);
    }

    [Fact]
    async Task Update_ReturnsOk_WhenActivityExists()
    {
        // Arrange
        var activity = new Activity
        {
            Slug = "update-test",
            Date = "2025-01-15",
            ImageUrl = "/images/old.jpg",
            Category = "Old",
            Title = "Old Title",
            Excerpt = "Old",
            ContentJson = "[\"Old\"]",
            CreatedAt = DateTime.UtcNow
        };
        _db.Activities.Add(activity);
        await _db.SaveChangesAsync();

        var updateRequest = new UpsertActivityRequest
        {
            Slug = "updated-slug",
            Date = "2025-02-01",
            ImageUrl = "/images/new.jpg",
            Category = "New",
            Title = "New Title",
            Excerpt = "New",
            Content = new[] { "New" },
            SortOrder = 1
        };

        // Act
        var result = await _sut.Update(activity.Id, updateRequest);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ActivityResponse>(ok.Value);
        Assert.Equal("New Title", response.Title);
    }

    [Fact]
    async Task Update_ReturnsNotFound_WhenActivityDoesNotExist()
    {
        // Arrange
        var updateRequest = new UpsertActivityRequest
        {
            Slug = "test",
            Date = "2025-02-01",
            ImageUrl = "/images/test.jpg",
            Category = "Test",
            Title = "Test",
            Excerpt = "Test",
            Content = new[] { "Test" },
            SortOrder = 1
        };

        // Act
        var result = await _sut.Update(999, updateRequest);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    async Task Delete_ReturnsNoContent_WhenActivityExists()
    {
        // Arrange
        var activity = new Activity
        {
            Slug = "delete-test",
            Date = "2025-01-15",
            ImageUrl = "/images/test.jpg",
            Category = "Test",
            Title = "Delete Test",
            Excerpt = "Test",
            ContentJson = "[\"Test\"]",
            CreatedAt = DateTime.UtcNow
        };
        _db.Activities.Add(activity);
        await _db.SaveChangesAsync();
        int id = activity.Id;

        // Act
        var result = await _sut.Delete(id);

        // Assert
        Assert.IsType<NoContentResult>(result);
        var deleted = _db.Activities.FirstOrDefault(a => a.Id == id);
        Assert.Null(deleted);
    }

    [Fact]
    async Task Delete_ReturnsNotFound_WhenActivityDoesNotExist()
    {
        // Act
        var result = await _sut.Delete(999);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }
}
