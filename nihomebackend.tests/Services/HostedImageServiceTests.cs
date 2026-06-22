using Microsoft.AspNetCore.Hosting;
using Moq;
using NihomeBackend.Services;
using Xunit;

namespace nihomebackend.tests.Services;

public class HostedImageServiceTests : IDisposable
{
    private readonly string _root;
    private readonly HostedImageService _sut;

    public HostedImageServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "hosted-image-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "wwwroot", "images", "upload"));
        _sut = new HostedImageService(Mock.Of<IWebHostEnvironment>(e => e.ContentRootPath == _root));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void NormalizeImageUrl_NullOrEmpty_ReturnsAsIs()
    {
        Assert.Null(_sut.NormalizeImageUrl(null));
        Assert.Equal("", _sut.NormalizeImageUrl(""));
    }

    [Fact]
    public void NormalizeImageUrl_RelativeManagedPath_IsUnchanged()
    {
        Assert.Equal("/images/upload/a.png", _sut.NormalizeImageUrl("/images/upload/a.png"));
    }

    [Fact]
    public void NormalizeImageUrl_AbsoluteManagedUrl_IsStrippedToPath()
    {
        Assert.Equal("/images/upload/a.png",
            _sut.NormalizeImageUrl("https://example.test/images/upload/a.png"));
    }

    [Fact]
    public void NormalizeImageUrl_ExternalUrl_IsUnchanged()
    {
        Assert.Equal("https://cdn.example.com/x.png",
            _sut.NormalizeImageUrl("https://cdn.example.com/x.png"));
    }

    [Fact]
    public void IsManagedUpload_DetectsByPrefix()
    {
        Assert.True(_sut.IsManagedUpload("/images/upload/x.png"));
        Assert.False(_sut.IsManagedUpload("/images/news/x.png"));
        Assert.False(_sut.IsManagedUpload(null));
    }

    [Fact]
    public void DeleteIfManagedUpload_RemovesExistingFile()
    {
        var rel = "/images/upload/file.png";
        var full = Path.Combine(_root, "wwwroot", "images", "upload", "file.png");
        File.WriteAllBytes(full, new byte[] { 1, 2, 3 });

        _sut.DeleteIfManagedUpload(rel);

        Assert.False(File.Exists(full));
    }

    [Fact]
    public void DeleteIfManagedUpload_RemovesFileInBucketSubFolder()
    {
        var rel = "/images/upload/activities/abc.png";
        var dir = Path.Combine(_root, "wwwroot", "images", "upload", "activities");
        Directory.CreateDirectory(dir);
        var full = Path.Combine(dir, "abc.png");
        File.WriteAllBytes(full, new byte[] { 1, 2, 3 });

        _sut.DeleteIfManagedUpload(rel);

        Assert.False(File.Exists(full));
    }

    [Fact]
    public void DeleteIfManagedUpload_IgnoresMissingFile()
    {
        // Should not throw
        _sut.DeleteIfManagedUpload("/images/upload/nope.png");
    }

    [Fact]
    public void DeleteIfManagedUpload_IgnoresNonManaged()
    {
        _sut.DeleteIfManagedUpload("https://cdn/x.png");
        _sut.DeleteIfManagedUpload(null);
    }

    [Fact]
    public void DeleteIfManagedUpload_TraversalAttempt_DoesNotEscapeUploadRoot()
    {
        // Seed a file outside /images/upload/ but inside wwwroot
        var sensitiveDir = Path.Combine(_root, "wwwroot", "config");
        Directory.CreateDirectory(sensitiveDir);
        var sensitive = Path.Combine(sensitiveDir, "secret.json");
        File.WriteAllText(sensitive, "do-not-delete");

        // Forged URL passes the prefix check but resolves outside the upload root
        _sut.DeleteIfManagedUpload("/images/upload/../config/secret.json");

        Assert.True(File.Exists(sensitive));
    }
}
