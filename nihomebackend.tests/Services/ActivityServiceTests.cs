using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public class ActivityServiceTests : IDisposable
{
    private readonly AppDbContext _db = DbContextFactory.Create();
    private readonly ActivityService _service;

    public ActivityServiceTests()
    {
        _service = new ActivityService(
            _db,
            new EntityTranslationService(_db, new MemoryCache(new MemoryCacheOptions())),
            new HostedImageService(Mock.Of<IWebHostEnvironment>(env => env.ContentRootPath == "/tmp")),
            new ActivityCategoryService(_db, NullLogger<ActivityCategoryService>.Instance),
            NullLogger<ActivityService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private static UpsertActivityRequest Request(string category) => new()
    {
        Slug = "activity-category-test",
        Date = "2026-10-08",
        ImageUrl = "/images/activity.jpg",
        Title = "Activity",
        Excerpt = "Excerpt",
        Category = category,
        Content = ["Content"],
    };

    [Fact]
    public async Task Create_RejectsUnknownCategoryWithoutWritingActivity()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateAsync(Request("Not configured")));

        Assert.Empty(_db.ActivityCategories);
        Assert.Empty(_db.Activities);
    }

    [Fact]
    public async Task Update_PreservesUnlinkedLegacyCategoryWithoutCreatingOne()
    {
        var activity = new Activity
        {
            Slug = "activity-category-test", Title = "Old", Excerpt = "Old",
            Date = "2026-10-08", Category = "Legacy", ContentJson = "[]",
        };
        _db.Activities.Add(activity);
        await _db.SaveChangesAsync();
        var request = Request("Legacy");
        request.Title = "Edited";

        var updated = await _service.UpdateAsync(activity.Id, request);

        Assert.Equal("Edited", updated!.Title);
        Assert.Equal("Legacy", updated.Category);
        Assert.Null(updated.CategoryId);
        Assert.Empty(_db.ActivityCategories);
    }
}
