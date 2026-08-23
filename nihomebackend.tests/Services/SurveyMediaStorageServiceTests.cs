using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Moq;
using NihomeBackend.Services;

namespace nihomebackend.tests.Services;

public sealed class SurveyMediaStorageServiceTests : IDisposable
{
    private readonly string contentRoot = Path.Combine(Path.GetTempPath(), $"survey-media-{Guid.NewGuid():N}");
    private readonly SurveyMediaStorageService service;

    public SurveyMediaStorageServiceTests()
    {
        Directory.CreateDirectory(contentRoot);
        service = new SurveyMediaStorageService(
            Mock.Of<IWebHostEnvironment>(environment => environment.ContentRootPath == contentRoot));
    }

    [Theory]
    [InlineData("photo.JPG", "FFD8FFE0", "image/jpeg")]
    [InlineData("photo.jpeg", "FFD8FFE0", "image/jpeg")]
    [InlineData("diagram.png", "89504E470D0A1A0A", "image/png")]
    [InlineData("camera.heic", "000000186674797068656963", "image/heic")]
    [InlineData("video.mp4", "000000186674797069736F6D", "video/mp4")]
    [InlineData("video.mov", "000000186674797071742020", "video/quicktime")]
    [InlineData("report.pdf", "255044462D312E34", "application/pdf")]
    [InlineData("drawing.dwg", "414331303138", "application/acad")]
    [InlineData("model.rvt", "D0CF11E0A1B11AE1", "application/vnd.autodesk.revit")]
    public async Task StoreAsync_ValidSignature_NormalizesContentType(
        string fileName, string hexBytes, string expectedContentType)
    {
        var result = await service.StoreAsync(12, File(fileName, "application/octet-stream", Convert.FromHexString(hexBytes)));

        Assert.Equal(expectedContentType, result.ContentType);
        Assert.StartsWith("/files/survey-media/12/", result.RelativePath);
        Assert.NotNull(service.GetContent(12, result.RelativePath, result.OriginalFileName, result.ContentType));
    }

    [Fact]
    public async Task StoreAsync_UnsupportedExtension_ReturnsActionableVietnameseError()
    {
        var exception = await Assert.ThrowsAsync<SurveyMediaValidationException>(() =>
            service.StoreAsync(1, File("script.exe", "application/octet-stream", [1])));

        Assert.Contains("Chỉ chấp nhận", exception.Message);
    }

    [Fact]
    public async Task StoreAsync_AllowedExtensionWithSpoofedContent_IsRejectedBeforeWriting()
    {
        var exception = await Assert.ThrowsAsync<SurveyMediaValidationException>(() =>
            service.StoreAsync(1, File("spoofed.jpg", "image/jpeg", [0x4D, 0x5A, 0x90, 0x00])));

        Assert.Contains("không khớp", exception.Message);
        Assert.False(Directory.Exists(Path.Combine(contentRoot, "wwwroot", "files", "survey-media", "1")));
    }

    [Fact]
    public async Task StoreAsync_Over100MiB_IsRejectedBeforeReadingStream()
    {
        var file = new FormFile(Stream.Null, 0, SurveyMediaStorageService.MaxFileSize + 1, "file", "large.mp4");

        var exception = await Assert.ThrowsAsync<SurveyMediaValidationException>(() => service.StoreAsync(1, file));

        Assert.Contains("100 MiB", exception.Message);
    }

    [Fact]
    public void GetContent_PathOutsideParent_IsRejected()
    {
        Assert.Null(service.GetContent(7, "/files/survey-media/8/file.jpg", "file.jpg", "image/jpeg"));
        Assert.Null(service.GetContent(7, "/files/survey-media/7/../8/file.jpg", "file.jpg", "image/jpeg"));
    }

    public void Dispose()
    {
        if (Directory.Exists(contentRoot)) Directory.Delete(contentRoot, true);
    }

    private static FormFile File(string fileName, string contentType, byte[] bytes) => new(
        new MemoryStream(bytes), 0, bytes.Length, "file", fileName)
    {
        Headers = new HeaderDictionary(),
        ContentType = contentType,
    };
}