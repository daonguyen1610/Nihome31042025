using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Controllers;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;
using Xunit;

namespace nihomebackend.tests.Controllers;

public class ActivityCategoriesControllerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ActivityCategoryService _service;
    private readonly ActivityCategoriesController _sut;

    public ActivityCategoriesControllerTests()
    {
        _db = DbContextFactory.Create();
        _service = new ActivityCategoryService(_db);
        _sut = new ActivityCategoriesController(_service);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetAll_ReturnsOk_WithCategoryList()
    {
        _db.ActivityCategories.Add(new ActivityCategory { Name = "Events", IsActive = true, SortOrder = 1 });
        await _db.SaveChangesAsync();

        var result = await _sut.GetAll(includeInactive: true);

        var ok = Assert.IsType<OkObjectResult>(result);
        var categories = Assert.IsType<List<ActivityCategoryResponse>>(ok.Value);
        Assert.Single(categories);
        Assert.Equal("Events", categories[0].Name);
    }

    [Fact]
    public async Task Create_ReturnsCreatedAtAction_WhenRequestIsValid()
    {
        var result = await _sut.Create(new UpsertActivityCategoryRequest
        {
            Name = "Events",
            IsActive = true,
            SortOrder = 1,
        });

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var response = Assert.IsType<ActivityCategoryResponse>(created.Value);
        Assert.Equal("Events", response.Name);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenNameAlreadyExists()
    {
        _db.ActivityCategories.Add(new ActivityCategory { Name = "Events", IsActive = true, SortOrder = 1 });
        await _db.SaveChangesAsync();

        var result = await _sut.Create(new UpsertActivityCategoryRequest
        {
            Name = "events",
            IsActive = true,
            SortOrder = 2,
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenCategoryDoesNotExist()
    {
        var result = await _sut.Update(999, new UpsertActivityCategoryRequest
        {
            Name = "Events",
            IsActive = true,
            SortOrder = 1,
        });

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Delete_ReturnsBadRequest_WhenCategoryIsInUse()
    {
        var category = new ActivityCategory { Name = "Events", IsActive = true, SortOrder = 1 };
        _db.ActivityCategories.Add(category);
        await _db.SaveChangesAsync();
        _db.Activities.Add(new Activity
        {
            Slug = "post-1",
            Date = "25.04.2026",
            ImageUrl = "/images/post-1.jpg",
            Category = "events",
            ActivityCategoryId = category.Id,
            Title = "Post 1",
            Excerpt = "Post 1",
            ContentJson = "[]",
        });
        await _db.SaveChangesAsync();

        var result = await _sut.Delete(category.Id);

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
