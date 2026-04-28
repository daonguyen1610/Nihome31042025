using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
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
        var service = new AboutSectionService(_db, hostedImageService);
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
}
