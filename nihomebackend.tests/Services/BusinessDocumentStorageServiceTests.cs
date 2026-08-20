using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Moq;
using NihomeBackend.Services;

namespace nihomebackend.tests.Services;

public class BusinessDocumentStorageServiceTests : IDisposable
{
    private readonly string _contentRoot = Path.Combine(
        Path.GetTempPath(), $"nihome-business-documents-{Guid.NewGuid():N}");
    private readonly BusinessDocumentStorageService _sut;

    public BusinessDocumentStorageServiceTests()
    {
        Directory.CreateDirectory(_contentRoot);
        _sut = new BusinessDocumentStorageService(
            Mock.Of<IWebHostEnvironment>(environment => environment.ContentRootPath == _contentRoot));
    }

    public void Dispose()
    {
        if (Directory.Exists(_contentRoot)) Directory.Delete(_contentRoot, recursive: true);
    }

    [Fact]
    public async Task StoreAsync_ValidFile_WritesUnderManagedWebRoot()
    {
        var result = await _sut.StoreAsync(CreateFile("evidence.pdf"), BusinessDocumentArea.Acceptance);

        Assert.StartsWith("/files/business-documents/acceptance/", result.Path);
        Assert.Equal("evidence.pdf", result.OriginalFileName);
        Assert.True(File.Exists(Path.Combine(
            _contentRoot,
            "wwwroot",
            result.Path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public async Task StoreAsync_UnsupportedExtension_ThrowsWithoutCreatingDirectory()
    {
        await Assert.ThrowsAsync<BusinessDocumentStorageException>(() =>
            _sut.StoreAsync(CreateFile("payload.exe"), BusinessDocumentArea.Vendors));

        Assert.False(Directory.Exists(Path.Combine(_contentRoot, "wwwroot")));
    }

    [Fact]
    public async Task StoreAsync_EmptyFile_Throws()
    {
        var file = new FormFile(Stream.Null, 0, 0, "file", "empty.pdf");

        await Assert.ThrowsAsync<BusinessDocumentStorageException>(() =>
            _sut.StoreAsync(file, BusinessDocumentArea.Handover));
    }

    private static FormFile CreateFile(string fileName)
    {
        var stream = new MemoryStream("document"u8.ToArray());
        return new FormFile(stream, 0, stream.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf",
        };
    }
}