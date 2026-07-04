using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;
using Xunit;

namespace nihomebackend.tests.Services;

public class ReferencedUploadedImagesTests : IDisposable
{
    private readonly AppDbContext _db = DbContextFactory.Create();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetAsync_ProtectsServiceItemIntroBlockImage()
    {
        // ServiceItem was previously never scanned at all, so every image
        // referenced only from IntroBlocksJson looked orphaned and was
        // deleted by UploadedImageCleanupService after 24h.
        _db.ServiceItems.Add(new ServiceItem
        {
            Slug = "xay-dung",
            Title = "Xay dung",
            IntroBlocksJson = """[{"text":"intro","imageUrl":"/images/upload/services/abc123.jpg"}]""",
        });
        await _db.SaveChangesAsync();

        var referenced = await ReferencedUploadedImages.GetAsync(_db);

        Assert.Contains("services/abc123.jpg", referenced);
    }

    [Fact]
    public async Task GetAsync_ProtectsAboutOrgChartImagesWithNonStandardKeys()
    {
        // Org-chart images are stored under "companyChartUrl"/"siteChartUrl"
        // keys, not "imageUrl" — the old scanner only matched properties
        // literally named "imageUrl" and missed these.
        _db.AboutSectionContents.Add(new AboutSectionContent
        {
            Slug = "organization",
            ItemsJson = """{"companyChartUrl":"/images/upload/about/company.png","siteChartUrl":"/images/upload/about/site.png"}""",
        });
        await _db.SaveChangesAsync();

        var referenced = await ReferencedUploadedImages.GetAsync(_db);

        Assert.Contains("about/company.png", referenced);
        Assert.Contains("about/site.png", referenced);
    }

    [Fact]
    public async Task GetAsync_ProtectsInlineContentBlockImagesInNewsAndActivities()
    {
        // Rich content blocks store inline images as { type: "image", url: "..." }
        // — the old scanner never looked at ContentJson at all.
        _db.NewsArticles.Add(new NewsArticle
        {
            Slug = "khai-truong",
            Title = "Khai truong",
            ContentJson = """[{"type":"image","url":"/images/upload/news/khai-truong/inline.jpg"}]""",
        });
        _db.Activities.Add(new Activity
        {
            Slug = "hop-mat",
            Title = "Hop mat",
            ContentJson = """[{"type":"image","url":"/images/upload/activities/hop-mat/inline.jpg"}]""",
        });
        await _db.SaveChangesAsync();

        var referenced = await ReferencedUploadedImages.GetAsync(_db);

        Assert.Contains("news/khai-truong/inline.jpg", referenced);
        Assert.Contains("activities/hop-mat/inline.jpg", referenced);
    }

    [Fact]
    public async Task GetAsync_ProtectsProcessDocumentImages()
    {
        _db.ProcessDocuments.Add(new ProcessDocument
        {
            GroupKey = "quality",
            Title = "Quy trinh",
            ImagesJson = """["/images/upload/misc/process1.png"]""",
        });
        await _db.SaveChangesAsync();

        var referenced = await ReferencedUploadedImages.GetAsync(_db);

        Assert.Contains("misc/process1.png", referenced);
    }

    [Fact]
    public async Task GetAsync_IgnoresUnmanagedStringsAndTextContent()
    {
        _db.ServiceItems.Add(new ServiceItem
        {
            Slug = "tu-van",
            Title = "Tu van",
            IntroBlocksJson = """[{"text":"Just some plain text, not a URL"}]""",
        });
        await _db.SaveChangesAsync();

        var referenced = await ReferencedUploadedImages.GetAsync(_db);

        Assert.Empty(referenced);
    }
}
