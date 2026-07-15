using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace nihomebackend.tests.Services;

public class ActivityCategoryServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ActivityCategoryService _sut;

    public ActivityCategoryServiceTests()
    {
        _db = DbContextFactory.Create();
        _sut = new ActivityCategoryService(_db, NullLogger<ActivityCategoryService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetAllAsync_SeedsCategoriesFromActivities_WhenCategoryTableIsEmpty()
    {
        _db.Activities.AddRange(
            new Activity
            {
                Slug = "a-1",
                Date = "25.04.2026",
                ImageUrl = "/images/a-1.jpg",
                Category = " Event ",
                Title = "A1",
                Excerpt = "A1",
                ContentJson = "[]",
            },
            new Activity
            {
                Slug = "a-2",
                Date = "25.04.2026",
                ImageUrl = "/images/a-2.jpg",
                Category = "event",
                Title = "A2",
                Excerpt = "A2",
                ContentJson = "[]",
            },
            new Activity
            {
                Slug = "a-3",
                Date = "25.04.2026",
                ImageUrl = "/images/a-3.jpg",
                Category = "News",
                Title = "A3",
                Excerpt = "A3",
                ContentJson = "[]",
            });
        await _db.SaveChangesAsync();

        var result = await _sut.GetAllAsync(includeInactive: true);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, item => item.Name == "Event");
        Assert.Contains(result, item => item.Name == "News");
        Assert.All(result, item => Assert.Equal(item.Name, item.NameVi));
    }

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyActiveCategories_ByDefault()
    {
        _db.ActivityCategories.AddRange(
            new ActivityCategory { Name = "Active", IsActive = true, SortOrder = 1 },
            new ActivityCategory { Name = "Inactive", IsActive = false, SortOrder = 2 });
        await _db.SaveChangesAsync();

        var result = await _sut.GetAllAsync();

        Assert.Single(result);
        Assert.Equal("Active", result[0].Name);
    }

    [Fact]
    public async Task GetAllAsync_FallsBackNameVi_ForLegacyRows()
    {
        _db.ActivityCategories.Add(new ActivityCategory { Name = "Legacy", NameVi = "", IsActive = true, SortOrder = 1 });
        await _db.SaveChangesAsync();

        var result = await _sut.GetAllAsync();

        Assert.Equal("Legacy", result[0].NameVi);
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenCategoryNameAlreadyExistsIgnoringCase()
    {
        _db.ActivityCategories.Add(new ActivityCategory { Name = "Events", IsActive = true, SortOrder = 1 });
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.CreateAsync(new UpsertActivityCategoryRequest
        {
            Name = " events ",
            IsActive = true,
            SortOrder = 2,
        }));
    }

    [Fact]
    public async Task UpdateAsync_RenamesLinkedActivities_WhenCategoryNameChanges()
    {
        var category = new ActivityCategory { Name = "Old Name", IsActive = true, SortOrder = 1 };
        _db.ActivityCategories.Add(category);
        await _db.SaveChangesAsync();
        _db.Activities.AddRange(
            new Activity
            {
                Slug = "linked-1",
                Date = "25.04.2026",
                ImageUrl = "/images/linked-1.jpg",
                Category = "old name",
                ActivityCategoryId = category.Id,
                Title = "Linked 1",
                Excerpt = "Linked 1",
                ContentJson = "[]",
            },
            new Activity
            {
                Slug = "linked-2",
                Date = "25.04.2026",
                ImageUrl = "/images/linked-2.jpg",
                Category = "Old Name",
                ActivityCategoryId = category.Id,
                Title = "Linked 2",
                Excerpt = "Linked 2",
                ContentJson = "[]",
            });
        await _db.SaveChangesAsync();

        var result = await _sut.UpdateAsync(category.Id, new UpsertActivityCategoryRequest
        {
            Name = "New Name",
            IsActive = true,
            SortOrder = 3,
        });

        Assert.NotNull(result);
        Assert.All(_db.Activities.ToList(), activity => Assert.Equal("New Name", activity.Category));
    }

    [Fact]
    public async Task DeleteAsync_Throws_WhenCategoryIsInUse()
    {
        var category = new ActivityCategory { Name = "In Use", IsActive = true, SortOrder = 1 };
        _db.ActivityCategories.Add(category);
        await _db.SaveChangesAsync();
        _db.Activities.Add(new Activity
        {
            Slug = "linked",
            Date = "25.04.2026",
            ImageUrl = "/images/linked.jpg",
            Category = "in use",
            ActivityCategoryId = category.Id,
            Title = "Linked",
            Excerpt = "Linked",
            ContentJson = "[]",
        });
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.DeleteAsync(category.Id));

        Assert.Equal("Danh mục đang được sử dụng trong bài đăng, không thể xóa.", ex.Message);
    }

    [Fact]
    public async Task DeleteAsync_RemovesCategory_WhenNotInUse()
    {
        var category = new ActivityCategory { Name = "Unused", IsActive = true, SortOrder = 1 };
        _db.ActivityCategories.Add(category);
        await _db.SaveChangesAsync();

        var deleted = await _sut.DeleteAsync(category.Id);

        Assert.True(deleted);
        Assert.Empty(_db.ActivityCategories);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsExisting_WhenIdProvided()
    {
        var category = new ActivityCategory { Name = "Event", IsActive = true, SortOrder = 1 };
        _db.ActivityCategories.Add(category);
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
        var category = new ActivityCategory { Name = "Event", IsActive = true, SortOrder = 1 };
        _db.ActivityCategories.Add(category);
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
        var stored = Assert.Single(_db.ActivityCategories);
        Assert.Equal("Brand New", stored.Name);
        Assert.Equal("Brand New", stored.NameVi);
        Assert.True(stored.IsActive);
        Assert.Equal(1, stored.SortOrder);
    }
}
