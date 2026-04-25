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

public class ProjectsControllerTests : IDisposable
{
    private readonly AppDbContext _db;
        private readonly ProjectsController _sut;

        public ProjectsControllerTests()
        {
            _db = DbContextFactory.Create();
            
            var service = new ProjectService(_db);
            _sut = new ProjectsController(service);
        }

        public void Dispose() => _db.Dispose();

    [Fact]
    async Task GetAll_ReturnsEmptyList_WhenNoProjects()
    {
        // Act
        var result = await _sut.GetAll();

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var projects = Assert.IsType<List<ProjectResponse>>(ok.Value);
        Assert.Empty(projects);
    }

    [Fact]
    async Task GetAll_ReturnsProjects_WhenDataExists()
    {
        // Arrange
        var project = new Project
        {
            Slug = "bma-project",
            ImageUrl = "/images/project-bma.jpg",
            Name = "BMA Office",
            Client = "Client Name",
            Location = "Location",
            Scale = "Large",
            Scope = "Commercial",
            Status = "Completed",
            Year = "2024",
            SortOrder = 1,
            CreatedAt = DateTime.UtcNow
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetAll();

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var projects = Assert.IsType<List<ProjectResponse>>(ok.Value);
        Assert.Single(projects);
        Assert.Equal("bma-project", projects[0].Slug);
    }

    [Fact]
    async Task GetBySlug_ReturnsProject_WhenSlugExists()
    {
        // Arrange
        var project = new Project
        {
            Slug = "nbdc-project",
            ImageUrl = "/images/project-nbdc.jpg",
            Name = "NBDC Building",
            Client = "NBDC",
            Location = "Vietnam",
            Scale = "Medium",
            Scope = "Data Center",
            Status = "Ongoing",
            Year = "2023",
            CreatedAt = DateTime.UtcNow
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetBySlug("nbdc-project");

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ProjectResponse>(ok.Value);
        Assert.Equal("nbdc-project", response.Slug);
        Assert.Equal("NBDC Building", response.Name);
    }

    [Fact]
    async Task GetBySlug_ReturnsNotFound_WhenSlugDoesNotExist()
    {
        // Act
        var result = await _sut.GetBySlug("nonexistent-project");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    async Task Create_ReturnsCreatedAtAction_WithValidRequest()
    {
        // Arrange
        var request = new UpsertProjectRequest
        {
            Slug = "new-project",
            ImageUrl = "/images/new-project.jpg",
            Name = "New Project",
            Client = "Test Client",
            Location = "Test Location",
            Scale = "Small",
            Scope = "Test Scope",
            Status = "Planning",
            Year = "2025",
            SortOrder = 1
        };

        // Act
        var result = await _sut.Create(request);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(ProjectsController.GetBySlug), createdResult.ActionName);
        var response = Assert.IsType<ProjectResponse>(createdResult.Value);
        Assert.Equal("new-project", response.Slug);
    }

    [Fact]
    async Task Create_SavesProjectToDatabase()
    {
        // Arrange
        var request = new UpsertProjectRequest
        {
            Slug = "db-project",
            ImageUrl = "/images/db.jpg",
            Name = "DB Project",
            Client = "Test",
            Location = "Test",
            Scale = "Test",
            Scope = "Test",
            Status = "Active",
            Year = "2025"
        };

        // Act
        await _sut.Create(request);

        // Assert
        var saved = _db.Projects.FirstOrDefault(p => p.Slug == "db-project");
        Assert.NotNull(saved);
        Assert.Equal("DB Project", saved.Name);
    }

    [Fact]
    async Task Update_ReturnsOk_WhenProjectExists()
    {
        // Arrange
        var project = new Project
        {
            Slug = "old-project",
            ImageUrl = "/images/old.jpg",
            Name = "Old Title",
            Client = "Old",
            Location = "Old",
            Scale = "Old",
            Scope = "Old",
            Status = "Old",
            Year = "2024",
            CreatedAt = DateTime.UtcNow
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        var updateRequest = new UpsertProjectRequest
        {
            Slug = "updated-project",
            ImageUrl = "/images/new.jpg",
            Name = "Updated Title",
            Client = "Updated",
            Location = "Updated",
            Scale = "Updated",
            Scope = "Updated",
            Status = "Updated",
            Year = "2025"
        };

        // Act
        var result = await _sut.Update(project.Id, updateRequest);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ProjectResponse>(ok.Value);
        Assert.Equal("Updated Title", response.Name);
    }

    [Fact]
    async Task Update_ReturnsNotFound_WhenProjectDoesNotExist()
    {
        // Arrange
        var updateRequest = new UpsertProjectRequest
        {
            Slug = "test",
            ImageUrl = "/images/test.jpg",
            Name = "Test",
            Client = "Test",
            Location = "Test",
            Scale = "Test",
            Scope = "Test",
            Status = "Test",
            Year = "2025"
        };

        // Act
        var result = await _sut.Update(999, updateRequest);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    async Task Delete_ReturnsNoContent_WhenProjectExists()
    {
        // Arrange
        var project = new Project
        {
            Slug = "delete-project",
            ImageUrl = "/images/delete.jpg",
            Name = "Delete Test",
            Client = "Test",
            Location = "Test",
            Scale = "Test",
            Scope = "Test",
            Status = "Test",
            Year = "2025",
            CreatedAt = DateTime.UtcNow
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();
        int id = project.Id;

        // Act
        var result = await _sut.Delete(id);

        // Assert
        Assert.IsType<NoContentResult>(result);
        var deleted = _db.Projects.FirstOrDefault(p => p.Id == id);
        Assert.Null(deleted);
    }

    [Fact]
    async Task Delete_ReturnsNotFound_WhenProjectDoesNotExist()
    {
        // Act
        var result = await _sut.Delete(999);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }
}
