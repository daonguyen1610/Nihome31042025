using System.Text.Json;
using System.Text.Json.Nodes;
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
    public void Seed_LogosRestoreEachMissingDefaultFromPartialCategoryState()
    {
        _db.ClientLogos.Add(new ClientLogo
        {
            Name = "Custom client",
            ImageUrl = "/images/custom-client.png",
            Kind = LogoKind.Client,
            SortOrder = 500,
        });
        _db.SaveChanges();

        ContentSeeder.Seed(_db);

        Assert.Contains(_db.ClientLogos, logo => logo.Kind == LogoKind.Client && logo.Name == "BIDV");
        Assert.Contains(_db.ClientLogos, logo => logo.Kind == LogoKind.Client && logo.Name == "SBMT");
        Assert.Contains(_db.ClientLogos, logo => logo.Kind == LogoKind.Client && logo.Name == "DOMINSNANT");
        Assert.Contains(_db.ClientLogos, logo => logo.Kind == LogoKind.Client && logo.Name == "MEDICARE");
        Assert.Contains(_db.ClientLogos, logo => logo.Kind == LogoKind.Partner && logo.Name == "AGC");
        Assert.Contains(_db.ClientLogos, logo => logo.Kind == LogoKind.Supplier && logo.Name == "LHC");
        Assert.Contains(_db.ClientLogos, logo => logo.Kind == LogoKind.Supplier && logo.Name == "ACG");
    }

    [Fact]
    public void Seed_LogosPreserveMatchingDefaultWithCustomValuesAndStableId()
    {
        var existing = new ClientLogo
        {
            Name = "bidv",
            ImageUrl = "/images/admin-bidv.png",
            Kind = LogoKind.Client,
            SortOrder = 777,
        };
        _db.ClientLogos.Add(existing);
        _db.SaveChanges();
        var id = existing.Id;

        ContentSeeder.Seed(_db);
        ContentSeeder.Seed(_db);

        var matching = _db.ClientLogos.Where(logo =>
            logo.Kind == LogoKind.Client && logo.Name.ToLower() == "bidv").ToList();
        var preserved = Assert.Single(matching);
        Assert.Equal(id, preserved.Id);
        Assert.Equal("bidv", preserved.Name);
        Assert.Equal("/images/admin-bidv.png", preserved.ImageUrl);
        Assert.Equal(777, preserved.SortOrder);
    }

    [Fact]
    public void Seed_LogosDoNotRemoveOrRenameLegacyObsoleteAndCustomRows()
    {
        var existing = new[]
        {
            new ClientLogo { Name = "CLOTEX", ImageUrl = "/legacy/clotex.png", Kind = LogoKind.Client, SortOrder = 91 },
            new ClientLogo { Name = "AKATI WOOD", ImageUrl = "/legacy/akati.png", Kind = LogoKind.Client, SortOrder = 92 },
            new ClientLogo { Name = "Seamasterpaint", ImageUrl = "/legacy/seamaster.png", Kind = LogoKind.Supplier, SortOrder = 93 },
            new ClientLogo { Name = "Admin custom", ImageUrl = "/custom/logo.png", Kind = LogoKind.Partner, SortOrder = 94 },
        };
        _db.ClientLogos.AddRange(existing);
        _db.SaveChanges();
        var idsByName = existing.ToDictionary(logo => logo.Name, logo => logo.Id);

        ContentSeeder.Seed(_db);

        Assert.All(idsByName, pair => Assert.Equal(
            pair.Value,
            _db.ClientLogos.Single(logo => logo.Name == pair.Key).Id));
    }

    [Fact]
    public void Seed_LogosRerunKeepsStableCountsAndIds()
    {
        ContentSeeder.Seed(_db);
        var firstIds = _db.ClientLogos.ToDictionary(logo => logo.Kind + "|" + logo.Name, logo => logo.Id);

        ContentSeeder.Seed(_db);

        var secondIds = _db.ClientLogos.ToDictionary(logo => logo.Kind + "|" + logo.Name, logo => logo.Id);
        Assert.Equal(firstIds.Count, secondIds.Count);
        Assert.All(firstIds, pair => Assert.Equal(pair.Value, secondIds[pair.Key]));
    }

    [Fact]
    public void Seed_MigratesLegacyShopDrawingProcessLabels()
    {
        ContentSeeder.Seed(_db);
        var process = _db.ProcessDocuments.Single(item =>
            item.GroupKey == "tc" && item.Code == "3");
        process.Title = "3. QT-Duyệt và kiểm soát bản vẽ shopdrawings";
        var legacyFiles = JsonNode.Parse(process.FilesJson!)!.AsArray();
        foreach (var file in legacyFiles)
        {
            var displayName = file!["DisplayName"]!.GetValue<string>();
            if (displayName == "01. TC-SD-QT-Quy trình kiểm soát bản vẽ thiết kế chi tiết.doc")
            {
                file["DisplayName"] = "01. TC-SD-QT-Quy trinh kiem soat ban ve shop drawings .doc";
            }
            else if (displayName == "TC-SD-M01-Kế hoạch trình duyệt thiết kế chi tiết.doc")
            {
                file["DisplayName"] = "TC-SD-M01-Kế hoạch trình duyệt shop drawing.doc";
            }
        }
        process.FilesJson = legacyFiles.ToJsonString();
        _db.SaveChanges();

        ContentSeeder.Seed(_db);

        Assert.Equal(
            "3. QT-Duyệt và kiểm soát bản vẽ thiết kế chi tiết",
            process.Title);
        using var files = JsonDocument.Parse(process.FilesJson!);
        var displayNames = files.RootElement.EnumerateArray()
            .Select(item => item.GetProperty("DisplayName").GetString())
            .ToList();
        Assert.Contains(
            "01. TC-SD-QT-Quy trình kiểm soát bản vẽ thiết kế chi tiết.doc",
            displayNames);
        Assert.Contains(
            "TC-SD-M01-Kế hoạch trình duyệt thiết kế chi tiết.doc",
            displayNames);
    }

    [Fact]
    public void Seed_DoesNotOverwriteAdminEditedProcessLabels()
    {
        ContentSeeder.Seed(_db);
        var process = _db.ProcessDocuments.Single(item =>
            item.GroupKey == "tc" && item.Code == "3");
        process.Title = "Admin-edited process title";
        var editedFiles = JsonNode.Parse(process.FilesJson!)!.AsArray();
        editedFiles[0]!["DisplayName"] = "Admin-edited document label";
        process.FilesJson = editedFiles.ToJsonString();
        _db.SaveChanges();

        ContentSeeder.Seed(_db);

        Assert.Equal("Admin-edited process title", process.Title);
        Assert.Contains("Admin-edited document label", process.FilesJson);
    }

    [Fact]
    public void Seed_ProcessesPreserveCustomRowsAndStableIds_WhileRestoringMissingDefaults()
    {
        ContentSeeder.Seed(_db);
        var seeded = _db.ProcessDocuments.Single(item => item.GroupKey == "tc" && item.Code == "3");
        var seededId = seeded.Id;
        seeded.Title = "Admin-owned process title";
        var reordered = _db.ProcessDocuments.Single(item => item.GroupKey == "dt" && item.SortOrder == 0);
        var reorderedId = reordered.Id;
        reordered.SortOrder = 777;
        _db.ProcessDocuments.Add(new ProcessDocument
        {
            GroupKey = "custom",
            Code = "admin",
            Title = "Custom process",
            SortOrder = 900,
        });
        var missing = _db.ProcessDocuments.Single(item => item.GroupKey == "general" && item.SortOrder == 1);
        _db.ProcessDocuments.Remove(missing);
        _db.SaveChanges();

        ContentSeeder.Seed(_db);

        Assert.Equal("Admin-owned process title", _db.ProcessDocuments.Single(item => item.Id == seededId).Title);
        Assert.Equal(777, _db.ProcessDocuments.Single(item => item.Id == reorderedId).SortOrder);
        Assert.Contains(_db.ProcessDocuments, item => item.GroupKey == "custom" && item.Title == "Custom process");
        Assert.Contains(_db.ProcessDocuments, item => item.GroupKey == "general" && item.SortOrder == 1);
        Assert.Equal(30, _db.ProcessDocuments.Count());
    }

    [Fact]
    public void Seed_ProcessesUseImmutableSeedIdentityAfterTitleAndSortOrderEdits()
    {
        ContentSeeder.Seed(_db);
        var process = _db.ProcessDocuments.Single(item =>
            item.GroupKey == "general" && item.Title == "Quy trình đánh giá nội bộ");
        var id = process.Id;
        var seedKey = process.SeedKey;
        var canonicalCount = _db.ProcessDocuments.Count(item => item.SeedKey != null);
        Assert.False(string.IsNullOrWhiteSpace(process.SeedKey));
        Assert.DoesNotContain(process.Title.ToLowerInvariant(), process.SeedKey);
        process.Title = "Admin-renamed process";
        process.SortOrder = 777;
        _db.ProcessDocuments.Add(new ProcessDocument
        {
            GroupKey = "general",
            Title = "Custom process with colliding order",
            SortOrder = 1,
        });
        _db.SaveChanges();

        ContentSeeder.Seed(_db);

        Assert.Equal(canonicalCount, _db.ProcessDocuments.Count(item => item.SeedKey != null));
        var preserved = _db.ProcessDocuments.Single(item => item.Id == id);
        Assert.Equal(seedKey, preserved.SeedKey);
        Assert.Equal("Admin-renamed process", preserved.Title);
        Assert.Equal(777, preserved.SortOrder);
        Assert.Contains(_db.ProcessDocuments, item =>
            item.SeedKey == null && item.Title == "Custom process with colliding order");
    }

    [Fact]
    public void Seed_ProcessesMigrateTemporaryTitleIdentityWithoutIdOrCountChurn()
    {
        ContentSeeder.Seed(_db);
        var process = _db.ProcessDocuments.Single(item =>
            item.GroupKey == "general" && item.Title == "Quy trình đánh giá nội bộ");
        var id = process.Id;
        var count = _db.ProcessDocuments.Count();
        process.SeedKey = "general|quy trình đánh giá nội bộ";
        _db.SaveChanges();

        ContentSeeder.Seed(_db);

        var migrated = _db.ProcessDocuments.Single(item => item.Id == id);
        Assert.Equal(count, _db.ProcessDocuments.Count());
        Assert.StartsWith("general|/process-assets/", migrated.SeedKey);
    }

    [Fact]
    public void Seed_SlideshowTranslationsResolveBySlugAndPreserveAdminContent()
    {
        _db.SlideshowItems.Add(new SlideshowItem
        {
            Slug = "admin-hero",
            Title = "Admin hero",
            ImageUrl = "/images/custom.jpg",
            LinkUrl = "/custom",
            LinkText = "Custom",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        _db.SaveChanges();

        ContentSeeder.Seed(_db);
        var factory = _db.SlideshowItems.Single(item => item.Slug == "hero-factory");
        factory.Title = "Admin-edited factory hero";
        _db.SaveChanges();

        ContentSeeder.Seed(_db);

        Assert.NotEqual(1, factory.Id);
        Assert.Equal("Admin-edited factory hero", _db.SlideshowItems.Single(item => item.Id == factory.Id).Title);
        Assert.Contains(_db.EntityTranslations, translation =>
            translation.EntityType == EntityTypes.Slideshow && translation.EntityId == factory.Id &&
            translation.FieldName == "Title" && translation.LanguageCode == "en" &&
            translation.Value == "General Contractor for Factory Design & Build");
        Assert.DoesNotContain(_db.EntityTranslations, translation =>
            translation.EntityType == EntityTypes.Slideshow && translation.EntityId == 1);
    }

    [Fact]
    public void Seed_RecruitmentAndContactDefaultsRecoverFromPartialStateWithoutOverwritingCustomRows()
    {
        _db.EmploymentTypes.Add(new EmploymentType
        {
            Code = "full-time",
            Name = "Admin full time",
            IsActive = false,
            SortOrder = 99,
        });
        _db.JobPositions.Add(new JobPosition
        {
            Title = "Kỹ sư Xây dựng (Site Engineer)",
            Department = "Admin department",
            Location = "Đà Nẵng",
            EmploymentType = "full-time",
            RequirementsJson = "[]",
            SortOrder = 50,
        });
        _db.ContactMessages.Add(new ContactMessage
        {
            Name = "Custom sender",
            Email = "custom@example.com",
            Subject = "Custom subject",
            Message = "Custom message",
        });
        _db.SaveChanges();

        ContentSeeder.Seed(_db);
        var position = _db.JobPositions.Single(item => item.Title == "Kỹ sư Xây dựng (Site Engineer)");
        var positionId = position.Id;
        var counts = (_db.EmploymentTypes.Count(), _db.JobPositions.Count(), _db.JobApplications.Count(), _db.ContactMessages.Count());

        ContentSeeder.Seed(_db);

        Assert.Equal("Admin full time", _db.EmploymentTypes.Single(item => item.Code == "full-time").Name);
        Assert.Equal("Admin department", _db.JobPositions.Single(item => item.Id == positionId).Department);
        Assert.Contains(_db.EmploymentTypes, item => item.Code == "part-time");
        Assert.Contains(_db.JobPositions, item => item.Title == "Kiến trúc sư Thiết kế");
        Assert.Contains(_db.ContactMessages, item => item.Email == "custom@example.com" && item.Message == "Custom message");
        Assert.Equal(counts, (_db.EmploymentTypes.Count(), _db.JobPositions.Count(), _db.JobApplications.Count(), _db.ContactMessages.Count()));
    }

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
        Assert.Equal("Company News", category.NameEn);
        Assert.Equal("Company News", category.NameZh);
        Assert.Equal("Company News", category.NameJa);
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
    public void Seed_PreservesSourceFallbackAfterTranslationReset()
    {
        _db.ProjectCategories.Add(new ProjectCategory
        {
            Name = "Nhà máy công nghiệp",
            NameVi = "Nhà máy công nghiệp",
            NameEn = "Nhà máy công nghiệp",
            NameZh = "Nhà máy công nghiệp",
            NameJa = "Nhà máy công nghiệp",
            IsActive = true,
        });
        _db.SaveChanges();

        ContentSeeder.Seed(_db);

        var category = _db.ProjectCategories.Single(c => c.Name == "Nhà máy công nghiệp");
        Assert.Equal(category.NameVi, category.NameEn);
        Assert.Equal(category.NameVi, category.NameZh);
        Assert.Equal(category.NameVi, category.NameJa);
    }

    [Fact]
    public void Seed_BackfillsLanguagesOnNonCanonicalLegacyCategories()
    {
        _db.ActivityCategories.Add(new ActivityCategory { Name = "Legacy activity", NameVi = "Legacy activity" });
        _db.ProjectCategories.Add(new ProjectCategory { Name = "Legacy project", NameVi = "Legacy project" });
        _db.NewsCategories.Add(new NewsCategory { Name = "Legacy news", NameVi = "Legacy news" });
        _db.SaveChanges();

        ContentSeeder.Seed(_db);

        var activity = _db.ActivityCategories.Single(category => category.Name == "Legacy activity");
        Assert.Equal(activity.NameVi, activity.NameEn);
        Assert.Equal(activity.NameVi, activity.NameZh);
        Assert.Equal(activity.NameVi, activity.NameJa);
        var project = _db.ProjectCategories.Single(category => category.Name == "Legacy project");
        Assert.Equal(project.NameVi, project.NameEn);
        Assert.Equal(project.NameVi, project.NameZh);
        Assert.Equal(project.NameVi, project.NameJa);
        var news = _db.NewsCategories.Single(category => category.Name == "Legacy news");
        Assert.Equal(news.NameVi, news.NameEn);
        Assert.Equal(news.NameVi, news.NameZh);
        Assert.Equal(news.NameVi, news.NameJa);
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
