using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Moq;
using NihomeBackend.Models;
using NihomeBackend.Services;

namespace nihomebackend.tests.Services;

public sealed class ProjectDocumentStorageServiceTests : IDisposable
{
    private readonly string contentRoot = Path.Combine(Path.GetTempPath(), $"nihome-project-documents-{Guid.NewGuid():N}");
    private readonly ProjectDocumentStorageService storage;

    public ProjectDocumentStorageServiceTests()
    {
        Directory.CreateDirectory(contentRoot);
        storage = new ProjectDocumentStorageService(Mock.Of<IWebHostEnvironment>(env => env.ContentRootPath == contentRoot));
    }

    [Fact]
    public async Task StoreAsync_ExecutableFile_IsRejectedWithoutCreatingManagedDirectory()
    {
        await Assert.ThrowsAsync<ProjectDocumentValidationException>(() => storage.StoreAsync(12, File("payload", "payload.exe")));
        Assert.False(Directory.Exists(Path.Combine(contentRoot, "wwwroot", "files", "project-documents", "12")));
    }

    [Fact]
    public async Task InspectExistingAsync_TraversalPath_IsRejected()
    {
        await Assert.ThrowsAsync<ProjectDocumentValidationException>(() => storage.InspectExistingAsync(
            ProjectDocumentSourceModule.Design, "/files/design/../contracts/private.pdf", "private.pdf"));
    }

    [Fact]
    public async Task InspectExistingAsync_PermitPath_IsAllowed()
    {
        var directory = Path.Combine(contentRoot, "wwwroot", "files", "business-documents", "permits");
        Directory.CreateDirectory(directory);
        await System.IO.File.WriteAllTextAsync(Path.Combine(directory, "permit.pdf"), "permit");

        var stored = await storage.InspectExistingAsync(
            ProjectDocumentSourceModule.Design,
            "/files/business-documents/permits/permit.pdf",
            "permit.pdf");

        Assert.Equal("/files/business-documents/permits/permit.pdf", stored.LocalPath);
    }

    [Theory]
    [InlineData(ProjectDocumentSourceModule.Acceptance, "acceptance")]
    [InlineData(ProjectDocumentSourceModule.Acceptance, "as-built")]
    [InlineData(ProjectDocumentSourceModule.Handover, "handover")]
    public async Task InspectExistingAsync_ProjectCloseoutPaths_AreAllowed(
        ProjectDocumentSourceModule sourceModule, string area)
    {
        var directory = Path.Combine(contentRoot, "wwwroot", "files", "business-documents", area);
        Directory.CreateDirectory(directory);
        await System.IO.File.WriteAllTextAsync(Path.Combine(directory, "document.pdf"), "document");
        var path = $"/files/business-documents/{area}/document.pdf";

        var stored = await storage.InspectExistingAsync(sourceModule, path, "document.pdf");

        Assert.Equal(path, stored.LocalPath);
    }

    [Fact]
    public async Task DeleteOwned_SourceManagedFile_DoesNotDeleteSourceFile()
    {
        var directory = Path.Combine(contentRoot, "wwwroot", "files", "design", "basic");
        Directory.CreateDirectory(directory);
        var fullPath = Path.Combine(directory, "drawing.pdf");
        await System.IO.File.WriteAllTextAsync(fullPath, "drawing");

        storage.DeleteOwned(12, "/files/design/basic/drawing.pdf");

        Assert.True(System.IO.File.Exists(fullPath));
    }

    [Fact]
    public async Task StoreDriveImportAsync_StreamOverMaximum_IsRejectedAndRemoved()
    {
        var chunk = new byte[1024 * 1024];

        await Assert.ThrowsAsync<ProjectDocumentValidationException>(() => storage.StoreDriveImportAsync(
            12, "large.pdf", "application/pdf", null, async (destination, cancellationToken) =>
            {
                for (var index = 0; index < 101; index++)
                    await destination.WriteAsync(chunk, cancellationToken);
            }));

        var directory = Path.Combine(contentRoot, "wwwroot", "files", "project-documents", "12");
        Assert.True(!Directory.Exists(directory) || Directory.GetFiles(directory).Length == 0);
    }

    [Fact]
    public void MultipartLimit_IncludesEnvelopeOverhead()
    {
        Assert.Equal(106L * 1024 * 1024, ProjectDocumentStorageService.MultipartBodyLengthLimit);
        Assert.Equal(100L * 1024 * 1024, ProjectDocumentStorageService.MaxFileSize);
    }

    private static FormFile File(string content, string name)
    {
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        return new FormFile(stream, 0, stream.Length, "file", name) { Headers = new HeaderDictionary() };
    }

    public void Dispose()
    {
        if (Directory.Exists(contentRoot)) Directory.Delete(contentRoot, true);
    }
}
