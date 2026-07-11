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
}
