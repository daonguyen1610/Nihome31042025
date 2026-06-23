using Microsoft.AspNetCore.Hosting;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace nihomebackend.tests.Services;

public class LogoServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly LogoService _sut;

    public LogoServiceTests()
    {
        _db = DbContextFactory.Create();
        var hosted = new HostedImageService(Mock.Of<IWebHostEnvironment>(e => e.ContentRootPath == "/tmp"));
        _sut = new LogoService(_db, hosted, NullLogger<LogoService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private static UpsertLogoRequest Make(string name, string kind, int order = 0) => new()
    {
        Name = name,
        ImageUrl = "/images/logos/x.png",
        Href = "https://example.com",
        Kind = kind,
        SortOrder = order,
    };

    [Fact]
    public async Task GetAllGroupedAsync_BucketsByKind()
    {
        await _sut.CreateAsync(Make("c1", "Client"));
        await _sut.CreateAsync(Make("p1", "Partner"));
        await _sut.CreateAsync(Make("s1", "Supplier"));
        await _sut.CreateAsync(Make("a1", "Award"));

        var grouped = await _sut.GetAllGroupedAsync();

        Assert.Single(grouped.Clients);
        Assert.Single(grouped.Partners);
        Assert.Single(grouped.Suppliers);
        Assert.Single(grouped.Awards);
    }

    [Fact]
    public async Task Create_AcceptsLowercaseKind()
    {
        var res = await _sut.CreateAsync(Make("lc", "client"));
        Assert.Equal("Client", res.Kind);
    }

    [Fact]
    public async Task Create_WithInvalidKind_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.CreateAsync(Make("bad", "Unknown")));
    }

    [Fact]
    public async Task Update_NonExisting_ReturnsNull()
    {
        Assert.Null(await _sut.UpdateAsync(404, Make("x", "Client")));
    }

    [Fact]
    public async Task Update_PersistsChanges()
    {
        var created = await _sut.CreateAsync(Make("orig", "Client"));
        var req = Make("renamed", "Partner");
        var updated = await _sut.UpdateAsync(created.Id, req);
        Assert.NotNull(updated);
        Assert.Equal("renamed", updated!.Name);
        Assert.Equal("Partner", updated.Kind);
    }

    [Fact]
    public async Task Delete_RemovesEntity()
    {
        var created = await _sut.CreateAsync(Make("d", "Client"));
        Assert.True(await _sut.DeleteAsync(created.Id));
        Assert.Empty(_db.ClientLogos);
    }

    [Fact]
    public async Task Delete_NonExisting_ReturnsFalse()
    {
        Assert.False(await _sut.DeleteAsync(404));
    }
}
