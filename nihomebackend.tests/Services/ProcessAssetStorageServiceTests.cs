using Microsoft.AspNetCore.Hosting;
using Moq;
using NihomeBackend.Services;
using Xunit;

namespace nihomebackend.tests.Services;

public class ProcessAssetStorageServiceTests : IDisposable
{
    private readonly string _contentRootPath;
    private readonly ProcessAssetStorageService _sut;

    public ProcessAssetStorageServiceTests()
    {
        _contentRootPath = Path.Combine(Path.GetTempPath(), $"nihome-process-asset-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_contentRootPath, "wwwroot", "process-assets"));
        _sut = new ProcessAssetStorageService(
            Mock.Of<IWebHostEnvironment>(env => env.ContentRootPath == _contentRootPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_contentRootPath))
        {
            Directory.Delete(_contentRootPath, recursive: true);
        }
    }

    [Fact]
    public async Task DeleteIfManagedAsset_DoesNotDeleteFileOutsideProcessAssetsRoot_WhenUrlContainsTraversal()
    {
        var outsideFilePath = Path.Combine(_contentRootPath, "wwwroot", "appsettings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(outsideFilePath)!);
        await File.WriteAllTextAsync(outsideFilePath, "keep");

        _sut.DeleteIfManagedAsset("/process-assets/../appsettings.json");

        Assert.True(File.Exists(outsideFilePath));
    }

    [Fact]
    public async Task DeleteIfManagedAsset_DeletesManagedAssetInsideProcessAssetsRoot()
    {
        var managedFilePath = Path.Combine(_contentRootPath, "wwwroot", "process-assets", "images", "asset.png");
        Directory.CreateDirectory(Path.GetDirectoryName(managedFilePath)!);
        await File.WriteAllTextAsync(managedFilePath, "delete");

        _sut.DeleteIfManagedAsset("/process-assets/images/asset.png");

        Assert.False(File.Exists(managedFilePath));
    }
}
