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

    [Fact]
    public void Seed_PopulatesNameViOnAllSeededCategories()
    {
        ContentSeeder.Seed(_db);

        Assert.NotEmpty(_db.ActivityCategories);
        Assert.All(_db.ActivityCategories, c => Assert.False(string.IsNullOrWhiteSpace(c.NameVi)));
        Assert.NotEmpty(_db.ProjectCategories);
        Assert.All(_db.ProjectCategories, c => Assert.False(string.IsNullOrWhiteSpace(c.NameVi)));
    }

    [Fact]
    public void Seed_BackfillsNewsCategoryIdFromLegacyCategoryString()
    {
        // Simulates real dev-DB data found during review: a News row with a
        // legacy Category string but no NewsCategoryId FK (the manifest's own
        // seed data has an empty Category for every item, so this has to be
        // set up explicitly rather than relying on ContentSeeder.Seed alone).
        _db.NewsArticles.Add(new NewsArticle
        {
            Slug = "legacy-category-article",
            Title = "Legacy Category Article",
            Excerpt = "Excerpt",
            ContentJson = "[]",
            ImageUrl = "/images/news/legacy/thumb.png",
            Category = "Company News",
            Date = "01/01/2026",
            SortOrder = 998,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        _db.SaveChanges();

        ContentSeeder.Seed(_db);

        var article = _db.NewsArticles.Single(n => n.Slug == "legacy-category-article");
        Assert.NotNull(article.NewsCategoryId);
        var category = _db.NewsCategories.Single(c => c.Id == article.NewsCategoryId);
        Assert.Equal("Company News", category.NameVi);
    }

    [Fact]
    public void Seed_PopulatesCanonicalTranslationsOnAllThreeCategoryTypes()
    {
        ContentSeeder.Seed(_db);

        var groundbreaking = _db.ActivityCategories.Single(c => c.Name == "Khởi công");
        Assert.Equal("Groundbreaking", groundbreaking.NameEn);
        Assert.Equal("奠基仪式", groundbreaking.NameZh);
        Assert.Equal("起工式", groundbreaking.NameJa);

        var industrialPlant = _db.ProjectCategories.Single(c => c.Name == "Nhà máy công nghiệp");
        Assert.Equal("Industrial Plant", industrialPlant.NameEn);
        Assert.Equal("工业厂房", industrialPlant.NameZh);
        Assert.Equal("工業プラント", industrialPlant.NameJa);

        var quotation = _db.NewsCategories.Single(c => c.Name == "Báo giá");
        Assert.Equal("Quotation", quotation.NameEn);
        Assert.Equal("报价", quotation.NameZh);
        Assert.Equal("見積もり", quotation.NameJa);
    }

    [Fact]
    public void Seed_BackfillsEmptyTranslations_OnExistingCategoryRow()
    {
        // Simulates the real dev-DB state found during review: a NewsCategory
        // row that already exists (auto-created from legacy News.Category
        // strings before this seed data existed) with no translations set.
        _db.NewsCategories.Add(new NewsCategory
        {
            Name = "Báo giá",
            NameVi = "Báo giá",
            IsActive = true,
            SortOrder = 1,
        });
        _db.SaveChanges();

        ContentSeeder.Seed(_db);

        var category = _db.NewsCategories.Single(c => c.Name == "Báo giá");
        Assert.Equal("Quotation", category.NameEn);
        Assert.Equal("报价", category.NameZh);
        Assert.Equal("見積もり", category.NameJa);
    }

    [Fact]
    public void Seed_DoesNotOverwriteAdminEditedCategoryTranslation()
    {
        _db.NewsCategories.Add(new NewsCategory
        {
            Name = "Báo giá",
            NameVi = "Báo giá",
            NameEn = "Admin-edited quotation label",
            IsActive = true,
            SortOrder = 1,
        });
        _db.SaveChanges();

        ContentSeeder.Seed(_db);

        var category = _db.NewsCategories.Single(c => c.Name == "Báo giá");
        Assert.Equal("Admin-edited quotation label", category.NameEn);
    }

    [Fact]
    public void Seed_LoadsProjectsFromManifest_NotHardcodedArray()
    {
        ContentSeeder.Seed(_db);

        var bmaFactory = _db.Projects.FirstOrDefault(p => p.Slug == "nha-may-bma-tai-kcn-huu-thanh");
        Assert.NotNull(bmaFactory);
        Assert.False(string.IsNullOrWhiteSpace(bmaFactory!.Client));
        Assert.False(string.IsNullOrWhiteSpace(bmaFactory.Location));

        // The old fake placeholder slug (different from the real scraped one
        // above) must no longer be seeded by fresh runs.
        Assert.Null(_db.Projects.FirstOrDefault(p => p.Slug == "nha-may-bma"));
    }

    [Fact]
    public void Seed_PopulatesContentJson_ForProjectsWithRealNarrative()
    {
        ContentSeeder.Seed(_db);

        var stfood = _db.Projects.FirstOrDefault(p => p.Slug == "stfood-marketing-factory-vn");
        Assert.NotNull(stfood);
        Assert.NotEqual("[]", stfood!.ContentJson);
    }

    [Fact]
    public void Seed_PopulatesEnContentTranslation_ViaExistingGenericLoader()
    {
        // SeedProjectTranslations() is unmodified by this plan — it already
        // stores any string-valued field name it finds under each language
        // object in project-translations.json. This confirms "Content"
        // flows through it the same way "Name" already does, with zero
        // changes to that method.
        ContentSeeder.Seed(_db);

        var stfood = _db.Projects.FirstOrDefault(p => p.Slug == "stfood-marketing-factory-vn");
        Assert.NotNull(stfood);

        var enContent = _db.EntityTranslations.FirstOrDefault(t =>
            t.EntityType == EntityTypes.Project && t.EntityId == stfood!.Id &&
            t.FieldName == "Content" && t.LanguageCode == "en");
        Assert.NotNull(enContent);
        Assert.False(string.IsNullOrWhiteSpace(enContent!.Value));
    }

    [Fact]
    public void Seed_IsBackfillOnly_ForProjects()
    {
        ContentSeeder.Seed(_db);
        var before = _db.Projects.Count();

        var project = _db.Projects.First();
        project.Description = "Admin-edited description";
        _db.SaveChanges();

        ContentSeeder.Seed(_db);
        var after = _db.Projects.Count();
        Assert.Equal(before, after);
        Assert.Equal("Admin-edited description", _db.Projects.First(p => p.Id == project.Id).Description);
    }

    [Fact]
    public void Seed_FallsBackToFirstGalleryImage_WhenTopLevelImageUrlIsBlank()
    {
        // The scraper leaves the top-level imageUrl blank for legacy pages
        // that had no distinct card thumbnail (nha-xuong-nbdc is one of ~44
        // of the 74 real projects in this state). Without a fallback, the
        // project card/detail hero would render a broken <img src="">.
        ContentSeeder.Seed(_db);

        var nbdc = _db.Projects.FirstOrDefault(p => p.Slug == "nha-xuong-nbdc");
        Assert.NotNull(nbdc);
        Assert.False(string.IsNullOrWhiteSpace(nbdc!.ImageUrl));
        Assert.StartsWith("/images/projects/nha-xuong-nbdc/", nbdc.ImageUrl);
    }

    [Fact]
    public void Seed_FallsBackToViExcerpt_WhenTopLevelDescriptionIsBlank()
    {
        // The top-level "description" field is blank on all 74 real projects;
        // the real summary text only exists under translations.vi.excerpt.
        ContentSeeder.Seed(_db);

        var nbdc = _db.Projects.FirstOrDefault(p => p.Slug == "nha-xuong-nbdc");
        Assert.NotNull(nbdc);
        Assert.False(string.IsNullOrWhiteSpace(nbdc!.Description));
    }

    [Fact]
    public void Seed_PopulatesYear_ExtractedFromViDate()
    {
        // The manifest has no top-level "year" field (only a Vietnamese
        // formatted date string under translations.vi.date, e.g.
        // "15 Tháng Tám 2024"); Year must be extracted from it instead of
        // staying null for every seeded project.
        ContentSeeder.Seed(_db);

        var nbdc = _db.Projects.FirstOrDefault(p => p.Slug == "nha-xuong-nbdc");
        Assert.NotNull(nbdc);
        Assert.Equal("2024", nbdc!.Year);
    }
}
