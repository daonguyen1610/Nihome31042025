using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace nihomebackend.tests.Services;

public class ProjectCategoryServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ProjectCategoryService _sut;

    public ProjectCategoryServiceTests()
    {
        _db = DbContextFactory.Create();
        _sut = new ProjectCategoryService(_db, NullLogger<ProjectCategoryService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private static Project NewProject(string slug, string? category, int? categoryId = null) => new()
    {
        Slug = slug,
        ImageUrl = $"/images/{slug}.jpg",
        Name = slug,
        Client = "Client",
        Location = "Location",
        Scope = "Scope",
        Status = "ongoing",
        Category = category,
        ProjectCategoryId = categoryId,
    };

    [Fact]
    public async Task GetAllAsync_SeedsCategoriesFromProjects_WhenCategoryTableIsEmpty()
    {
        _db.Projects.AddRange(
            NewProject("p-1", " Factory "),
            NewProject("p-2", "factory"),
            NewProject("p-3", "Hotel"));
        await _db.SaveChangesAsync();

        var result = await _sut.GetAllAsync(includeInactive: true);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, c => c.Name == "Factory");
        Assert.Contains(result, c => c.Name == "Hotel");
        Assert.All(result, c => Assert.Equal(c.Name, c.NameVi));
    }

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyActiveCategories_ByDefault()
    {
        _db.ProjectCategories.AddRange(
            new ProjectCategory { Name = "Active", IsActive = true, SortOrder = 1 },
            new ProjectCategory { Name = "Inactive", IsActive = false, SortOrder = 2 });
        await _db.SaveChangesAsync();

        var result = await _sut.GetAllAsync();

        Assert.Single(result);
        Assert.Equal("Active", result[0].Name);
    }

    [Fact]
    public async Task GetAllAsync_FallsBackNameVi_ForLegacyRows()
    {
        _db.ProjectCategories.Add(new ProjectCategory { Name = "Legacy", NameVi = "", IsActive = true, SortOrder = 1 });
        await _db.SaveChangesAsync();

        var result = await _sut.GetAllAsync();

        Assert.Equal("Legacy", result[0].NameVi);
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenCategoryNameAlreadyExistsIgnoringCase()
    {
        _db.ProjectCategories.Add(new ProjectCategory { Name = "Factory", IsActive = true, SortOrder = 1 });
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.CreateAsync(new UpsertProjectCategoryRequest
        {
            Name = " factory ",
            IsActive = true,
            SortOrder = 2,
        }));
    }

    [Fact]
    public async Task UpdateAsync_RenamesLinkedProjects_WhenCategoryNameChanges()
    {
        var category = new ProjectCategory { Name = "Old Name", IsActive = true, SortOrder = 1 };
        _db.ProjectCategories.Add(category);
        await _db.SaveChangesAsync();
        _db.Projects.AddRange(
            NewProject("linked-1", "Old Name", category.Id),
            NewProject("linked-2", "old name", category.Id));
        await _db.SaveChangesAsync();

        var result = await _sut.UpdateAsync(category.Id, new UpsertProjectCategoryRequest
        {
            Name = "New Name",
            IsActive = true,
            SortOrder = 3,
        });

        Assert.NotNull(result);
        Assert.All(_db.Projects.ToList(), p => Assert.Equal("New Name", p.Category));
    }

    [Fact]
    public async Task DeleteAsync_Throws_WhenCategoryIsInUse()
    {
        var category = new ProjectCategory { Name = "In Use", IsActive = true, SortOrder = 1 };
        _db.ProjectCategories.Add(category);
        await _db.SaveChangesAsync();
        _db.Projects.Add(NewProject("linked", "in use", category.Id));
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.DeleteAsync(category.Id));

        Assert.Equal("Danh mục đang được sử dụng trong dự án, không thể xóa.", ex.Message);
    }

    [Fact]
    public async Task DeleteAsync_RemovesCategory_WhenNotInUse()
    {
        var category = new ProjectCategory { Name = "Unused", IsActive = true, SortOrder = 1 };
        _db.ProjectCategories.Add(category);
        await _db.SaveChangesAsync();

        var deleted = await _sut.DeleteAsync(category.Id);

        Assert.True(deleted);
        Assert.Empty(_db.ProjectCategories);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsExisting_WhenIdProvided()
    {
        var category = new ProjectCategory { Name = "Factory", IsActive = true, SortOrder = 1 };
        _db.ProjectCategories.Add(category);
        await _db.SaveChangesAsync();

        var (id, name) = await _sut.ResolveAsync(category.Id, categoryName: "ignored");

        Assert.Equal(category.Id, id);
        Assert.Equal("Factory", name);
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
        var category = new ProjectCategory { Name = "Factory", IsActive = true, SortOrder = 1 };
        _db.ProjectCategories.Add(category);
        await _db.SaveChangesAsync();

        var (id, name) = await _sut.ResolveAsync(categoryId: null, categoryName: " factory ");

        Assert.Equal(category.Id, id);
        Assert.Equal("Factory", name);
    }

    [Fact]
    public async Task ResolveAsync_AutoCreatesCategory_WhenNameNotFound()
    {
        var (id, name) = await _sut.ResolveAsync(categoryId: null, categoryName: " Brand New ");

        Assert.NotNull(id);
        Assert.Equal("Brand New", name);
        var stored = Assert.Single(_db.ProjectCategories);
        Assert.Equal("Brand New", stored.Name);
        Assert.Equal("Brand New", stored.NameVi);
        Assert.True(stored.IsActive);
        Assert.Equal(1, stored.SortOrder);
    }
}
