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

public class AboutSectionServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly AboutSectionService _sut;

    public AboutSectionServiceTests()
    {
        _db = DbContextFactory.Create();
        var hostedImageService = new HostedImageService(
            Mock.Of<IWebHostEnvironment>(env => env.ContentRootPath == "/tmp"));
        var translationSvc = new EntityTranslationService(_db, new MemoryCache(new MemoryCacheOptions()));
        _sut = new AboutSectionService(_db, hostedImageService, translationSvc);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyActiveItems_WhenActiveOnlyTrue()
    {
        _db.AboutSectionContents.AddRange(
            new AboutSectionContent { Slug = "about-main", Eyebrow = "A", TitleA = "B", TitleB = "C", Paragraph1 = "P1", Paragraph2 = "P2", IsActive = true, SortOrder = 0 },
            new AboutSectionContent { Slug = "strategy-main", Eyebrow = "A", TitleA = "B", TitleB = "C", Paragraph1 = "P1", Paragraph2 = "P2", IsActive = false, SortOrder = 1 }
        );
        await _db.SaveChangesAsync();

        var result = await _sut.GetAllAsync();

        Assert.Single(result);
        Assert.Equal("about-main", result[0].Slug);
    }

    [Fact]
    public async Task CreateAsync_PersistsItemsJsonAndNormalizesManagedImageUrl()
    {
        var result = await _sut.CreateAsync(new UpsertAboutSectionRequest
        {
            Slug = "timeline-main",
            ItemsJson = """[{"year":"2006","title":"Founded","desc":"..." }]""",
            Eyebrow = "History",
            TitleA = "Title A",
            TitleB = "Title B",
            Paragraph1 = "P1",
            Paragraph2 = "P2",
            ImageUrl = "https://example.test/images/upload/about.png",
            IsActive = true,
            SortOrder = 4,
        });

        Assert.Equal("timeline-main", result.Slug);
        Assert.Equal("""[{"year":"2006","title":"Founded","desc":"..." }]""", result.ItemsJson);
        Assert.Equal("/images/upload/about.png", result.ImageUrl);

        var saved = _db.AboutSectionContents.Single(x => x.Slug == "timeline-main");
        Assert.Equal(result.ItemsJson, saved.ItemsJson);
        Assert.Equal("/images/upload/about.png", saved.ImageUrl);
    }
}
