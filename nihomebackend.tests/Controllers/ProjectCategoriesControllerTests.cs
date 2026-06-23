using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Controllers;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace nihomebackend.tests.Controllers;

public class ProjectCategoriesControllerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ProjectCategoryService _service;
    private readonly ProjectCategoriesController _sut;

    public ProjectCategoriesControllerTests()
    {
        _db = DbContextFactory.Create();
        _service = new ProjectCategoryService(_db, NullLogger<ProjectCategoryService>.Instance);
        _sut = new ProjectCategoriesController(_service);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetAll_ReturnsOk_WithCategoryList()
    {
        _db.ProjectCategories.Add(new ProjectCategory { Name = "Factory", IsActive = true, SortOrder = 1 });
        await _db.SaveChangesAsync();

        var result = await _sut.GetAll(includeInactive: true);

        var ok = Assert.IsType<OkObjectResult>(result);
        var categories = Assert.IsType<List<ProjectCategoryResponse>>(ok.Value);
        Assert.Single(categories);
        Assert.Equal("Factory", categories[0].Name);
    }

    [Fact]
    public async Task Create_ReturnsCreatedAtAction_WhenRequestIsValid()
    {
        var result = await _sut.Create(new UpsertProjectCategoryRequest
        {
            Name = "Factory",
            IsActive = true,
            SortOrder = 1,
        });

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var response = Assert.IsType<ProjectCategoryResponse>(created.Value);
        Assert.Equal("Factory", response.Name);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenNameAlreadyExists()
    {
        _db.ProjectCategories.Add(new ProjectCategory { Name = "Factory", IsActive = true, SortOrder = 1 });
        await _db.SaveChangesAsync();

        var result = await _sut.Create(new UpsertProjectCategoryRequest
        {
            Name = "factory",
            IsActive = true,
            SortOrder = 2,
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenCategoryDoesNotExist()
    {
        var result = await _sut.Update(999, new UpsertProjectCategoryRequest
        {
            Name = "Factory",
            IsActive = true,
            SortOrder = 1,
        });

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Update_ReturnsOk_WhenCategoryUpdated()
    {
        var category = new ProjectCategory { Name = "Old", IsActive = true, SortOrder = 1 };
        _db.ProjectCategories.Add(category);
        await _db.SaveChangesAsync();

        var result = await _sut.Update(category.Id, new UpsertProjectCategoryRequest
        {
            Name = "New",
            IsActive = false,
            SortOrder = 5,
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ProjectCategoryResponse>(ok.Value);
        Assert.Equal("New", response.Name);
        Assert.False(response.IsActive);
        Assert.Equal(5, response.SortOrder);
    }

    [Fact]
    public async Task Delete_ReturnsBadRequest_WhenCategoryIsInUse()
    {
        var category = new ProjectCategory { Name = "Factory", IsActive = true, SortOrder = 1 };
        _db.ProjectCategories.Add(category);
        await _db.SaveChangesAsync();
        _db.Projects.Add(new Project
        {
            Slug = "p-1",
            ImageUrl = "/images/p-1.jpg",
            Name = "P1",
            Client = "Client",
            Location = "Location",
            Scope = "Scope",
            Status = "ongoing",
            Category = "factory",
            ProjectCategoryId = category.Id,
        });
        await _db.SaveChangesAsync();

        var result = await _sut.Delete(category.Id);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Delete_ReturnsNoContent_WhenCategoryIsRemoved()
    {
        var category = new ProjectCategory { Name = "Unused", IsActive = true, SortOrder = 1 };
        _db.ProjectCategories.Add(category);
        await _db.SaveChangesAsync();

        var result = await _sut.Delete(category.Id);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenCategoryDoesNotExist()
    {
        var result = await _sut.Delete(999);

        Assert.IsType<NotFoundResult>(result);
    }
}
