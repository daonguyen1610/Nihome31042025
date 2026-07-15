using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace nihomebackend.tests.Services;

public class ActivityServiceCategorySyncTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ActivityService _sut;

    public ActivityServiceCategorySyncTests()
    {
        _db = DbContextFactory.Create();
        var entityTranslationSvc = new EntityTranslationService(_db, Mock.Of<IMemoryCache>());
        var hostedImageService = new HostedImageService(
            Mock.Of<IWebHostEnvironment>(env => env.ContentRootPath == "/tmp"));
        var categorySvc = new ActivityCategoryService(_db, NullLogger<ActivityCategoryService>.Instance);
        _sut = new ActivityService(_db, entityTranslationSvc, hostedImageService, categorySvc, NullLogger<ActivityService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task CreateAsync_AutoCreatesMissingActivityCategory()
    {
        await _sut.CreateAsync(new UpsertActivityRequest
        {
            Slug = "post-with-new-category",
            Date = "25.04.2026",
            ImageUrl = "/images/post.jpg",
            Category = "Groundbreaking",
            Title = "Groundbreaking Post",
            Excerpt = "Excerpt",
            Content = ["Paragraph"],
            SortOrder = 1,
        });

        var category = _db.ActivityCategories.SingleOrDefault(c => c.Name == "Groundbreaking");
        Assert.NotNull(category);
        Assert.True(category!.IsActive);
    }

    [Fact]
    public async Task CreateAsync_AutoCreatedActivityCategory_HasNameViPopulated()
    {
        await _sut.CreateAsync(new UpsertActivityRequest
        {
            Slug = "post-with-another-new-category",
            Date = "25.04.2026",
            ImageUrl = "/images/post2.jpg",
            Category = "Ribbon Cutting",
            Title = "Ribbon Cutting Post",
            Excerpt = "Excerpt",
            Content = ["Paragraph"],
            SortOrder = 1,
        });

        var category = _db.ActivityCategories.Single(c => c.Name == "Ribbon Cutting");
        Assert.Equal("Ribbon Cutting", category.NameVi);
    }
}
