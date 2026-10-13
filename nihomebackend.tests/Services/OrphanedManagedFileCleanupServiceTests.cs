using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public sealed class OrphanedManagedFileCleanupServiceTests : IDisposable
{
    private readonly string contentRoot = Path.Combine(
        Path.GetTempPath(),
        "nihome-orphan-cleanup-tests",
        Guid.NewGuid().ToString("N"));
    private readonly AppDbContext db = DbContextFactory.Create();

    public void Dispose()
    {
        db.Dispose();
        if (Directory.Exists(contentRoot)) Directory.Delete(contentRoot, recursive: true);
    }

    [Fact]
    public async Task CleanupAsync_DeletesOnlyOldUnreferencedManagedFiles()
    {
        var now = new DateTime(2026, 10, 13, 10, 0, 0, DateTimeKind.Utc);
        var referenced = CreateFile("files/cv/ho-so-nguyen-van-an.pdf", now.AddDays(-3));
        var orphanDocument = CreateFile("files/contracts/orphan-contract.pdf", now.AddDays(-3));
        var orphanImage = CreateFile("images/upload/projects/orphan-image.jpg", now.AddDays(-3));
        var recent = CreateFile("files/tenders/recent-upload.pdf", now.AddHours(-2));
        var quarantined = CreateFile(
            "files/.hard-delete-quarantine/operation/file.pdf",
            now.AddDays(-3));

        db.JobApplications.Add(new JobApplication
        {
            JobPositionId = 1,
            CandidateName = "Nguyen Van An",
            Email = "an@example.com",
            CvUrl = "/files/cv/ho-so-nguyen-van-an.pdf",
        });
        await db.SaveChangesAsync();

        var environment = Mock.Of<IWebHostEnvironment>(item => item.ContentRootPath == contentRoot);
        var service = new OrphanedManagedFileCleanupService(
            db,
            environment,
            NullLogger<OrphanedManagedFileCleanupService>.Instance);

        var result = await service.CleanupAsync(now.AddHours(-24));

        Assert.True(File.Exists(referenced));
        Assert.False(File.Exists(orphanDocument));
        Assert.False(File.Exists(orphanImage));
        Assert.True(File.Exists(recent));
        Assert.True(File.Exists(quarantined));
        Assert.Equal(4, result.TotalFiles);
        Assert.Equal(1, result.ReferencedFiles);
        Assert.Equal(1, result.RecentFiles);
        Assert.Equal(2, result.DeletedFiles);
        Assert.Equal(0, result.FailedFiles);
    }

    [Fact]
    public async Task CleanupAsync_RejectsNonUtcCutoffWithoutDeletingFiles()
    {
        var oldFile = CreateFile("files/contracts/must-remain.pdf", DateTime.UtcNow.AddDays(-3));
        var environment = Mock.Of<IWebHostEnvironment>(item => item.ContentRootPath == contentRoot);
        var service = new OrphanedManagedFileCleanupService(
            db,
            environment,
            NullLogger<OrphanedManagedFileCleanupService>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CleanupAsync(new DateTime(2026, 10, 13, 10, 0, 0, DateTimeKind.Local)));

        Assert.True(File.Exists(oldFile));
    }

    private string CreateFile(string relativePath, DateTime lastWriteUtc)
    {
        var fullPath = Path.Combine(contentRoot, "wwwroot", relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, "test");
        File.SetLastWriteTimeUtc(fullPath, lastWriteUtc);
        return fullPath;
    }
}
