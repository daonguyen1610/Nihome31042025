using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;
using Xunit;

namespace nihomebackend.tests.Services;

/// <summary>
/// These tests reach into the private CleanupOrphanImagesAsync method via
/// reflection. The hosted service loop itself is just a delay+invoke wrapper,
/// so exercising the inner method gives the same coverage without spinning up
/// a real BackgroundService.
/// </summary>
public class UploadedImageCleanupServiceTests : IDisposable
{
    private readonly string _root;
    private readonly string _uploadDir;
    private readonly AppDbContext _db;
    private readonly UploadedImageCleanupService _sut;

    public UploadedImageCleanupServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "upload-cleanup-" + Guid.NewGuid().ToString("N"));
        _uploadDir = Path.Combine(_root, "wwwroot", "images", "upload");
        Directory.CreateDirectory(_uploadDir);

        _db = DbContextFactory.Create();

        var scopeFactory = new Mock<IServiceScopeFactory>();
        var scope = new Mock<IServiceScope>();
        var sp = new ServiceCollection().AddSingleton(_db).BuildServiceProvider();
        scope.Setup(s => s.ServiceProvider).Returns(sp);
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        var env = Mock.Of<IWebHostEnvironment>(e => e.ContentRootPath == _root);

        _sut = new UploadedImageCleanupService(
            scopeFactory.Object,
            env,
            NullLogger<UploadedImageCleanupService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private async Task RunCleanupAsync()
    {
        var method = typeof(UploadedImageCleanupService)
            .GetMethod("CleanupOrphanImagesAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var task = (Task)method.Invoke(_sut, new object[] { CancellationToken.None })!;
        await task;
    }

    private string SeedFile(string relativePath, TimeSpan age)
    {
        var fullPath = Path.Combine(_uploadDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, new byte[] { 1, 2, 3 });
        var stamp = DateTime.UtcNow - age;
        File.SetLastWriteTimeUtc(fullPath, stamp);
        return fullPath;
    }

    [Fact]
    public async Task Cleanup_DeletesOrphans_InsideBucketSubFolders()
    {
        var orphan = SeedFile("activities/orphan.png", TimeSpan.FromHours(48));

        await RunCleanupAsync();

        Assert.False(File.Exists(orphan));
    }

    [Fact]
    public async Task Cleanup_KeepsReferencedFile_InBucket()
    {
        var keepName = $"{Guid.NewGuid():N}.png";
        var keep = SeedFile($"projects/{keepName}", TimeSpan.FromHours(48));

        _db.Projects.Add(new Project
        {
            Slug = "p1",
            ImageUrl = $"/images/upload/projects/{keepName}",
            Name = "P1",
            Client = "",
            Location = "",
            Scale = "",
            Scope = "",
            Status = "ongoing",
            Year = "2024",
            Category = "Nhà máy",
        });
        await _db.SaveChangesAsync();

        await RunCleanupAsync();

        Assert.True(File.Exists(keep));
    }

    [Fact]
    public async Task Cleanup_KeepsReferencedFile_InGalleryJson()
    {
        var galleryName = $"{Guid.NewGuid():N}.jpg";
        var galleryFile = SeedFile($"news/{galleryName}", TimeSpan.FromHours(48));

        _db.NewsArticles.Add(new NewsArticle
        {
            Slug = "n1",
            Date = "2024-01-01",
            ImageUrl = "/images/news/n1/thumb.jpg",
            GalleryJson = JsonSerializer.Serialize(new[] { $"/images/upload/news/{galleryName}" }),
            Category = "tin-tuc",
            Title = "T",
            Excerpt = "E",
            ContentJson = "[]",
        });
        await _db.SaveChangesAsync();

        await RunCleanupAsync();

        Assert.True(File.Exists(galleryFile));
    }

    [Fact]
    public async Task Cleanup_KeepsRecentOrphan_UnderAgeGuard()
    {
        var young = SeedFile("misc/young.png", TimeSpan.FromMinutes(5));

        await RunCleanupAsync();

        Assert.True(File.Exists(young));
    }

    [Fact]
    public async Task Cleanup_SkipsFiles_InUnownedSubFolders()
    {
        var documentsFile = SeedFile("documents/legacy.pdf", TimeSpan.FromDays(7));

        await RunCleanupAsync();

        Assert.True(File.Exists(documentsFile));
    }

    [Fact]
    public async Task Cleanup_PreservesGitkeepInsideBuckets()
    {
        var gitkeep = SeedFile("activities/.gitkeep", TimeSpan.FromDays(30));

        await RunCleanupAsync();

        Assert.True(File.Exists(gitkeep));
    }

    [Fact]
    public async Task Cleanup_DeletesLegacyTopLevelOrphan()
    {
        var legacy = SeedFile("toplevel.png", TimeSpan.FromHours(48));

        await RunCleanupAsync();

        Assert.False(File.Exists(legacy));
    }

    [Fact]
    public async Task Cleanup_KeepsLegacyTopLevelReferencedFile()
    {
        var name = $"{Guid.NewGuid():N}.png";
        var keep = SeedFile(name, TimeSpan.FromHours(48));

        _db.ClientLogos.Add(new ClientLogo
        {
            Name = "Logo",
            ImageUrl = $"/images/upload/{name}",
            Kind = LogoKind.Client,
            SortOrder = 0,
        });
        await _db.SaveChangesAsync();

        await RunCleanupAsync();

        Assert.True(File.Exists(keep));
    }
}
