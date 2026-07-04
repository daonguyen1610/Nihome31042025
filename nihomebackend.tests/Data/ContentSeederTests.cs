using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Data;

public class ContentSeederTests : IDisposable
{
    private readonly AppDbContext _db = DbContextFactory.Create();

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Seed_DoesNotOverwriteAdminEditedNewsTranslation()
    {
        ContentSeeder.Seed(_db);
        var article = _db.NewsArticles.First();

        // Simulate an admin edit replacing whatever translation (if any) the
        // manifest seeded for this field/language.
        var priorTranslations = _db.EntityTranslations.Where(t =>
            t.EntityType == EntityTypes.News && t.EntityId == article.Id &&
            t.FieldName == "Title" && t.LanguageCode == "en").ToList();
        _db.EntityTranslations.RemoveRange(priorTranslations);

        var now = DateTime.UtcNow;
        _db.EntityTranslations.Add(new EntityTranslation
        {
            EntityType = EntityTypes.News,
            EntityId = article.Id,
            FieldName = "Title",
            LanguageCode = "en",
            Value = "Admin-edited title via CMS",
            CreatedAt = now,
            UpdatedAt = now,
        });
        _db.SaveChanges();

        // Re-running the seeder simulates a backend restart, which previously
        // wiped and re-created News + its translations from the static manifest.
        ContentSeeder.Seed(_db);

        var translation = _db.EntityTranslations.Single(t =>
            t.EntityType == EntityTypes.News && t.EntityId == article.Id &&
            t.FieldName == "Title" && t.LanguageCode == "en");
        Assert.Equal("Admin-edited title via CMS", translation.Value);
    }

    [Fact]
    public void Seed_DoesNotDeleteNewsArticleAddedOutsideManifest()
    {
        ContentSeeder.Seed(_db);
        var countBefore = _db.NewsArticles.Count();

        _db.NewsArticles.Add(new NewsArticle
        {
            Slug = "admin-added-article-not-in-manifest",
            Title = "Bài viết do admin thêm",
            Excerpt = "Excerpt",
            ContentJson = "[]",
            ImageUrl = "/images/news/admin-added/thumb.png",
            Category = "",
            Date = "01/01/2026",
            SortOrder = 999,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        _db.SaveChanges();

        ContentSeeder.Seed(_db);

        Assert.Equal(countBefore + 1, _db.NewsArticles.Count());
        Assert.Contains(_db.NewsArticles, n => n.Slug == "admin-added-article-not-in-manifest");
    }

    [Fact]
    public void Seed_IsIdempotent_RunningTwiceDoesNotDuplicateNews()
    {
        ContentSeeder.Seed(_db);
        var countAfterFirst = _db.NewsArticles.Count();

        ContentSeeder.Seed(_db);

        Assert.Equal(countAfterFirst, _db.NewsArticles.Count());
    }
}
