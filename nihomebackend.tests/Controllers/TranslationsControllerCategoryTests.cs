using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using NihomeBackend.Constants;
using NihomeBackend.Controllers;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;
using Xunit;

namespace nihomebackend.tests.Controllers;

public class TranslationsControllerCategoryTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly TranslationsController _sut;

    public TranslationsControllerCategoryTests()
    {
        _db = DbContextFactory.Create();
        var translationSvc = new TranslationService(_db, Mock.Of<IMemoryCache>());
        var entitySvc = new EntityTranslationService(_db, Mock.Of<IMemoryCache>());
        _sut = new TranslationsController(translationSvc, entitySvc, _db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void GetEntityTypes_IncludesAllThreeCategoryTypes()
    {
        var result = Assert.IsType<OkObjectResult>(_sut.GetEntityTypes());
        var json = JsonSerializer.Serialize(result.Value);

        Assert.Contains("\"ActivityCategory\"", json);
        Assert.Contains("\"NewsCategory\"", json);
        Assert.Contains("\"ProjectCategory\"", json);
    }

    [Fact]
    public async Task SaveThenGet_RoundTripsActivityCategoryEnglishName_WithoutTouchingOtherLanguages()
    {
        var category = new ActivityCategory
        {
            Name = "Su kien",
            NameVi = "Sự kiện",
            NameZh = "活动",
            IsActive = true,
            SortOrder = 1,
        };
        _db.ActivityCategories.Add(category);
        await _db.SaveChangesAsync();

        var saveResult = await _sut.SaveEntityTranslations(
            EntityTypes.ActivityCategory,
            category.Id,
            new SaveEntityTranslationsRequest { LanguageCode = "en", Translations = new() { ["Name"] = "Event" } });
        Assert.IsType<OkResult>(saveResult);

        var getResult = Assert.IsType<OkObjectResult>(
            await _sut.GetEntityTranslations(EntityTypes.ActivityCategory, category.Id));
        var json = JsonSerializer.Serialize(getResult.Value, new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });

        Assert.Contains("\"Name\":\"Sự kiện\"", json);
        Assert.Contains("\"Name\":\"Event\"", json);
        Assert.Contains("\"Name\":\"活动\"", json);

        var stored = await _db.ActivityCategories.FindAsync(category.Id);
        Assert.Equal("Event", stored!.NameEn);
        Assert.Equal("Sự kiện", stored.NameVi);
        Assert.Equal("活动", stored.NameZh);
    }

    [Fact]
    public async Task GetEntitiesWithTranslationStatus_CountsPopulatedLanguages_ForNewsCategory()
    {
        _db.NewsCategories.Add(new NewsCategory
        {
            Name = "Cong ty",
            NameVi = "Công ty",
            NameEn = "Company",
            IsActive = true,
            SortOrder = 1,
        });
        await _db.SaveChangesAsync();

        var result = Assert.IsType<OkObjectResult>(
            await _sut.GetEntitiesWithTranslationStatus(EntityTypes.NewsCategory));
        var json = JsonSerializer.Serialize(result.Value);

        Assert.Contains("\"translationCount\":1", json);
        Assert.Contains("\"expectedFields\":3", json);
    }

    [Fact]
    public async Task DeleteEntityTranslations_ClearsAllThreeLanguageColumns_ForProjectCategory()
    {
        var category = new ProjectCategory
        {
            Name = "Nha may",
            NameVi = "Nhà máy",
            NameEn = "Factory",
            NameZh = "工厂",
            NameJa = "工場",
            IsActive = true,
            SortOrder = 1,
        };
        _db.ProjectCategories.Add(category);
        await _db.SaveChangesAsync();

        var result = await _sut.DeleteEntityTranslations(EntityTypes.ProjectCategory, category.Id);

        Assert.IsType<NoContentResult>(result);
        var stored = await _db.ProjectCategories.FindAsync(category.Id);
        Assert.Equal("", stored!.NameEn);
        Assert.Equal("", stored.NameZh);
        Assert.Equal("", stored.NameJa);
        Assert.Equal("Nhà máy", stored.NameVi);
    }
}
