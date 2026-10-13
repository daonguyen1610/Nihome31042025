using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NihomeBackend.Services;

namespace nihomebackend.tests.Services;

public sealed class OrphanedManagedFileCleanupServiceTests : IDisposable
{
    private readonly string contentRoot = Path.Combine(
        Path.GetTempPath(),
        "nihome-orphan-cleanup-tests",
        Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(contentRoot)) Directory.Delete(contentRoot, recursive: true);
    }

    [Fact]
    public async Task CleanupAsync_QuarantinesOnlyOldUnreferencedManagedFiles()
    {
        var now = new DateTime(2026, 10, 13, 10, 0, 0, DateTimeKind.Utc);
        var referenced = CreateFile("files/cv/ho-so-nguyen-van-an.pdf", now.AddDays(-3));
        var orphanDocument = CreateFile("files/contracts/orphan-contract.pdf", now.AddDays(-3));
        var orphanImage = CreateFile("images/upload/projects/orphan-image.jpg", now.AddDays(-3));
        var recent = CreateFile("files/tenders/recent-upload.pdf", now.AddHours(-2));
        var hardDeleteQuarantine = CreateFile(
            "files/.hard-delete-quarantine/operation/file.pdf",
            now.AddDays(-3));
        var references = CreateReferenceProvider("/files/cv/ho-so-nguyen-van-an.pdf");

        var result = await CreateService(references.Object).CleanupAsync(now);

        Assert.True(File.Exists(referenced));
        Assert.False(File.Exists(orphanDocument));
        Assert.False(File.Exists(orphanImage));
        Assert.True(File.Exists(QuarantinePath("files/contracts/orphan-contract.pdf")));
        Assert.True(File.Exists(QuarantinePath("images/upload/projects/orphan-image.jpg")));
        Assert.True(File.Exists(recent));
        Assert.True(File.Exists(hardDeleteQuarantine));
        Assert.Equal(4, result.TotalFiles);
        Assert.Equal(1, result.ReferencedFiles);
        Assert.Equal(1, result.RecentFiles);
        Assert.Equal(2, result.QuarantinedFiles);
        Assert.Equal(0, result.RestoredFiles);
        Assert.Equal(0, result.PurgedFiles);
        Assert.Equal(0, result.FailedFiles);
    }

    [Fact]
    public async Task CleanupAsync_RestoresQuarantinedFileWhenItBecomesReferenced()
    {
        var now = new DateTime(2026, 10, 13, 10, 0, 0, DateTimeKind.Utc);
        var managedPath = "/files/contracts/newly-linked.pdf";
        var originalPath = CreateFile(managedPath.TrimStart('/'), now.AddDays(-3));

        await CreateService(CreateReferenceProvider().Object).CleanupAsync(now);
        Assert.False(File.Exists(originalPath));

        var result = await CreateService(CreateReferenceProvider(managedPath).Object)
            .CleanupAsync(now.AddHours(1));

        Assert.True(File.Exists(originalPath));
        Assert.False(File.Exists(QuarantinePath(managedPath.TrimStart('/'))));
        Assert.Equal(1, result.RestoredFiles);
        Assert.Equal(0, result.PurgedFiles);
    }

    [Fact]
    public async Task CleanupAsync_PurgesUnreferencedFileOnlyAfterQuarantineRetention()
    {
        var now = new DateTime(2026, 10, 13, 10, 0, 0, DateTimeKind.Utc);
        var managedPath = "files/contracts/expired-orphan.pdf";
        CreateFile(managedPath, now.AddDays(-3));
        var service = CreateService(CreateReferenceProvider().Object);

        await service.CleanupAsync(now);
        var quarantinePath = QuarantinePath(managedPath);
        Assert.True(File.Exists(quarantinePath));

        var result = await service.CleanupAsync(now.AddHours(25));

        Assert.False(File.Exists(quarantinePath));
        Assert.Equal(1, result.PurgedFiles);
    }

    [Fact]
    public async Task CleanupAsync_RechecksReferencesBeforeQuarantining()
    {
        var now = new DateTime(2026, 10, 13, 10, 0, 0, DateTimeKind.Utc);
        var managedPath = "/files/contracts/linked-during-scan.pdf";
        var originalPath = CreateFile(managedPath.TrimStart('/'), now.AddDays(-3));
        var provider = new Mock<IManagedFileReferenceProvider>();
        provider.SetupSequence(item => item.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([])
            .ReturnsAsync([])
            .ReturnsAsync(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { managedPath })
            .ReturnsAsync(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { managedPath });

        var result = await CreateService(provider.Object).CleanupAsync(now);

        Assert.True(File.Exists(originalPath));
        Assert.False(File.Exists(QuarantinePath(managedPath.TrimStart('/'))));
        Assert.Equal(0, result.QuarantinedFiles);
        Assert.Equal(1, result.ReferencedFiles);
    }

    [Fact]
    public async Task CleanupAsync_RestoresFileReferencedImmediatelyAfterQuarantineMove()
    {
        var now = new DateTime(2026, 10, 13, 10, 0, 0, DateTimeKind.Utc);
        var managedPath = "/files/contracts/linked-after-move.pdf";
        var originalPath = CreateFile(managedPath.TrimStart('/'), now.AddDays(-3));
        var provider = new Mock<IManagedFileReferenceProvider>();
        provider.SetupSequence(item => item.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([])
            .ReturnsAsync([])
            .ReturnsAsync([])
            .ReturnsAsync(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { managedPath });

        var result = await CreateService(provider.Object).CleanupAsync(now);

        Assert.True(File.Exists(originalPath));
        Assert.False(File.Exists(QuarantinePath(managedPath.TrimStart('/'))));
        Assert.Equal(0, result.QuarantinedFiles);
        Assert.Equal(1, result.RestoredFiles);
    }

    [Fact]
    public async Task CleanupAsync_RechecksReferencesImmediatelyBeforePermanentPurge()
    {
        var now = new DateTime(2026, 10, 13, 10, 0, 0, DateTimeKind.Utc);
        var managedPath = "/files/contracts/linked-before-purge.pdf";
        var originalPath = CreateFile(managedPath.TrimStart('/'), now.AddDays(-3));
        await CreateService(CreateReferenceProvider().Object).CleanupAsync(now);

        var provider = new Mock<IManagedFileReferenceProvider>();
        provider.SetupSequence(item => item.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([])
            .ReturnsAsync(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { managedPath })
            .ReturnsAsync(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { managedPath })
            .ReturnsAsync(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { managedPath });

        var result = await CreateService(provider.Object).CleanupAsync(now.AddHours(25));

        Assert.True(File.Exists(originalPath));
        Assert.False(File.Exists(QuarantinePath(managedPath.TrimStart('/'))));
        Assert.Equal(1, result.RestoredFiles);
        Assert.Equal(0, result.PurgedFiles);
    }

    [Fact]
    public async Task CleanupAsync_RollsBackMovedFileWhenFinalReferenceScanFails()
    {
        var now = new DateTime(2026, 10, 13, 10, 0, 0, DateTimeKind.Utc);
        var managedPath = "/files/contracts/rollback.pdf";
        var originalPath = CreateFile(managedPath.TrimStart('/'), now.AddDays(-3));
        var provider = new Mock<IManagedFileReferenceProvider>();
        provider.SetupSequence(item => item.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([])
            .ReturnsAsync([])
            .ReturnsAsync([])
            .ThrowsAsync(new InvalidDataException("Reference source unavailable"));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            CreateService(provider.Object).CleanupAsync(now));

        Assert.True(File.Exists(originalPath));
        Assert.False(File.Exists(QuarantinePath(managedPath.TrimStart('/'))));
    }

    [Fact]
    public async Task CleanupAsync_RejectsManagedRootSymlink()
    {
        if (!OperatingSystem.IsLinux()) return;

        var now = new DateTime(2026, 10, 13, 10, 0, 0, DateTimeKind.Utc);
        var outsideRoot = Path.Combine(Path.GetTempPath(), $"nihome-cleanup-outside-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outsideRoot);
        var outsideFile = Path.Combine(outsideRoot, "must-remain.pdf");
        await File.WriteAllTextAsync(outsideFile, "test");
        Directory.CreateDirectory(Path.Combine(contentRoot, "wwwroot"));
        Directory.CreateSymbolicLink(Path.Combine(contentRoot, "wwwroot", "files"), outsideRoot);

        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService(CreateReferenceProvider().Object).CleanupAsync(now));
            Assert.True(File.Exists(outsideFile));
        }
        finally
        {
            Directory.Delete(outsideRoot, recursive: true);
        }
    }

    [Fact]
    public async Task CleanupAsync_RejectsNonUtcTimeWithoutMovingFiles()
    {
        var oldFile = CreateFile("files/contracts/must-remain.pdf", DateTime.UtcNow.AddDays(-3));
        var service = CreateService(CreateReferenceProvider().Object);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CleanupAsync(new DateTime(2026, 10, 13, 10, 0, 0, DateTimeKind.Local)));

        Assert.True(File.Exists(oldFile));
    }

    private OrphanedManagedFileCleanupService CreateService(IManagedFileReferenceProvider provider)
    {
        var environment = Mock.Of<IWebHostEnvironment>(item => item.ContentRootPath == contentRoot);
        return new OrphanedManagedFileCleanupService(
            provider,
            environment,
            NullLogger<OrphanedManagedFileCleanupService>.Instance);
    }

    private static Mock<IManagedFileReferenceProvider> CreateReferenceProvider(params string[] paths)
    {
        var provider = new Mock<IManagedFileReferenceProvider>();
        provider.Setup(item => item.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(paths, StringComparer.OrdinalIgnoreCase));
        return provider;
    }

    private string QuarantinePath(string relativePath) =>
        Path.Combine(contentRoot, ".managed-file-quarantine", relativePath.Replace('/', Path.DirectorySeparatorChar));

    private string CreateFile(string relativePath, DateTime lastWriteUtc)
    {
        var fullPath = Path.Combine(contentRoot, "wwwroot", relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, "test");
        File.SetLastWriteTimeUtc(fullPath, lastWriteUtc);
        return fullPath;
    }
}
