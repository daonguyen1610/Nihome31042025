using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;
using Xunit;

namespace nihomebackend.tests.Services;

public sealed class ReferencedManagedFilesTests : IDisposable
{
    private readonly AppDbContext _db = DbContextFactory.Create();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetAsync_ProtectsServiceItemIntroBlockImage()
    {
        // ServiceItem was previously never scanned at all, so every image
        // referenced only from IntroBlocksJson looked orphaned and was
        // deleted by the orphan-file worker after 24h.
        _db.ServiceItems.Add(new ServiceItem
        {
            Slug = "xay-dung",
            Title = "Xay dung",
            IntroBlocksJson = """[{"text":"intro","imageUrl":"/images/upload/services/abc123.jpg"}]""",
        });
        await _db.SaveChangesAsync();

        var referenced = await ReferencedManagedFiles.GetAsync(_db);

        Assert.Contains("/images/upload/services/abc123.jpg", referenced);
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

        var referenced = await ReferencedManagedFiles.GetAsync(_db);

        Assert.Contains("/images/upload/about/company.png", referenced);
        Assert.Contains("/images/upload/about/site.png", referenced);
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

        var referenced = await ReferencedManagedFiles.GetAsync(_db);

        Assert.Contains("/images/upload/news/khai-truong/inline.jpg", referenced);
        Assert.Contains("/images/upload/activities/hop-mat/inline.jpg", referenced);
    }

    [Fact]
    public async Task GetAsync_ProtectsInlineContentBlockImagesInProjects()
    {
        // Project.ContentJson was never scanned (unlike Activity/NewsArticle
        // ContentJson above), so an image used only in a project's content
        // blocks looked orphaned and was deleted by the 24h cleanup job.
        _db.Projects.Add(new Project
        {
            Slug = "nha-may-abc",
            Name = "Nha may ABC",
            ContentJson = """[{"type":"image","url":"/images/upload/projects/nha-may-abc/inline.jpg"}]""",
        });
        await _db.SaveChangesAsync();

        var referenced = await ReferencedManagedFiles.GetAsync(_db);

        Assert.Contains("/images/upload/projects/nha-may-abc/inline.jpg", referenced);
    }

    [Fact]
    public async Task GetAsync_ProtectsProcessImagesAndFiles()
    {
        _db.ProcessDocuments.Add(new ProcessDocument
        {
            GroupKey = "quality",
            Title = "Quy trinh",
            ImagesJson = """["/images/processes/quality/process1.png"]""",
            FilesJson = """[{"url":"/files/processes/quality/iso-checklist.pdf"}]""",
        });
        await _db.SaveChangesAsync();

        var referenced = await ReferencedManagedFiles.GetAsync(_db);

        Assert.Contains("/images/processes/quality/process1.png", referenced);
        Assert.Contains("/files/processes/quality/iso-checklist.pdf", referenced);
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

        var referenced = await ReferencedManagedFiles.GetAsync(_db);

        Assert.Empty(referenced);
    }

    [Fact]
    public async Task GetAsync_RejectsMalformedActiveJsonInsteadOfMissingAReference()
    {
        _db.ProcessDocuments.Add(new ProcessDocument
        {
            GroupKey = "safety",
            Title = "Quy trinh an toan",
            FilesJson = "[\"/files/processes/safety/checklist.pdf\"",
        });
        await _db.SaveChangesAsync();

        await Assert.ThrowsAnyAsync<System.Text.Json.JsonException>(() =>
            ReferencedManagedFiles.GetAsync(_db));
    }

    [Fact]
    public async Task GetAsync_ProtectsManagedDocumentsAcrossBackendModules()
    {
        _db.UserDocuments.Add(new UserDocument
        {
            UserId = 10,
            OriginalName = "cccd.pdf",
            FileUrl = "/images/upload/documents/cccd.pdf",
            ContentType = "application/pdf",
        });
        _db.JobApplications.Add(new JobApplication
        {
            JobPositionId = 20,
            CandidateName = "Nguyen Van An",
            Email = "an@example.com",
            CvUrl = "/files/cv/nguyen-van-an.pdf",
        });
        _db.PaymentRequestAttachments.Add(new PaymentRequestAttachment
        {
            PaymentRequestId = 30,
            FileName = "de-nghi-thanh-toan.pdf",
            FilePath = "/files/business-documents/finance/de-nghi-thanh-toan.pdf",
        });
        _db.HseViolations.Add(new HseViolation
        {
            OperationalProjectId = 40,
            Code = "HSE-001",
            OfflineClientId = "offline-hse-001",
            Location = "Nha may Nicon",
            Category = "Safety",
            Description = "Bien ban hien truong",
            EvidenceDocumentsJson = """["/files/business-documents/construction/hse-photo.jpg"]""",
            ResponsibleSiteUserId = 10,
            CreatedByUserId = 10,
            UpdatedByUserId = 10,
        });
        await _db.SaveChangesAsync();

        var referenced = await ReferencedManagedFiles.GetAsync(_db);

        Assert.Contains("/images/upload/documents/cccd.pdf", referenced);
        Assert.Contains("/files/cv/nguyen-van-an.pdf", referenced);
        Assert.Contains("/files/business-documents/finance/de-nghi-thanh-toan.pdf", referenced);
        Assert.Contains("/files/business-documents/construction/hse-photo.jpg", referenced);
    }
}
