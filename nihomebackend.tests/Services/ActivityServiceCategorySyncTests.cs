using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;
using Xunit;

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
        _sut = new ActivityService(_db, entityTranslationSvc, hostedImageService);
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
}
