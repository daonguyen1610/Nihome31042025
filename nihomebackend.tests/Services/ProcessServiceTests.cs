using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using Xunit;

namespace nihomebackend.tests.Services;

public class ProcessServiceTests : IDisposable
{
    private readonly string _contentRootPath;

    public ProcessServiceTests()
    {
        _contentRootPath = Path.Combine(Path.GetTempPath(), $"nihome-process-service-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_contentRootPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_contentRootPath))
        {
            Directory.Delete(_contentRootPath, recursive: true);
        }
    }

    [Fact]
    public async Task UpdateAsync_DoesNotDeleteOldManagedAsset_WhenSaveChangesFails()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        await using var seedDb = new AppDbContext(options);
        seedDb.Database.EnsureCreated();
        var process = new ProcessDocument
        {
            GroupKey = "general",
            Title = "Old process",
            SortOrder = 0,
            Assets =
            [
                new ProcessAsset
                {
                    Type = ProcessAssetType.Image,
                    DisplayName = "Old image",
                    Url = "/process-assets/images/old-image.png",
                    OriginalFileName = "old-image.png",
                    ContentType = "image/png",
                    FileSizeBytes = 12,
                    SortOrder = 0,
                },
            ],
        };
        seedDb.ProcessDocuments.Add(process);
        await seedDb.SaveChangesAsync();

        var oldManagedFile = Path.Combine(_contentRootPath, "wwwroot", "process-assets", "images", "old-image.png");
        Directory.CreateDirectory(Path.GetDirectoryName(oldManagedFile)!);
        await File.WriteAllTextAsync(oldManagedFile, "keep-me");

        var storage = new ProcessAssetStorageService(
            Mock.Of<IWebHostEnvironment>(env => env.ContentRootPath == _contentRootPath));

        await using var failingDb = new ThrowingSaveChangesDbContext(options) { ThrowOnSaveChanges = true };
        var sut = new ProcessService(failingDb, storage);

        await Assert.ThrowsAsync<DbUpdateException>(() => sut.UpdateAsync(process.Id, new UpsertProcessRequest
        {
            GroupKey = "general",
            Title = "New process",
            SortOrder = 1,
            Images =
            [
                new UpsertProcessAssetRequest
                {
                    DisplayName = "New image",
                    Url = "/process-assets/images/new-image.png",
                    OriginalFileName = "new-image.png",
                    ContentType = "image/png",
                    FileSizeBytes = 16,
                    SortOrder = 0,
                },
            ],
        }));

        Assert.True(File.Exists(oldManagedFile));
    }

    [Fact]
    public async Task UpdateAsync_DeletesOldManagedAsset_AfterSuccessfulSave()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        await using var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        var process = new ProcessDocument
        {
            GroupKey = "general",
            Title = "Old process",
            SortOrder = 0,
            Assets =
            [
                new ProcessAsset
                {
                    Type = ProcessAssetType.Image,
                    DisplayName = "Old image",
                    Url = "/process-assets/images/old-image.png",
                    OriginalFileName = "old-image.png",
                    ContentType = "image/png",
                    FileSizeBytes = 12,
                    SortOrder = 0,
                },
            ],
        };
        db.ProcessDocuments.Add(process);
        await db.SaveChangesAsync();

        var oldManagedFile = Path.Combine(_contentRootPath, "wwwroot", "process-assets", "images", "old-image.png");
        Directory.CreateDirectory(Path.GetDirectoryName(oldManagedFile)!);
        await File.WriteAllTextAsync(oldManagedFile, "delete-me");

        var storage = new ProcessAssetStorageService(
            Mock.Of<IWebHostEnvironment>(env => env.ContentRootPath == _contentRootPath));

        var sut = new ProcessService(db, storage);
        var updated = await sut.UpdateAsync(process.Id, new UpsertProcessRequest
        {
            GroupKey = "general",
            Title = "New process",
            SortOrder = 1,
            Images =
            [
                new UpsertProcessAssetRequest
                {
                    DisplayName = "New image",
                    Url = "/process-assets/images/new-image.png",
                    OriginalFileName = "new-image.png",
                    ContentType = "image/png",
                    FileSizeBytes = 16,
                    SortOrder = 0,
                },
            ],
        });

        Assert.NotNull(updated);
        Assert.False(File.Exists(oldManagedFile));
    }

    private sealed class ThrowingSaveChangesDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        public bool ThrowOnSaveChanges { get; init; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (ThrowOnSaveChanges)
            {
                throw new DbUpdateException("Injected test failure.");
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
