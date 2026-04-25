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

public class NewsControllerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly NewsController _sut;

    public NewsControllerTests()
    {
        _db = DbContextFactory.Create();

        var entityTranslationSvc = new EntityTranslationService(_db, Mock.Of<IMemoryCache>());
        var hostedImageService = new HostedImageService(
            Mock.Of<IWebHostEnvironment>(env => env.ContentRootPath == "/tmp"));
        var service = new NewsService(_db, entityTranslationSvc, hostedImageService);
        _sut = new NewsController(service);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetAll_ReturnsEmptyList_WhenNoNews()
    {
        var result = await _sut.GetAll();
        var ok = Assert.IsType<OkObjectResult>(result);
        var news = Assert.IsType<List<NewsResponse>>(ok.Value);
        Assert.Empty(news);
    }

    [Fact]
    public async Task GetAll_ReturnsNews_WhenDataExists()
    {
        var newsItem = new NewsArticle
        {
            Slug = "company-announcement",
            Date = "2025-02-01",
            ImageUrl = "/images/news.jpg",
            Title = "Company Announcement",
            Excerpt = "Important announcement",
            ContentJson = "[\"Details\"]",
            Category = "News",
            SortOrder = 1,
            CreatedAt = DateTime.UtcNow
        };
        _db.NewsArticles.Add(newsItem);
        await _db.SaveChangesAsync();

        var result = await _sut.GetAll();
        var ok = Assert.IsType<OkObjectResult>(result);
        var articles = Assert.IsType<List<NewsResponse>>(ok.Value);
        Assert.Single(articles);
        Assert.Equal("company-announcement", articles[0].Slug);
    }

    [Fact]
    public async Task GetBySlug_ReturnsNews_WhenSlugExists()
    {
        var newsItem = new NewsArticle
        {
            Slug = "press-release",
            Date = "2025-02-01",
            ImageUrl = "/images/press.jpg",
            Title = "Press Release",
            Excerpt = "New release",
            ContentJson = "[\"Content\"]",
            Category = "News",
            CreatedAt = DateTime.UtcNow
        };
        _db.NewsArticles.Add(newsItem);
        await _db.SaveChangesAsync();

        var result = await _sut.GetBySlug("press-release");
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<NewsResponse>(ok.Value);
        Assert.Equal("press-release", response.Slug);
        Assert.Equal("Press Release", response.Title);
    }

    [Fact]
    public async Task GetBySlug_ReturnsNotFound_WhenSlugDoesNotExist()
    {
        var result = await _sut.GetBySlug("nonexistent");
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Create_ReturnsCreatedAtAction_WithValidRequest()
    {
        var request = new UpsertNewsRequest
        {
            Slug = "new-article",
            Date = "2025-02-15",
            ImageUrl = "/images/article.jpg",
            Title = "New Article",
            Excerpt = "New article content",
            Content = new[] { "Full content" },
            Category = "News",
            SortOrder = 1
        };

        var result = await _sut.Create(request);
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(NewsController.GetBySlug), createdResult.ActionName);
        var response = Assert.IsType<NewsResponse>(createdResult.Value);
        Assert.Equal("new-article", response.Slug);
    }

    [Fact]
    public async Task Create_SavesNewsToDatabase()
    {
        var request = new UpsertNewsRequest
        {
            Slug = "db-news",
            Date = "2025-02-15",
            ImageUrl = "/images/db-news.jpg",
            Title = "DB News",
            Excerpt = "Test",
            Content = new[] { "Content" },
            Category = "News"
        };

        await _sut.Create(request);
        var saved = _db.NewsArticles.FirstOrDefault(n => n.Slug == "db-news");
        Assert.NotNull(saved);
        Assert.Equal("DB News", saved.Title);
    }

    [Fact]
    public async Task Update_ReturnsOk_WhenNewsExists()
    {
        var newsItem = new NewsArticle
        {
            Slug = "old-news",
            Date = "2025-01-15",
            ImageUrl = "/images/old.jpg",
            Title = "Old Title",
            Excerpt = "Old",
            ContentJson = "[\"Old\"]",
            Category = "News",
            CreatedAt = DateTime.UtcNow
        };
        _db.NewsArticles.Add(newsItem);
        await _db.SaveChangesAsync();

        var updateRequest = new UpsertNewsRequest
        {
            Slug = "updated-news",
            Date = "2025-02-15",
            ImageUrl = "/images/new.jpg",
            Title = "Updated Title",
            Excerpt = "Updated",
            Content = new[] { "Updated" },
            Category = "News"
        };

        var result = await _sut.Update(newsItem.Id, updateRequest);
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<NewsResponse>(ok.Value);
        Assert.Equal("Updated Title", response.Title);
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenNewsDoesNotExist()
    {
        var updateRequest = new UpsertNewsRequest
        {
            Slug = "test",
            Date = "2025-02-15",
            ImageUrl = "/images/test.jpg",
            Title = "Test",
            Excerpt = "Test",
            Content = new[] { "Test" },
            Category = "News"
        };

        var result = await _sut.Update(999, updateRequest);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Delete_ReturnsNoContent_WhenNewsExists()
    {
        var newsItem = new NewsArticle
        {
            Slug = "delete-news",
            Date = "2025-01-15",
            ImageUrl = "/images/delete.jpg",
            Title = "Delete Test",
            Excerpt = "Test",
            ContentJson = "[\"Test\"]",
            Category = "News",
            CreatedAt = DateTime.UtcNow
        };
        _db.NewsArticles.Add(newsItem);
        await _db.SaveChangesAsync();
        int id = newsItem.Id;

        var result = await _sut.Delete(id);
        Assert.IsType<NoContentResult>(result);
        var deleted = _db.NewsArticles.FirstOrDefault(n => n.Id == id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenNewsDoesNotExist()
    {
        var result = await _sut.Delete(999);
        Assert.IsType<NotFoundResult>(result);
    }
}
