using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using NihomeBackend.Controllers;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;
using Xunit;

namespace nihomebackend.tests.Controllers;

public class AboutSectionsControllerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly AboutSectionsController _sut;

    public AboutSectionsControllerTests()
    {
        _db = DbContextFactory.Create();
        var hostedImageService = new HostedImageService(
            Mock.Of<IWebHostEnvironment>(env => env.ContentRootPath == "/tmp"));
        var translationSvc = new EntityTranslationService(_db, new MemoryCache(new MemoryCacheOptions()));
        var service = new AboutSectionService(_db, hostedImageService, translationSvc);
        _sut = new AboutSectionsController(service);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetAll_ReturnsSeededSectionsOrderedBySortOrder()
    {
        _db.AboutSectionContents.AddRange(
            new AboutSectionContent { Slug = "timeline-main", Eyebrow = "Timeline", TitleA = "T1", TitleB = "T2", Paragraph1 = "", Paragraph2 = "", IsActive = true, SortOrder = 2 },
            new AboutSectionContent { Slug = "about-main", Eyebrow = "About", TitleA = "A1", TitleB = "A2", Paragraph1 = "", Paragraph2 = "", IsActive = true, SortOrder = 0 }
        );
        await _db.SaveChangesAsync();

        var result = await _sut.GetAll();

        var ok = Assert.IsType<OkObjectResult>(result);
        var items = Assert.IsType<List<AboutSectionResponse>>(ok.Value);
        Assert.Equal(2, items.Count);
        Assert.Equal("about-main", items[0].Slug);
        Assert.Equal("timeline-main", items[1].Slug);
    }

    [Fact]
    public async Task Create_ReturnsCreatedAtAction_WhenRequestIsValid()
    {
        var request = new UpsertAboutSectionRequest
        {
            Slug = "downloads-main",
            ItemsJson = """[{"name":"Company Profile","size":"12 MB","type":"PDF","url":"#"}]""",
            Eyebrow = "Downloads",
            TitleA = "Title A",
            TitleB = "Title B",
            Paragraph1 = "Desc",
            Paragraph2 = "",
            ImageUrl = "/images/upload/downloads.jpg",
            IsActive = true,
            SortOrder = 7,
        };

        var result = await _sut.Create(request);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(AboutSectionsController.GetBySlug), created.ActionName);
        var response = Assert.IsType<AboutSectionResponse>(created.Value);
        Assert.Equal("downloads-main", response.Slug);
        Assert.Equal(request.ItemsJson, response.ItemsJson);
    }

    [Fact]
    public async Task GetAll_ReturnsVietnamese_WhenLangIsVi()
    {
        _db.AboutSectionContents.Add(new AboutSectionContent
        {
            Slug = "about-main",
            Eyebrow = "Về chúng tôi",
            TitleA = "Tiêu đề A",
            TitleB = "Tiêu đề B",
            Paragraph1 = "Đoạn 1",
            Paragraph2 = "Đoạn 2",
            IsActive = true,
            SortOrder = 0
        });
        await _db.SaveChangesAsync();

        var result = await _sut.GetAll("vi");

        var ok = Assert.IsType<OkObjectResult>(result);
        var items = Assert.IsType<List<AboutSectionResponse>>(ok.Value);
        Assert.Single(items);
        Assert.Equal("Về chúng tôi", items[0].Eyebrow);
        Assert.Equal("Tiêu đề A", items[0].TitleA);
    }

    [Fact]
    public async Task GetAll_ReturnsTranslatedField_WhenLangIsEnAndTranslationExists()
    {
        var section = new AboutSectionContent
        {
            Slug = "about-main",
            Eyebrow = "Về chúng tôi",
            TitleA = "Tiêu đề A",
            TitleB = "Tiêu đề B",
            Paragraph1 = "Đoạn 1",
            Paragraph2 = "Đoạn 2",
            IsActive = true,
            SortOrder = 0
        };
        _db.AboutSectionContents.Add(section);
        await _db.SaveChangesAsync();

        _db.EntityTranslations.AddRange(
            new EntityTranslation { EntityType = "About", EntityId = section.Id, FieldName = "Eyebrow", LanguageCode = "en", Value = "About us" },
            new EntityTranslation { EntityType = "About", EntityId = section.Id, FieldName = "TitleA", LanguageCode = "en", Value = "Title A" }
        );
        await _db.SaveChangesAsync();

        var result = await _sut.GetAll("en");

        var ok = Assert.IsType<OkObjectResult>(result);
        var items = Assert.IsType<List<AboutSectionResponse>>(ok.Value);
        Assert.Single(items);
        Assert.Equal("About us", items[0].Eyebrow);
        Assert.Equal("Title A", items[0].TitleA);
        // Fields without translation fall back to Vietnamese
        Assert.Equal("Tiêu đề B", items[0].TitleB);
        Assert.Equal("Đoạn 1", items[0].Paragraph1);
    }

    [Fact]
    public async Task GetAll_FallsBackToVietnamese_WhenNoTranslationExists()
    {
        _db.AboutSectionContents.Add(new AboutSectionContent
        {
            Slug = "values-main",
            Eyebrow = "Giá trị",
            TitleA = "Tiêu đề",
            TitleB = "",
            Paragraph1 = "Mô tả",
            Paragraph2 = "",
            IsActive = true,
            SortOrder = 1
        });
        await _db.SaveChangesAsync();

        // Request English but no translations exist → all fields return Vietnamese
        var result = await _sut.GetAll("en");

        var ok = Assert.IsType<OkObjectResult>(result);
        var items = Assert.IsType<List<AboutSectionResponse>>(ok.Value);
        Assert.Single(items);
        Assert.Equal("Giá trị", items[0].Eyebrow);
        Assert.Equal("Tiêu đề", items[0].TitleA);
        Assert.Equal("Mô tả", items[0].Paragraph1);
    }

    [Fact]
    public async Task GetAll_ItemsJsonFallsBackToOriginal_WhenTranslationIsInvalidJson()
    {
        var validJson = """[{"label":"Năm kinh nghiệm","value":"18+"}]""";
        var section = new AboutSectionContent
        {
            Slug = "stats-main",
            Eyebrow = "",
            TitleA = "",
            TitleB = "",
            Paragraph1 = "",
            Paragraph2 = "",
            ItemsJson = validJson,
            IsActive = true,
            SortOrder = 0
        };
        _db.AboutSectionContents.Add(section);
        await _db.SaveChangesAsync();

        _db.EntityTranslations.Add(new EntityTranslation
        {
            EntityType = "About",
            EntityId = section.Id,
            FieldName = "ItemsJson",
            LanguageCode = "en",
            Value = "NOT_VALID_JSON{{{"
        });
        await _db.SaveChangesAsync();

        var result = await _sut.GetAll("en");

        var ok = Assert.IsType<OkObjectResult>(result);
        var items = Assert.IsType<List<AboutSectionResponse>>(ok.Value);
        // Invalid JSON translation → fallback to original Vietnamese ItemsJson
        Assert.Equal(validJson, items[0].ItemsJson);
    }
}
