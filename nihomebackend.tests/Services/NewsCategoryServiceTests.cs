using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace nihomebackend.tests.Services;

public class NewsCategoryServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly NewsCategoryService _sut;

    public NewsCategoryServiceTests()
    {
        _db = DbContextFactory.Create();
        _sut = new NewsCategoryService(_db, NullLogger<NewsCategoryService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetAll_SeedsFromExistingNews_WhenEmpty()
    {
        _db.NewsArticles.AddRange(
            new NewsArticle { Slug = "n1", Category = "Company", Title = "T", Excerpt = "E", ContentJson = "[]" },
            new NewsArticle { Slug = "n2", Category = "Project", Title = "T", Excerpt = "E", ContentJson = "[]" },
            new NewsArticle { Slug = "n3", Category = "Company", Title = "T", Excerpt = "E", ContentJson = "[]" });
        await _db.SaveChangesAsync();

        var result = await _sut.GetAllAsync();

        Assert.Equal(["Company", "Project"], result.Select(c => c.Name).ToArray());
        Assert.All(result, c => Assert.Equal(c.Name, c.NameVi));
    }

    [Fact]
    public async Task GetAll_FallsBackNameVi_ForLegacyRows()
    {
        _db.NewsCategories.Add(new NewsCategory { Name = "Legacy", NameVi = "", IsActive = true, SortOrder = 1 });
        await _db.SaveChangesAsync();

        var result = await _sut.GetAllAsync();

        Assert.Equal("Legacy", result[0].NameVi);
    }

    [Fact]
    public async Task Update_RenamesLinkedNewsCategories()
    {
        var category = new NewsCategory { Name = "Old", IsActive = true, SortOrder = 1 };
        _db.NewsCategories.Add(category);
        await _db.SaveChangesAsync();

        _db.NewsArticles.Add(new NewsArticle
        {
            Slug = "linked",
            Category = "Old",
            NewsCategoryId = category.Id,
            Title = "T",
            Excerpt = "E",
            ContentJson = "[]",
        });
        await _db.SaveChangesAsync();

        await _sut.UpdateAsync(category.Id, new UpsertNewsCategoryRequest { Name = "New", IsActive = true, SortOrder = 1 });

        Assert.Equal("New", _db.NewsArticles.Single().Category);
    }

    [Fact]
    public async Task Delete_InUseCategory_Throws()
    {
        var category = new NewsCategory { Name = "In Use", IsActive = true, SortOrder = 1 };
        _db.NewsCategories.Add(category);
        await _db.SaveChangesAsync();

        _db.NewsArticles.Add(new NewsArticle
        {
            Slug = "in-use",
            Category = "In Use",
            NewsCategoryId = category.Id,
            Title = "T",
            Excerpt = "E",
            ContentJson = "[]",
        });
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.DeleteAsync(category.Id));
    }

    [Fact]
    public async Task ResolveAsync_ReturnsExisting_WhenIdProvided()
    {
        var category = new NewsCategory { Name = "Event", IsActive = true, SortOrder = 1 };
        _db.NewsCategories.Add(category);
        await _db.SaveChangesAsync();

        var (id, name) = await _sut.ResolveAsync(category.Id, categoryName: "ignored");

        Assert.Equal(category.Id, id);
        Assert.Equal("Event", name);
    }

    [Fact]
    public async Task ResolveAsync_Throws_WhenIdNotFound()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ResolveAsync(categoryId: 999, categoryName: null));
    }

    [Fact]
    public async Task ResolveAsync_ReturnsEmpty_WhenBothInputsMissing()
    {
        var (id, name) = await _sut.ResolveAsync(categoryId: null, categoryName: "   ");

        Assert.Null(id);
        Assert.Equal(string.Empty, name);
    }

    [Fact]
    public async Task ResolveAsync_FindsExistingByName_CaseInsensitive()
    {
        var category = new NewsCategory { Name = "Event", IsActive = true, SortOrder = 1 };
        _db.NewsCategories.Add(category);
        await _db.SaveChangesAsync();

        var (id, name) = await _sut.ResolveAsync(categoryId: null, categoryName: " event ");

        Assert.Equal(category.Id, id);
        Assert.Equal("Event", name);
    }

    [Fact]
    public async Task ResolveAsync_AutoCreatesCategory_WhenNameNotFound_AndSetsNameVi()
    {
        var (id, name) = await _sut.ResolveAsync(categoryId: null, categoryName: " Brand New ");

        Assert.NotNull(id);
        Assert.Equal("Brand New", name);
        var stored = Assert.Single(_db.NewsCategories);
        Assert.Equal("Brand New", stored.Name);
        Assert.Equal("Brand New", stored.NameVi);
        Assert.True(stored.IsActive);
        Assert.Equal(1, stored.SortOrder);
    }
}
