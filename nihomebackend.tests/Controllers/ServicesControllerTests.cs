using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Controllers;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;
using System.Text.Json;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace nihomebackend.tests.Controllers;

public class ServicesControllerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ServicesController _sut;

    public ServicesControllerTests()
    {
        _db = DbContextFactory.Create();
        var service = new ServiceItemService(_db, NullLogger<ServiceItemService>.Instance);
        _sut = new ServicesController(service);
    }

    public void Dispose() => _db.Dispose();

    private static JsonElement CreateJsonElement(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    [Fact]
    public async Task GetAll_ReturnsEmptyList_WhenNoServices()
    {
        var result = await _sut.GetAll();
        var ok = Assert.IsType<OkObjectResult>(result);
        var services = Assert.IsType<List<ServiceResponse>>(ok.Value);
        Assert.Empty(services);
    }

    [Fact]
    public async Task GetAll_ReturnsServices_WhenDataExists()
    {
        var service = new ServiceItem
        {
            Slug = "consulting",
            Title = "Consulting",
            ShortTitle = "Consult",
            Tagline = "Professional consulting",
            Intro = "We provide consulting services",
            SortOrder = 1,
            CreatedAt = DateTime.UtcNow
        };
        _db.ServiceItems.Add(service);
        await _db.SaveChangesAsync();

        var result = await _sut.GetAll();
        var ok = Assert.IsType<OkObjectResult>(result);
        var services = Assert.IsType<List<ServiceResponse>>(ok.Value);
        Assert.Single(services);
        Assert.Equal("consulting", services[0].Slug);
    }

    [Fact]
    public async Task GetBySlug_ReturnsService_WhenSlugExists()
    {
        var service = new ServiceItem
        {
            Slug = "design",
            Title = "Design",
            ShortTitle = "Design",
            Tagline = "Creative design",
            Intro = "Design solutions",
            CreatedAt = DateTime.UtcNow
        };
        _db.ServiceItems.Add(service);
        await _db.SaveChangesAsync();

        var result = await _sut.GetBySlug("design");
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ServiceResponse>(ok.Value);
        Assert.Equal("design", response.Slug);
        Assert.Equal("Design", response.Title);
    }

    [Fact]
    public async Task GetBySlug_ReturnsNotFound_WhenSlugDoesNotExist()
    {
        var result = await _sut.GetBySlug("nonexistent");
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    async Task Create_ReturnsCreatedAtAction_WithValidRequest()
    {
        var request = new UpsertServiceRequest
        {
            Slug = "development",
            Title = "Development",
            ShortTitle = "Dev",
            Tagline = "Software development",
            Intro = "We build software",
            Sections = CreateJsonElement("[]"),
            Highlights = new[] { "highlight1", "highlight2" },
            SortOrder = 1
        };

        var result = await _sut.Create(request);
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(ServicesController.GetBySlug), createdResult.ActionName);
        var response = Assert.IsType<ServiceResponse>(createdResult.Value);
        Assert.Equal("development", response.Slug);
    }

    [Fact]
    async Task Create_SavesServiceToDatabase()
    {
        var request = new UpsertServiceRequest
        {
            Slug = "db-service",
            Title = "DB Service",
            ShortTitle = "DB",
            Tagline = "Test service",
            Intro = "Test",
            Sections = CreateJsonElement("[]"),
            Highlights = new[] { "test" }
        };

        await _sut.Create(request);
        var saved = _db.ServiceItems.FirstOrDefault(s => s.Slug == "db-service");
        Assert.NotNull(saved);
        Assert.Equal("DB Service", saved.Title);
    }

    [Fact]
    async Task Update_ReturnsOk_WhenServiceExists()
    {
        var service = new ServiceItem
        {
            Slug = "old-service",
            Title = "Old Title",
            ShortTitle = "Old",
            Tagline = "Old",
            Intro = "Old",
            CreatedAt = DateTime.UtcNow
        };
        _db.ServiceItems.Add(service);
        await _db.SaveChangesAsync();

        var updateRequest = new UpsertServiceRequest
        {
            Slug = "updated-service",
            Title = "Updated Title",
            ShortTitle = "New",
            Tagline = "Updated",
            Intro = "Updated",
            Sections = CreateJsonElement("[]"),
            Highlights = new[] { "highlight" }
        };

        var result = await _sut.Update(service.Id, updateRequest);
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ServiceResponse>(ok.Value);
        Assert.Equal("Updated Title", response.Title);
    }

    [Fact]
    async Task Update_ReturnsNotFound_WhenServiceDoesNotExist()
    {
        var updateRequest = new UpsertServiceRequest
        {
            Slug = "test",
            Title = "Test",
            ShortTitle = "Test",
            Tagline = "Test",
            Intro = "Test",
            Sections = CreateJsonElement("[]"),
            Highlights = new[] { "test" }
        };

        var result = await _sut.Update(999, updateRequest);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Delete_ReturnsNoContent_WhenServiceExists()
    {
        var service = new ServiceItem
        {
            Slug = "delete-service",
            Title = "Delete Test",
            ShortTitle = "Delete",
            Tagline = "Test",
            Intro = "Test",
            CreatedAt = DateTime.UtcNow
        };
        _db.ServiceItems.Add(service);
        await _db.SaveChangesAsync();
        int id = service.Id;

        var result = await _sut.Delete(id);
        Assert.IsType<NoContentResult>(result);
        var deleted = _db.ServiceItems.FirstOrDefault(s => s.Id == id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenServiceDoesNotExist()
    {
        var result = await _sut.Delete(999);
        Assert.IsType<NotFoundResult>(result);
    }
}
