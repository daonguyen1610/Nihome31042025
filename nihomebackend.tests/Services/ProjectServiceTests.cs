using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace nihomebackend.tests.Services;

public class ProjectServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ProjectService _sut;

    public ProjectServiceTests()
    {
        _db = DbContextFactory.Create();
        var hosted = new HostedImageService(Mock.Of<IWebHostEnvironment>(e => e.ContentRootPath == "/tmp"));
        var catSvc = new ProjectCategoryService(_db, NullLogger<ProjectCategoryService>.Instance);
        _sut = new ProjectService(_db, hosted, catSvc, NullLogger<ProjectService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private static UpsertProjectRequest BasePayload(string slug = "p1") => new()
    {
        Slug = slug,
        ImageUrl = "/images/projects/cover.png",
        Name = "Project " + slug,
        Client = "Client",
        Location = "HCM",
        Scope = "Build",
        Status = "ongoing",
        SortOrder = 0,
    };

    [Fact]
    public async Task Create_AutoCreatesCategoryFromName()
    {
        var req = BasePayload();
        req.Category = "Hospitality";

        var res = await _sut.CreateAsync(req);

        Assert.Equal("Hospitality", res.Category);
        Assert.NotNull(res.CategoryId);
        Assert.Single(_db.ProjectCategories);
    }

    [Fact]
    public async Task Create_WithUnknownCategoryId_Throws()
    {
        var req = BasePayload();
        req.CategoryId = 999;

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.CreateAsync(req));
    }

    [Fact]
    public async Task Create_PersistsJsonFields()
    {
        var req = BasePayload("json");
        req.Challenges = new[] { "c1", "c2" };
        req.Solutions = new[] { "s1" };
        req.Highlights = JsonSerializer.Deserialize<JsonElement>("""[{"k":"v"}]""");

        var res = await _sut.CreateAsync(req);

        Assert.Equal(new[] { "c1", "c2" }, res.Challenges);
        Assert.Equal(new[] { "s1" }, res.Solutions);
        Assert.NotNull(res.Highlights);
    }

    [Fact]
    public async Task GetAll_OrdersBySortOrder()
    {
        var a = BasePayload("a"); a.SortOrder = 5; await _sut.CreateAsync(a);
        var b = BasePayload("b"); b.SortOrder = 1; await _sut.CreateAsync(b);

        var list = await _sut.GetAllAsync();
        Assert.Equal(new[] { "b", "a" }, list.Select(p => p.Slug));
    }

    [Fact]
    public async Task GetBySlug_NotFound_ReturnsNull()
    {
        Assert.Null(await _sut.GetBySlugAsync("missing"));
    }

    [Fact]
    public async Task Update_NonExistingId_ReturnsNull()
    {
        Assert.Null(await _sut.UpdateAsync(123, BasePayload()));
    }

    [Fact]
    public async Task Update_ChangesGallery_AndCategoryReassign()
    {
        var initial = BasePayload("u1"); initial.Category = "OldCat";
        var created = await _sut.CreateAsync(initial);

        var req = BasePayload("u1");
        req.Category = "NewCat";
        req.Gallery = new[] { "/images/projects/x.jpg" };
        req.Name = "Renamed";

        var updated = await _sut.UpdateAsync(created.Id, req);
        Assert.NotNull(updated);
        Assert.Equal("Renamed", updated!.Name);
        Assert.Equal("NewCat", updated.Category);
        Assert.Single(updated.Gallery!);
    }

    [Fact]
    public async Task Delete_RemovesEntity()
    {
        var created = await _sut.CreateAsync(BasePayload("del"));
        Assert.True(await _sut.DeleteAsync(created.Id));
        Assert.Empty(_db.Projects);
    }

    [Fact]
    public async Task Delete_NonExisting_ReturnsFalse()
    {
        Assert.False(await _sut.DeleteAsync(404));
    }
}
