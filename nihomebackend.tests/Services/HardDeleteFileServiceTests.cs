using Microsoft.AspNetCore.Hosting;
using Moq;
using NihomeBackend.Services.HardDelete;

namespace nihomebackend.tests.Services;

public sealed class HardDeleteFileServiceTests : IDisposable
{
    private readonly string contentRoot = Path.Combine(Path.GetTempPath(), $"nihome-hard-delete-{Guid.NewGuid():N}");
    private readonly HardDeleteFileService service;

    public HardDeleteFileServiceTests()
    {
        Directory.CreateDirectory(Path.Combine(contentRoot, "wwwroot"));
        service = new HardDeleteFileService(
            Mock.Of<IWebHostEnvironment>(environment => environment.ContentRootPath == contentRoot),
            new HardDeleteFileOptions());
    }

    [Theory]
    [InlineData("files/quotes/a.pdf")]
    [InlineData("/files/quotes")]
    [InlineData("/files/quotes/../contracts/a.pdf")]
    [InlineData("/files/quotes/%2e%2e/a.pdf")]
    [InlineData("/files/quotes\\a.pdf")]
    [InlineData("/images/upload/a.png")]
    [InlineData("/files/.hard-delete-quarantine/op/a.pdf")]
    public void ValidateManagedPath_UnsafeOrUnmanagedPath_IsRejected(string path)
    {
        var exception = Assert.Throws<HardDeleteFileException>(() => service.ValidateManagedPath(path));

        Assert.Equal("invalid_managed_path", exception.Code);
    }

    [Fact]
    public void ValidateManagedPath_FileUnderExplicitPrivateRoot_IsAccepted()
    {
        Assert.Equal("/files/project-documents/42/a.pdf",
            service.ValidateManagedPath("/files/project-documents/42/a.pdf"));
    }

    [Fact]
    public async Task QuarantineAndRestore_RepeatedCalls_AreIdempotent()
    {
        const string managedPath = "/files/quotes/42/evidence.pdf";
        WriteManagedFile(managedPath, "proof");
        var operationId = Guid.NewGuid();

        var first = await service.QuarantineAsync(operationId, managedPath);
        var second = await service.QuarantineAsync(operationId, managedPath);

        Assert.False(first.WasMissing);
        Assert.Equal(first.QuarantinePath, second.QuarantinePath);
        Assert.False(File.Exists(FullPath(managedPath)));

        await service.RestoreAsync(managedPath, first.QuarantinePath);
        await service.RestoreAsync(managedPath, first.QuarantinePath);

        Assert.Equal("proof", await File.ReadAllTextAsync(FullPath(managedPath)));
    }

    [Fact]
    public async Task QuarantineAndPurge_RepeatedPurge_RemainsSuccessful()
    {
        const string managedPath = "/files/contracts/contract.pdf";
        WriteManagedFile(managedPath, "contract");
        var quarantined = await service.QuarantineAsync(Guid.NewGuid(), managedPath);

        await service.PurgeAsync(quarantined.QuarantinePath);
        await service.PurgeAsync(quarantined.QuarantinePath);

        Assert.False(File.Exists(FullPath(managedPath)));
    }

    [Fact]
    public async Task Quarantine_MissingManagedFile_IsSuccessfulNoOp()
    {
        var result = await service.QuarantineAsync(Guid.NewGuid(), "/files/tenders/missing.pdf");

        Assert.True(result.WasMissing);
        Assert.Null(result.QuarantinePath);
    }

    private void WriteManagedFile(string managedPath, string content)
    {
        var fullPath = FullPath(managedPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    private string FullPath(string managedPath) => Path.Combine(
        contentRoot, "wwwroot", managedPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

    public void Dispose()
    {
        if (Directory.Exists(contentRoot)) Directory.Delete(contentRoot, recursive: true);
    }
}