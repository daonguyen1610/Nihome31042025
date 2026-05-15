using Microsoft.AspNetCore.Hosting;
using Moq;
using NihomeBackend.Models;
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

    [Fact]
    public async Task SaveLegacyAsync_Throws_WhenExtensionIsNotAllowed()
    {
        await using var stream = new MemoryStream([1, 2, 3]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.SaveLegacyAsync(
            stream,
            "malware.exe",
            "application/octet-stream",
            stream.Length,
            ProcessAssetType.File,
            CancellationToken.None));

        Assert.Equal("Legacy process asset has unsupported file extension.", ex.Message);
    }

    [Fact]
    public async Task SaveLegacyAsync_Throws_WhenStreamExceedsConfiguredMaxSizeWithoutContentLength()
    {
        const long maxDocumentBytes = 25 * 1024 * 1024;
        await using var stream = new FixedLengthReadStream(maxDocumentBytes + 1);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.SaveLegacyAsync(
            stream,
            "oversized.pdf",
            "application/pdf",
            null,
            ProcessAssetType.File,
            CancellationToken.None));

        Assert.Equal("Legacy process asset is too large.", ex.Message);

        var filesDir = Path.Combine(_contentRootPath, "wwwroot", "process-assets", "files");
        Assert.False(Directory.Exists(filesDir) && Directory.EnumerateFiles(filesDir).Any());
    }

    private sealed class FixedLengthReadStream(long length) : Stream
    {
        private long _position;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => length;
        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position >= length)
            {
                return 0;
            }

            var bytesToRead = (int)Math.Min(count, length - _position);
            buffer.AsSpan(offset, bytesToRead).Clear();
            _position += bytesToRead;
            return bytesToRead;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => Task.FromResult(Read(buffer, offset, count));

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var bytesRead = Read(buffer.Span);
            return ValueTask.FromResult(bytesRead);
        }

        public override int Read(Span<byte> buffer)
        {
            if (_position >= length)
            {
                return 0;
            }

            var bytesToRead = (int)Math.Min(buffer.Length, length - _position);
            buffer[..bytesToRead].Clear();
            _position += bytesToRead;
            return bytesToRead;
        }

        public override void Flush()
            => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();
    }
}
