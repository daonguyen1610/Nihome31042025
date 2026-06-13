using System.Text.Json;
using NihomeBackend.Data;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;
using Xunit;

namespace nihomebackend.tests.Services;

public class ServiceItemServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ServiceItemService _sut;

    public ServiceItemServiceTests()
    {
        _db = DbContextFactory.Create();
        _sut = new ServiceItemService(_db);
    }

    public void Dispose() => _db.Dispose();

    private static UpsertServiceRequest BasePayload(string slug = "s1") => new()
    {
        Slug = slug,
        Title = "Title",
        ShortTitle = "Short",
        Tagline = "Tag",
        Intro = "Intro",
        Sections = JsonSerializer.Deserialize<JsonElement>("""[{"name":"sec1"}]"""),
        Highlights = new[] { "h1", "h2" },
        SortOrder = 0,
    };

    [Fact]
    public async Task Create_PersistsAndRoundtripsSectionsAndHighlights()
    {
        var res = await _sut.CreateAsync(BasePayload());
        Assert.Equal("s1", res.Slug);
        Assert.Equal(new[] { "h1", "h2" }, res.Highlights);
        Assert.Single(_db.ServiceItems);
    }

    [Fact]
    public async Task GetAll_OrdersBySortOrder()
    {
        var a = BasePayload("a"); a.SortOrder = 10; await _sut.CreateAsync(a);
        var b = BasePayload("b"); b.SortOrder = 1; await _sut.CreateAsync(b);
        var list = await _sut.GetAllAsync();
        Assert.Equal(new[] { "b", "a" }, list.Select(s => s.Slug));
    }

    [Fact]
    public async Task GetBySlug_Missing_ReturnsNull()
    {
        Assert.Null(await _sut.GetBySlugAsync("missing"));
    }

    [Fact]
    public async Task Update_NonExisting_ReturnsNull()
    {
        Assert.Null(await _sut.UpdateAsync(99, BasePayload()));
    }

    [Fact]
    public async Task Update_ChangesFields()
    {
        var created = await _sut.CreateAsync(BasePayload("u"));
        var req = BasePayload("u");
        req.Title = "New Title";
        req.Highlights = Array.Empty<string>();

        var updated = await _sut.UpdateAsync(created.Id, req);
        Assert.NotNull(updated);
        Assert.Equal("New Title", updated!.Title);
        Assert.Empty(updated.Highlights);
    }

    [Fact]
    public async Task Delete_RemovesEntity()
    {
        var created = await _sut.CreateAsync(BasePayload("d"));
        Assert.True(await _sut.DeleteAsync(created.Id));
        Assert.Empty(_db.ServiceItems);
    }

    [Fact]
    public async Task Delete_NonExisting_ReturnsFalse()
    {
        Assert.False(await _sut.DeleteAsync(404));
    }
}
