using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;
using Xunit;

namespace nihomebackend.tests.Services;

public class ActivityServiceImageUrlTests : IDisposable
{
    private readonly string _contentRootPath;
    private readonly AppDbContext _db;
    private readonly ActivityService _sut;

    public ActivityServiceImageUrlTests()
    {
        _contentRootPath = Path.Combine(Path.GetTempPath(), $"nihome-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_contentRootPath, "wwwroot", "images", "upload"));

        _db = DbContextFactory.Create();
        var translationService = new EntityTranslationService(_db, Mock.Of<IMemoryCache>());
        var hostedImageService = new HostedImageService(
            Mock.Of<IWebHostEnvironment>(env => env.ContentRootPath == _contentRootPath));

        _sut = new ActivityService(_db, translationService, hostedImageService);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_contentRootPath))
        {
            Directory.Delete(_contentRootPath, recursive: true);
        }
    }

    [Fact]
    public async Task UpdateAsync_DoesNotDeleteExistingManagedImage_WhenRequestUsesAbsoluteUrlForSameFile()
    {
        var managedImageUrl = "/images/upload/existing-image.png";
        var absoluteImageUrl = $"https://example.test{managedImageUrl}";
        var filePath = Path.Combine(_contentRootPath, "wwwroot", "images", "upload", "existing-image.png");
        await File.WriteAllTextAsync(filePath, "test-image");

        var activity = new Activity
        {
            Slug = "existing-post",
            Date = "26.04.2026",
            ImageUrl = managedImageUrl,
            Category = "Events",
            Title = "Existing Post",
            Excerpt = "Excerpt",
            ContentJson = "[]",
            SortOrder = 1,
        };
        _db.Activities.Add(activity);
        await _db.SaveChangesAsync();

        var result = await _sut.UpdateAsync(activity.Id, new UpsertActivityRequest
        {
            Slug = activity.Slug,
            Date = activity.Date,
            ImageUrl = absoluteImageUrl,
            Category = activity.Category,
            Title = activity.Title,
            Excerpt = activity.Excerpt,
            Content = [],
            SortOrder = activity.SortOrder,
        });

        Assert.NotNull(result);
        Assert.Equal(managedImageUrl, result!.ImageUrl);
        Assert.True(File.Exists(filePath));

        var savedActivity = await _db.Activities.FindAsync(activity.Id);
        Assert.NotNull(savedActivity);
        Assert.Equal(managedImageUrl, savedActivity!.ImageUrl);
    }
}
