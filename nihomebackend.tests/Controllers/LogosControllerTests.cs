using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Moq;
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

public class LogosControllerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly LogosController _sut;

    public LogosControllerTests()
    {
        _db = DbContextFactory.Create();
        var hostedImageService = new HostedImageService(
            Mock.Of<IWebHostEnvironment>(env => env.ContentRootPath == "/tmp"));
        var service = new LogoService(_db, hostedImageService, NullLogger<LogoService>.Instance);
        _sut = new LogosController(service);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetAll_ReturnsGroupedLogos()
    {
        var result = await _sut.GetAll();
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public async Task GetAll_ReturnsLogos_WhenDataExists()
    {
        var logo = new ClientLogo
        {
            Name = "Nicon Logo",
            ImageUrl = "/images/logo-nicon.png",
            Href = "https://nicon.com",
            Kind = LogoKind.Client,
            SortOrder = 1,
            CreatedAt = DateTime.UtcNow
        };
        _db.ClientLogos.Add(logo);
        await _db.SaveChangesAsync();

        var result = await _sut.GetAll();
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public async Task Create_ReturnsCreated_WithValidRequest()
    {
        var request = new UpsertLogoRequest
        {
            Name = "New Logo",
            ImageUrl = "/images/new-logo.png",
            Href = "https://example.com",
            Kind = "Client",
            SortOrder = 2
        };

        var result = await _sut.Create(request);
        var createdResult = Assert.IsType<CreatedResult>(result);
        var response = Assert.IsType<LogoResponse>(createdResult.Value);
        Assert.Equal("New Logo", response.Name);
    }

    [Fact]
    public async Task Create_SavesLogoToDatabase()
    {
        var request = new UpsertLogoRequest
        {
            Name = "DB Logo",
            ImageUrl = "/images/db-logo.png",
            Href = "https://db.com",
            Kind = "Partner",
            SortOrder = 1
        };

        await _sut.Create(request);
        var saved = _db.ClientLogos.FirstOrDefault(l => l.Name == "DB Logo");
        Assert.NotNull(saved);
        Assert.Equal("/images/db-logo.png", saved.ImageUrl);
    }

    [Fact]
    public async Task Update_ReturnsOk_WhenLogoExists()
    {
        var logo = new ClientLogo
        {
            Name = "Old Logo",
            ImageUrl = "/images/old.png",
            Href = "https://old.com",
            Kind = LogoKind.Client,
            SortOrder = 1,
            CreatedAt = DateTime.UtcNow
        };
        _db.ClientLogos.Add(logo);
        await _db.SaveChangesAsync();

        var updateRequest = new UpsertLogoRequest
        {
            Name = "Updated Logo",
            ImageUrl = "/images/updated.png",
            Href = "https://updated.com",
            Kind = "Partner",
            SortOrder = 2
        };

        var result = await _sut.Update(logo.Id, updateRequest);
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<LogoResponse>(ok.Value);
        Assert.Equal("Updated Logo", response.Name);
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenLogoDoesNotExist()
    {
        var updateRequest = new UpsertLogoRequest
        {
            Name = "Test",
            ImageUrl = "/images/test.png",
            Href = "https://test.com",
            Kind = "Client",
            SortOrder = 1
        };

        var result = await _sut.Update(999, updateRequest);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Delete_ReturnsNoContent_WhenLogoExists()
    {
        var logo = new ClientLogo
        {
            Name = "Delete Logo",
            ImageUrl = "/images/delete.png",
            Href = "https://delete.com",
            Kind = LogoKind.Client,
            SortOrder = 1,
            CreatedAt = DateTime.UtcNow
        };
        _db.ClientLogos.Add(logo);
        await _db.SaveChangesAsync();
        int id = logo.Id;

        var result = await _sut.Delete(id);
        Assert.IsType<NoContentResult>(result);
        var deleted = _db.ClientLogos.FirstOrDefault(l => l.Id == id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenLogoDoesNotExist()
    {
        var result = await _sut.Delete(999);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Create_MultipleLogos_BothStored()
    {
        var logo1 = new UpsertLogoRequest
        {
            Name = "Logo 1",
            ImageUrl = "/images/logo1.png",
            Kind = "Client",
            SortOrder = 1
        };

        var logo2 = new UpsertLogoRequest
        {
            Name = "Logo 2",
            ImageUrl = "/images/logo2.png",
            Kind = "Partner",
            SortOrder = 2
        };

        await _sut.Create(logo1);
        await _sut.Create(logo2);

        var allLogos = _db.ClientLogos.ToList();
        Assert.Equal(2, allLogos.Count);
    }
}
