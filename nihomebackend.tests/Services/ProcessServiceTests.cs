using Microsoft.AspNetCore.Hosting;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;
using Xunit;

namespace nihomebackend.tests.Services;

public class ProcessServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ProcessService _sut;
    private readonly string _tmpRoot;

    public ProcessServiceTests()
    {
        _db = DbContextFactory.Create();
        _tmpRoot = Path.Combine(Path.GetTempPath(), "nihome-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_tmpRoot, "wwwroot", "files"));
        var env = Mock.Of<IWebHostEnvironment>(e => e.ContentRootPath == _tmpRoot);
        _sut = new ProcessService(_db, env);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_tmpRoot)) Directory.Delete(_tmpRoot, recursive: true);
    }

    private static UpsertProcessRequest Make(string group, string title, params ProcessAssetInput[] files) => new()
    {
        GroupKey = group,
        Title = title,
        Files = files.ToList(),
    };

    [Fact]
    public async Task Create_PersistsAssets()
    {
        var res = await _sut.CreateAsync(Make("general", "Doc 1",
            new ProcessAssetInput { DisplayName = "PDF", Url = "/files/doc.pdf", OriginalFileName = "doc.pdf", ContentType = "application/pdf" }));
        Assert.Single(res.Files);
        Assert.Equal("PDF", res.Files[0].DisplayName);
    }

    [Fact]
    public async Task GetAllGrouped_GroupsByKey()
    {
        await _sut.CreateAsync(Make("g1", "T1"));
        await _sut.CreateAsync(Make("g1", "T2"));
        await _sut.CreateAsync(Make("g2", "T3"));

        var grouped = await _sut.GetAllGroupedAsync();
        Assert.Equal(2, grouped["g1"].Count);
        Assert.Single(grouped["g2"]);
    }

    [Fact]
    public async Task Hydrate_ResolvesFileSizeWhenFileExists()
    {
        var rel = "/files/exists.bin";
        var full = Path.Combine(_tmpRoot, "wwwroot", "files", "exists.bin");
        await File.WriteAllBytesAsync(full, new byte[] { 1, 2, 3, 4, 5 });

        await _sut.CreateAsync(Make("g", "T",
            new ProcessAssetInput { DisplayName = "B", Url = rel, OriginalFileName = "exists.bin", ContentType = "application/octet-stream" }));

        var grouped = await _sut.GetAllGroupedAsync();
        Assert.Equal(5, grouped["g"][0].Files[0].FileSizeBytes);
    }

    [Fact]
    public async Task Hydrate_ReturnsZeroForMissingFile()
    {
        await _sut.CreateAsync(Make("g", "T",
            new ProcessAssetInput { DisplayName = "Gone", Url = "/files/nope.bin", OriginalFileName = "nope.bin", ContentType = "x" }));

        var grouped = await _sut.GetAllGroupedAsync();
        Assert.Equal(0, grouped["g"][0].Files[0].FileSizeBytes);
    }

    [Fact]
    public async Task Update_NonExisting_ReturnsNull()
    {
        Assert.Null(await _sut.UpdateAsync(404, Make("g", "T")));
    }

    [Fact]
    public async Task Update_ReplacesAssets()
    {
        var created = await _sut.CreateAsync(Make("g", "T",
            new ProcessAssetInput { DisplayName = "A", Url = "/files/a.pdf", OriginalFileName = "a.pdf", ContentType = "application/pdf" }));

        var updated = await _sut.UpdateAsync(created.Id, Make("g", "T2",
            new ProcessAssetInput { DisplayName = "B", Url = "/files/b.pdf", OriginalFileName = "b.pdf", ContentType = "application/pdf" }));

        Assert.NotNull(updated);
        Assert.Equal("T2", updated!.Title);
        Assert.Single(updated.Files);
        Assert.Equal("B", updated.Files[0].DisplayName);
    }

    [Fact]
    public async Task Delete_RemovesEntity()
    {
        var created = await _sut.CreateAsync(Make("g", "T"));
        Assert.True(await _sut.DeleteAsync(created.Id));
        Assert.Empty(_db.ProcessDocuments);
    }

    [Fact]
    public async Task Delete_NonExisting_ReturnsFalse()
    {
        Assert.False(await _sut.DeleteAsync(404));
    }
}
