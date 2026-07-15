using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using NihomeBackend.Constants;
using NihomeBackend.Models;

namespace NihomeBackend.Data;

public static class ContentSeeder
{
    // Vietnamese text scraped from the CMS and hand-typed canonical category
    // names can use different Unicode normalization forms (NFC vs NFD) for the
    // same diacritics, which makes ordinal ToLower() comparisons treat visually
    // identical names as different keys and create duplicate category rows.
    // Normalize to NFC before lower-casing so both sources compare equal.
    private static string NormalizeCategoryKey(string name)
        => name.Trim().Normalize(NormalizationForm.FormC).ToLowerInvariant();

    public static void Seed(AppDbContext db)
    {
        SeedActivities(db);
        SeedNews(db);
        SeedProjects(db);
        SeedProjectTranslations(db);
        SeedServices(db);
        SeedLogos(db);
        SeedProcesses(db);
        SeedSlideshow(db);
        SeedAboutSections(db);
        SeedRecruitment(db);
        SeedContactMessages(db);
        SeedEntityTranslations(db);
        LinkCategories(db);
        SeedCategories(db);
    }

    private static void SeedCategories(AppDbContext db)
    {
        var now = DateTime.UtcNow;

        var projectCats = new[]
        {
            new { Name = "Nhà máy công nghiệp", NameEn = "Industrial Plant",       NameZh = "工业厂房",   NameJa = "工業プラント",  SortOrder = 0 },
            new { Name = "Nhà xưởng sản xuất",  NameEn = "Manufacturing Workshop", NameZh = "生产车间",   NameJa = "製造工場",    SortOrder = 1 },
            new { Name = "Tổ hợp công nghiệp",  NameEn = "Industrial Complex",     NameZh = "工业综合体",  NameJa = "工業団地",    SortOrder = 2 },
            new { Name = "Nhà kho logistics",    NameEn = "Logistics Warehouse",    NameZh = "物流仓库",   NameJa = "物流倉庫",    SortOrder = 3 },
            new { Name = "Văn phòng",            NameEn = "Office",                 NameZh = "办公楼",    NameJa = "オフィス",    SortOrder = 4 },
            new { Name = "Nội thất văn phòng",   NameEn = "Office Interior",        NameZh = "办公室内装",  NameJa = "オフィス内装",  SortOrder = 5 },
            new { Name = "Nội thất công nghiệp", NameEn = "Industrial Interior",    NameZh = "工业内装",   NameJa = "工業用内装",   SortOrder = 6 },
            new { Name = "Công trình công cộng", NameEn = "Public Works",           NameZh = "公共工程",   NameJa = "公共施設",    SortOrder = 7 },
            new { Name = "Khách sạn",            NameEn = "Hotel",                  NameZh = "酒店",     NameJa = "ホテル",     SortOrder = 8 },
            new { Name = "Nhà hàng",             NameEn = "Restaurant",             NameZh = "餐厅",     NameJa = "レストラン",   SortOrder = 9 },
            new { Name = "Thương mại",           NameEn = "Commercial",             NameZh = "商业",     NameJa = "商業施設",    SortOrder = 10 },
            new { Name = "Nhà ở",                NameEn = "Residential",            NameZh = "住宅",     NameJa = "住宅",      SortOrder = 11 },
            new { Name = "Bất động sản",         NameEn = "Real Estate",            NameZh = "房地产",    NameJa = "不動産",     SortOrder = 12 },
            new { Name = "Studio",               NameEn = "Studio",                 NameZh = "工作室",    NameJa = "スタジオ",    SortOrder = 13 },
            new { Name = "Nhà máy dược phẩm",   NameEn = "Pharmaceutical Plant",   NameZh = "制药厂",    NameJa = "製薬工場",    SortOrder = 14 },
            new { Name = "Giáo dục",             NameEn = "Education",              NameZh = "教育",     NameJa = "教育施設",    SortOrder = 15 },
        };

        var existingProjCats = db.ProjectCategories
            .ToDictionary(c => NormalizeCategoryKey(c.Name));
        foreach (var seed in projectCats)
        {
            var key = NormalizeCategoryKey(seed.Name);
            if (existingProjCats.TryGetValue(key, out var existing))
            {
                existing.SortOrder = seed.SortOrder;
                if (string.IsNullOrWhiteSpace(existing.NameEn)) existing.NameEn = seed.NameEn;
                if (string.IsNullOrWhiteSpace(existing.NameZh)) existing.NameZh = seed.NameZh;
                if (string.IsNullOrWhiteSpace(existing.NameJa)) existing.NameJa = seed.NameJa;
            }
            else
                db.ProjectCategories.Add(new ProjectCategory
                {
                    Name = seed.Name,
                    NameVi = seed.Name,
                    NameEn = seed.NameEn,
                    NameZh = seed.NameZh,
                    NameJa = seed.NameJa,
                    IsActive = true,
                    SortOrder = seed.SortOrder,
                });
        }

        var activityCats = new[]
        {
            new { Name = "Khởi công",   NameEn = "Groundbreaking", NameZh = "奠基仪式", NameJa = "起工式",    SortOrder = 1 },
            new { Name = "Khánh thành", NameEn = "Inauguration",   NameZh = "竣工典礼", NameJa = "竣工式",    SortOrder = 2 },
            new { Name = "Sự kiện",     NameEn = "Event",          NameZh = "活动",   NameJa = "イベント",   SortOrder = 3 },
            new { Name = "Dự án",       NameEn = "Project",        NameZh = "项目",   NameJa = "プロジェクト", SortOrder = 4 },
            new { Name = "Giải thưởng", NameEn = "Award",          NameZh = "奖项",   NameJa = "受賞",     SortOrder = 5 },
            new { Name = "Triển lãm",   NameEn = "Exhibition",     NameZh = "展览",   NameJa = "展示会",    SortOrder = 6 },
            new { Name = "Cộng đồng",   NameEn = "Community",      NameZh = "社区",   NameJa = "コミュニティ", SortOrder = 7 },
            new { Name = "Văn hóa",     NameEn = "Culture",        NameZh = "文化",   NameJa = "文化",     SortOrder = 8 },
            new { Name = "Đào tạo",     NameEn = "Training",       NameZh = "培训",   NameJa = "研修",     SortOrder = 9 },
            new { Name = "Dịch vụ",     NameEn = "Service",        NameZh = "服务",   NameJa = "サービス",   SortOrder = 10 },
        };

        var existingActCats = db.ActivityCategories
            .ToDictionary(c => NormalizeCategoryKey(c.Name));
        foreach (var seed in activityCats)
        {
            var key = NormalizeCategoryKey(seed.Name);
            if (existingActCats.TryGetValue(key, out var existing))
            {
                existing.SortOrder = seed.SortOrder;
                if (string.IsNullOrWhiteSpace(existing.NameEn)) existing.NameEn = seed.NameEn;
                if (string.IsNullOrWhiteSpace(existing.NameZh)) existing.NameZh = seed.NameZh;
                if (string.IsNullOrWhiteSpace(existing.NameJa)) existing.NameJa = seed.NameJa;
            }
            else
                db.ActivityCategories.Add(new ActivityCategory
                {
                    Name = seed.Name,
                    NameVi = seed.Name,
                    NameEn = seed.NameEn,
                    NameZh = seed.NameZh,
                    NameJa = seed.NameJa,
                    IsActive = true,
                    SortOrder = seed.SortOrder,
                });
        }

        var newsCats = new[]
        {
            new { Name = "Báo giá",    NameEn = "Quotation",   NameZh = "报价",   NameJa = "見積もり", SortOrder = 1 },
            new { Name = "Dịch vụ",    NameEn = "Service",     NameZh = "服务",   NameJa = "サービス", SortOrder = 2 },
            new { Name = "Dự án",      NameEn = "Project",     NameZh = "项目",   NameJa = "プロジェクト", SortOrder = 3 },
            new { Name = "Kiến trúc",  NameEn = "Architecture", NameZh = "建筑",  NameJa = "建築",    SortOrder = 4 },
            new { Name = "Kỹ thuật",   NameEn = "Engineering", NameZh = "工程技术", NameJa = "技術",    SortOrder = 5 },
            new { Name = "Ngành",      NameEn = "Industry",    NameZh = "行业",   NameJa = "業界",    SortOrder = 6 },
            new { Name = "Quy trình",  NameEn = "Process",     NameZh = "流程",   NameJa = "プロセス",  SortOrder = 7 },
            new { Name = "Thiết kế",   NameEn = "Design",      NameZh = "设计",   NameJa = "デザイン",  SortOrder = 8 },
            new { Name = "Tiêu chuẩn", NameEn = "Standards",   NameZh = "标准",   NameJa = "基準",    SortOrder = 9 },
            new { Name = "Xu hướng",   NameEn = "Trends",      NameZh = "趋势",   NameJa = "トレンド",  SortOrder = 10 },
            new { Name = "Đối tác",    NameEn = "Partners",    NameZh = "合作伙伴", NameJa = "パートナー", SortOrder = 11 },
        };

        var existingNewsCats = db.NewsCategories
            .ToDictionary(c => NormalizeCategoryKey(c.Name));
        foreach (var seed in newsCats)
        {
            var key = NormalizeCategoryKey(seed.Name);
            if (existingNewsCats.TryGetValue(key, out var existing))
            {
                existing.SortOrder = seed.SortOrder;
                if (string.IsNullOrWhiteSpace(existing.NameEn)) existing.NameEn = seed.NameEn;
                if (string.IsNullOrWhiteSpace(existing.NameZh)) existing.NameZh = seed.NameZh;
                if (string.IsNullOrWhiteSpace(existing.NameJa)) existing.NameJa = seed.NameJa;
            }
            else
                db.NewsCategories.Add(new NewsCategory
                {
                    Name = seed.Name,
                    NameVi = seed.Name,
                    NameEn = seed.NameEn,
                    NameZh = seed.NameZh,
                    NameJa = seed.NameJa,
                    IsActive = true,
                    SortOrder = seed.SortOrder,
                });
        }

        db.SaveChanges();
    }

    private static void LinkCategories(AppDbContext db)
    {
        // Ensure ActivityCategory rows exist for every distinct Activity.Category
        var activityCategoryNames = db.Activities
            .Select(a => a.Category)
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct()
            .ToList();
        var existingActivityNames = db.ActivityCategories
            .Select(c => c.Name)
            .ToList()
            .Select(NormalizeCategoryKey)
            .ToHashSet();
        var nextOrder = (db.ActivityCategories.Max(c => (int?)c.SortOrder) ?? 0) + 1;
        foreach (var name in activityCategoryNames)
        {
            var trimmed = name.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;
            if (existingActivityNames.Contains(NormalizeCategoryKey(trimmed))) continue;
            db.ActivityCategories.Add(new ActivityCategory { Name = trimmed, NameVi = trimmed, IsActive = true, SortOrder = nextOrder++ });
            existingActivityNames.Add(NormalizeCategoryKey(trimmed));
        }

        // Ensure ProjectCategory rows exist for every distinct Project.Category
        var projectCategoryNames = db.Projects
            .Select(p => p.Category)
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct()
            .ToList();
        var existingProjectNames = db.ProjectCategories
            .Select(c => c.Name)
            .ToList()
            .Select(NormalizeCategoryKey)
            .ToHashSet();
        var nextProjectOrder = (db.ProjectCategories.Max(c => (int?)c.SortOrder) ?? 0) + 1;
        foreach (var name in projectCategoryNames)
        {
            var trimmed = (name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;
            if (existingProjectNames.Contains(NormalizeCategoryKey(trimmed))) continue;
            db.ProjectCategories.Add(new ProjectCategory { Name = trimmed, NameVi = trimmed, IsActive = true, SortOrder = nextProjectOrder++ });
            existingProjectNames.Add(NormalizeCategoryKey(trimmed));
        }

        // Ensure NewsCategory rows exist for every distinct NewsArticle.Category
        var newsCategoryNames = db.NewsArticles
            .Select(n => n.Category)
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct()
            .ToList();
        var existingNewsNames = db.NewsCategories
            .Select(c => c.Name)
            .ToList()
            .Select(NormalizeCategoryKey)
            .ToHashSet();
        var nextNewsOrder = (db.NewsCategories.Max(c => (int?)c.SortOrder) ?? 0) + 1;
        foreach (var name in newsCategoryNames)
        {
            var trimmed = (name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;
            if (existingNewsNames.Contains(NormalizeCategoryKey(trimmed))) continue;
            db.NewsCategories.Add(new NewsCategory { Name = trimmed, NameVi = trimmed, IsActive = true, SortOrder = nextNewsOrder++ });
            existingNewsNames.Add(NormalizeCategoryKey(trimmed));
        }
        db.SaveChanges();

        // Backfill FK on Activity rows
        var activityCategoryMap = db.ActivityCategories.ToDictionary(c => NormalizeCategoryKey(c.Name), c => c.Id);
        foreach (var activity in db.Activities.Where(a => a.ActivityCategoryId == null && a.Category != ""))
        {
            if (activityCategoryMap.TryGetValue(NormalizeCategoryKey(activity.Category), out var id))
            {
                activity.ActivityCategoryId = id;
            }
        }

        // Backfill FK on Project rows
        var projectCategoryMap = db.ProjectCategories.ToDictionary(c => NormalizeCategoryKey(c.Name), c => c.Id);
        foreach (var project in db.Projects.Where(p => p.ProjectCategoryId == null && p.Category != null && p.Category != ""))
        {
            if (projectCategoryMap.TryGetValue(NormalizeCategoryKey(project.Category!), out var id))
            {
                project.ProjectCategoryId = id;
            }
        }

        // Backfill FK on News rows
        var newsCategoryMap = db.NewsCategories.ToDictionary(c => NormalizeCategoryKey(c.Name), c => c.Id);
        foreach (var article in db.NewsArticles.Where(n => n.NewsCategoryId == null && n.Category != ""))
        {
            if (newsCategoryMap.TryGetValue(NormalizeCategoryKey(article.Category), out var id))
            {
                article.NewsCategoryId = id;
            }
        }
        db.SaveChanges();
    }

    // ─── Activities (manifest-driven from legacy nicon.vn) ──────────

    private static void SeedActivities(AppDbContext db)
    {
        var manifest = LoadContentSeed("activities");
        if (manifest.Count == 0) return;

        var existingSlugs = db.Activities.Select(a => a.Slug).ToHashSet();
        var newItems = manifest.Where(item => !existingSlugs.Contains(item.Slug)).ToList();
        if (newItems.Count > 0)
        {
            foreach (var item in newItems)
            {
                var vi = item.GetTranslation("vi");
                db.Activities.Add(new Activity
                {
                    Slug = item.Slug,
                    Date = vi.Date.Length > 0 ? vi.Date : item.Date,
                    ImageUrl = item.ImageUrl,
                    GalleryJson = item.Gallery.Count > 0 ? JsonSerializer.Serialize(item.Gallery) : null,
                    Category = item.Category,
                    Author = string.Empty,
                    Title = vi.Title,
                    Excerpt = vi.Excerpt,
                    ContentJson = JsonSerializer.Serialize(vi.Content),
                    SortOrder = item.SortOrder,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                });
            }
            db.SaveChanges();
        }

        // Backfill only — never overwrites rows/translations already in the DB,
        // so admin-added content and translations survive future restarts.
        var bySlug = db.Activities.Select(a => new { a.Id, a.Slug }).ToList().ToDictionary(x => x.Slug, x => x.Id);
        SeedManifestTranslations(db, EntityTypes.Activity, manifest, bySlug);
    }

    // ─── News (manifest-driven from legacy nicon.vn) ───────────────

    private static void SeedNews(AppDbContext db)
    {
        var manifest = LoadContentSeed("news");
        if (manifest.Count == 0) return;

        var existingSlugs = db.NewsArticles.Select(n => n.Slug).ToHashSet();
        var newItems = manifest.Where(item => !existingSlugs.Contains(item.Slug)).ToList();
        if (newItems.Count > 0)
        {
            foreach (var item in newItems)
            {
                var vi = item.GetTranslation("vi");
                db.NewsArticles.Add(new NewsArticle
                {
                    Slug = item.Slug,
                    Date = vi.Date.Length > 0 ? vi.Date : item.Date,
                    ImageUrl = item.ImageUrl,
                    GalleryJson = item.Gallery.Count > 0 ? JsonSerializer.Serialize(item.Gallery) : null,
                    Category = item.Category,
                    Title = vi.Title,
                    Excerpt = vi.Excerpt,
                    ContentJson = JsonSerializer.Serialize(vi.Content),
                    SortOrder = item.SortOrder,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                });
            }
            db.SaveChanges();
        }

        // Backfill only — never overwrites rows/translations already in the DB,
        // so admin-added content and translations survive future restarts.
        var bySlug = db.NewsArticles.Select(n => new { n.Id, n.Slug }).ToList().ToDictionary(x => x.Slug, x => x.Id);
        SeedManifestTranslations(db, EntityTypes.News, manifest, bySlug);
    }

    // ─── Projects ───────────────────────────────────────────────────

    private static void SeedProjects(AppDbContext db)
    {
        var manifest = LoadProjectSeed();
        if (manifest.Count == 0) return;

        var existingSlugs = db.Projects.Select(p => p.Slug).ToHashSet();
        var newItems = manifest
            .Where(item => !existingSlugs.Contains(item.Slug))
            .Select(item =>
            {
                var vi = item.GetTranslation("vi");

                // The scraper leaves some top-level fields blank when the legacy
                // page had no distinct value for them; fall back to the vi
                // translation block (Description/Year) or the first gallery
                // image (ImageUrl) so cards/detail pages don't render blank.
                var imageUrl = string.IsNullOrWhiteSpace(item.ImageUrl)
                    ? (item.Gallery.FirstOrDefault() ?? "")
                    : item.ImageUrl;
                var description = string.IsNullOrWhiteSpace(item.Description)
                    ? vi.Excerpt
                    : item.Description;
                var yearMatch = Regex.Match(vi.Date, @"\d{4}");
                var year = yearMatch.Success ? yearMatch.Value : null;

                return new Project
                {
                    Slug = item.Slug,
                    ImageUrl = imageUrl,
                    GalleryJson = item.Gallery.Count > 0 ? JsonSerializer.Serialize(item.Gallery) : null,
                    Name = item.Name,
                    Client = item.Client,
                    Location = item.Location,
                    Scale = item.Scale,
                    Scope = item.Scope,
                    Status = item.Status ?? WarnMissingStatus(item.Slug),
                    Year = year,
                    Category = string.IsNullOrWhiteSpace(item.Category) ? null : item.Category,
                    Description = description,
                    ContentJson = item.Content is { Count: > 0 } ? JsonSerializer.Serialize(item.Content) : "[]",
                    ChallengesJson = item.Challenges is { Count: > 0 } ? JsonSerializer.Serialize(item.Challenges) : null,
                    SolutionsJson = item.Solutions is { Count: > 0 } ? JsonSerializer.Serialize(item.Solutions) : null,
                    SortOrder = item.SortOrder,
                };
            })
            .ToList();

        if (newItems.Count > 0)
        {
            db.Projects.AddRange(newItems);
            db.SaveChanges();
        }
    }

    // A missing "status" in the manifest previously defaulted to "ongoing" silently
    // (via a C# property initializer, indistinguishable from an explicit value) —
    // a completed project would seed as ongoing with no signal. Status is now
    // nullable so this path only runs, and warns, when the manifest truly omits it.
    private static string WarnMissingStatus(string slug)
    {
        Console.Error.WriteLine($"[ContentSeeder] Project '{slug}' has no \"status\" in the manifest; defaulting to \"ongoing\". Verify this is correct.");
        return "ongoing";
    }

    private static List<ProjectSeedItem> LoadProjectSeed()
    {
        const string resourceName = "nihomebackend.Data.Seeds.content.projects.json";
        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream(resourceName);
        if (stream is null) return [];
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<List<ProjectSeedItem>>(stream, opts) ?? [];
    }

    private sealed class ProjectSeedItem
    {
        public string Slug { get; set; } = "";
        public string ImageUrl { get; set; } = "";
        public List<string> Gallery { get; set; } = [];
        public string Name { get; set; } = "";
        public string Client { get; set; } = "";
        public string Location { get; set; } = "";
        public string Scale { get; set; } = "";
        public string Scope { get; set; } = "";
        public string? Status { get; set; }
        public string? Category { get; set; }
        public string? Description { get; set; }

        // Each entry is either a plain string (paragraph) or an object like
        // { "type": "image", "url": "/images/projects/<slug>/img-03.jpg" } —
        // keep the raw JsonElement so dict items survive a Serialize round-trip.
        public List<JsonElement>? Content { get; set; }
        public List<string>? Challenges { get; set; }
        public List<string>? Solutions { get; set; }
        public int SortOrder { get; set; }

        // Per-language overrides (vi/en/zh/ja), same shape as the
        // Activities/News manifests. Only "vi" is consumed here — it backs
        // the Description/Year fallbacks above (the top-level fields are
        // blank on ~all rows). en/zh/ja live in project-translations.json
        // and are consumed by SeedProjectTranslations() instead.
        public Dictionary<string, ContentSeedTranslation> Translations { get; set; } = new();

        public ContentSeedTranslation GetTranslation(string lang)
            => Translations.TryGetValue(lang, out var t) ? t : new ContentSeedTranslation();
    }

    // ─── Project Translations ────────────────────────────────────────

    private static void SeedProjectTranslations(AppDbContext db)
    {
        const string resourceName = "nihomebackend.Data.Seeds.content.project-translations.json";
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null) return;

        using var doc = JsonDocument.Parse(stream);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return;

        var slugToId = db.Projects
            .Select(p => new { p.Slug, p.Id })
            .ToDictionary(x => x.Slug, x => x.Id);

        var existing = db.EntityTranslations
            .Where(t => t.EntityType == EntityTypes.Project)
            .Select(t => new { t.EntityId, t.FieldName, t.LanguageCode, t.Id, t.Value })
            .ToList()
            .GroupBy(t => $"{t.EntityId}|{t.FieldName}|{t.LanguageCode}")
            .ToDictionary(g => g.Key, g => g.First());

        var toAdd = new List<EntityTranslation>();
        // Rows queued in this pass but not yet in `existing` (DB hasn't been hit
        // again) — tracked separately so a same-pass duplicate (slug/field/lang
        // repeated in project-translations.json) updates the pending row in place
        // instead of a second insert, which would violate the unique index on
        // (EntityType, EntityId, FieldName, LanguageCode).
        var pending = new Dictionary<string, EntityTranslation>();
        var now = DateTime.UtcNow;

        foreach (var entry in doc.RootElement.EnumerateArray())
        {
            if (!entry.TryGetProperty("slug", out var slugProp)) continue;
            var slug = slugProp.GetString();
            if (slug is null || !slugToId.TryGetValue(slug, out var entityId)) continue;

            foreach (var langProp in entry.EnumerateObject())
            {
                var lang = langProp.Name;
                if (lang == "slug") continue;
                if (langProp.Value.ValueKind != JsonValueKind.Object) continue;

                foreach (var fieldProp in langProp.Value.EnumerateObject())
                {
                    var field = fieldProp.Name;
                    var value = fieldProp.Value.GetString() ?? "";
                    if (string.IsNullOrWhiteSpace(value)) continue;

                    var key = $"{entityId}|{field}|{lang}";
                    if (pending.TryGetValue(key, out var pendingRow))
                    {
                        pendingRow.Value = value;
                        pendingRow.UpdatedAt = now;
                    }
                    else if (existing.TryGetValue(key, out var row))
                    {
                        if (row.Value != value)
                        {
                            var tracked = db.EntityTranslations.Find(row.Id);
                            if (tracked != null) { tracked.Value = value; tracked.UpdatedAt = now; }
                        }
                    }
                    else
                    {
                        var newRow = new EntityTranslation
                        {
                            EntityType = EntityTypes.Project,
                            EntityId = entityId,
                            FieldName = field,
                            LanguageCode = lang,
                            Value = value,
                            CreatedAt = now,
                            UpdatedAt = now,
                        };
                        toAdd.Add(newRow);
                        pending[key] = newRow;
                    }
                }
            }
        }

        if (toAdd.Count > 0) db.EntityTranslations.AddRange(toAdd);
        db.SaveChanges();
    }

    // ─── Services ───────────────────────────────────────────────────

    private static void SeedServices(AppDbContext db)
    {
        var now = DateTime.UtcNow;

        var seeds = new[]
        {
            new
            {
                Slug = "design-and-build", ShortTitle = "Design & Build", SortOrder = 0,
                Title   = "Tổng thầu Thiết kế và Thi công (D&B)",
                Tagline = "Một đầu mối — toàn bộ vòng đời dự án.",
                Intro   = "Phương thức Design & Build (D&B) và EPC là hai phương pháp phổ biến nhất trong xây dựng công nghiệp và dân dụng. NICON đã hệ thống hoá quy trình D&B từ ngày đầu thành lập và liên tục hoàn thiện qua hơn 150 dự án.",
                SectionsJson = JsonSerializer.Serialize(new[] {
                    new { heading = "Lợi thế của phương thức D&B / EPC", body = new[] { "Tối thiểu hóa nghĩa vụ quản lý cho chủ đầu tư — NICON đảm nhận toàn bộ điều phối và quản lý dự án.", "Giảm thiểu rủi ro không nhất quán giữa thiết kế và thi công.", "Linh hoạt đẩy nhanh tiến độ ngay cả khi thiết kế chưa hoàn chỉnh, giảm chi phí phát sinh.", "Chi phí quản lý hợp lý — chủ đầu tư dễ ước lượng và kiểm soát chất lượng do chỉ làm việc với một nhà thầu." } },
                    new { heading = "Phương pháp quản lý dự án tiên tiến", body = new[] { "Hợp tác chặt chẽ cùng Mori Construction (Nhật Bản), NICON ứng dụng quy trình BIM (Building Information Modeling) cho mọi giai đoạn.", "Đội ngũ chuyên viên BIM giàu kinh nghiệm cung cấp giải pháp đồng bộ, giúp chủ đầu tư có toàn bộ thông tin dự án và dự đoán rủi ro sớm." } },
                    new { heading = "Sản phẩm tốt nhất từ những con người tốt nhất", body = new[] { "NICON sở hữu mạng lưới đối tác quản lý quốc tế trong các lĩnh vực kiến trúc, kết cấu, nội thất và M&E.", "Đội ngũ giàu kinh nghiệm gồm project manager, kiến trúc sư, kỹ sư và công nhân lành nghề có thể xử lý các dự án QUY MÔ LỚN – TIẾN ĐỘ GẤP – CHẤT LƯỢNG CAO." } },
                }),
                HighlightsJson = JsonSerializer.Serialize(new[] { "BIM 4D / 5D", "ISO 9001:2015", "150+ Dự Án D&B", "Mori Group partner" }),
                IntroBlocksJson = JsonSerializer.Serialize(new[] {
                    new { text = "Hiện nay, ngành xây dựng chủ yếu áp dụng hai phương thức: Thiết kế-Xây dựng và Kỹ thuật – Mua sắm – Xây dựng (EPC).\n\nNhận thấy đây là giải pháp tối ưu cho thị trường Việt Nam, chúng tôi đã áp dụng đồng bộ hệ thống này ngay từ ngày đầu thành lập. Sự phát triển nhanh chóng của công ty là minh chứng cho sự lựa chọn đúng đắn này.\n\nKỹ thuật - Mua sắm - Xây dựng (EPC): Nhà thầu chịu trách nhiệm hoàn toàn về mọi thứ từ thiết kế, mua sắm vật tư thiết bị, đến thi công và bàn giao.\n\nDesign-Build: Một phương pháp hiện đại tối ưu hóa mô hình truyền thống (Design-Bid-Build), giúp rút ngắn thời gian bằng cách thực hiện thiết kế chi tiết song song với hoặc ngay trước khi thi công, thay vì chờ đấu thầu.", imageUrl = "/images/upload/services/01921bad60bc4188b3e194f9d85bcf39.png" },
                    new { text = "1. Phương pháp thiết kế - đấu thầu - xây dựng (truyền thống):\nChủ đầu tư phải lựa chọn nhiều nhà thầu cho từng giai đoạn riêng biệt: trao thiết kế cho công ty tư vấn chuyên nghiệp, thi công cho nhà thầu xây dựng và vật tư/thiết bị do nhà cung cấp cung cấp theo quy trình. Phương pháp này yêu cầu thiết kế chi tiết và được phê duyệt đầy đủ trước khi triển khai.\n\n2. Kỹ thuật - Mua sắm - Phương pháp xây dựng (Hiện đại):\nChủ đầu tư chỉ cần chuẩn bị thiết kế sơ bộ, sau đó chỉ định một nhà thầu duy nhất chịu trách nhiệm chìa khóa trao tay từ thiết kế chi tiết, mua sắm, thi công cho đến khi bàn giao dự án", imageUrl = "/images/upload/services/1bbb66eebdc3489abe7f6d40134a1b54.jpg" },
                    new { text = "Áp dụng phương pháp Design-Build mang lại nhiều lợi ích vượt trội cho cả chủ đầu tư và nhà thầu:\n\nQuản lý hợp lý cho chủ đầu tư: Nhà thầu thay mặt chủ đầu tư chịu trách nhiệm hoàn toàn từ điều phối đến quản lý dự án.\n\nGiảm thiểu rủi ro không phù hợp giữa thiết kế và thực tế: Nhờ thiết kế tự quản lý, nhà thầu có thể dễ dàng điều chỉnh các biện pháp thi công phù hợp, có thể triển khai sớm ngay cả khi thiết kế chưa hoàn thành, từ đó đẩy nhanh tiến độ và giảm thiểu chi phí phát sinh.", imageUrl = "/images/upload/services/6d91a196a9ae44f483e30438c19a5b56.jpg" },
                }),
            },
            new
            {
                Slug = "main-contractor", ShortTitle = "Main Contractor", SortOrder = 1,
                Title   = "Dịch vụ Tổng thầu chính",
                Tagline = "Quản lý trọn gói thi công — bàn giao chìa khóa trao tay.",
                Intro   = "Với vai trò Tổng thầu chính Việt – Nhật, NICON thực hiện đầy đủ các nhiệm vụ của một dự án xây dựng công nghiệp.",
                SectionsJson = JsonSerializer.Serialize(new[] {
                    new { heading = "Phạm vi công việc của Tổng thầu chính", body = new[] { "Quản lý toàn bộ công trường, điều phối các nhà thầu phụ và nhà cung cấp.", "Đảm bảo tiến độ, chất lượng và an toàn lao động (HSE) tại công trường.", "Báo cáo định kỳ cho chủ đầu tư bằng tiếng Việt – Anh – Nhật." } },
                    new { heading = "Phương pháp quản lý chuẩn quốc tế", body = new[] { "Áp dụng tiêu chuẩn quản lý dự án PMP và phương pháp Lean Construction.", "Sử dụng phần mềm MS Project, Primavera P6 cho lập tiến độ và kiểm soát chi phí.", "Quy trình QA/QC theo ISO 9001:2015 cho từng hạng mục thi công." } },
                    new { heading = "Đối tác chiến lược cùng Mori Group", body = new[] { "Sự hợp tác cùng Mori Industry Group (Nhật Bản) mang đến tiêu chuẩn kỹ thuật và văn hóa làm việc chuẩn Nhật cho mọi dự án NICON đảm nhận." } },
                }),
                HighlightsJson  = JsonSerializer.Serialize(new[] { "18+ năm kinh nghiệm", "Quản lý PMP", "QA/QC ISO 9001", "An toàn HSE chuẩn Nhật" }),
                IntroBlocksJson = JsonSerializer.Serialize(new[] {
                    new { text = "", imageUrl = "/images/upload/services/69c646fb16ac4ec5b3be0e7e62a2ffc2.jpg" },
                }),
            },
            new
            {
                Slug = "general-contractor", ShortTitle = "General Contractor", SortOrder = 2,
                Title   = "Dịch vụ Tổng thầu",
                Tagline = "Đảm nhận toàn bộ vòng đời thi công nhà máy công nghiệp.",
                Intro   = "Với cương vị Tổng thầu Việt Nam – Nhật Bản, NICON thực hiện đầy đủ nhiệm vụ của một dự án xây dựng công nghiệp gồm thiết kế, xin phép, thi công và bàn giao trọn gói.",
                SectionsJson = JsonSerializer.Serialize(new[] {
                    new { heading = "Vai trò Tổng thầu", body = new[] { "Quản lý toàn diện từ thiết kế cơ sở, thiết kế kỹ thuật đến bản vẽ thi công.", "Mua sắm vật tư – thiết bị (Procurement) và quản lý chuỗi cung ứng cho dự án.", "Tổ chức thi công, nghiệm thu từng phần và bàn giao công trình hoàn chỉnh." } },
                    new { heading = "Năng lực mega-project", body = new[] { "NICON đã thành công thực hiện các tổ hợp công nghiệp 250.000 m² như Lâm Hiệp Hưng – Tân Toàn Phát.", "Năng lực tổ chức công trường lớn với hàng trăm công nhân, thiết bị nặng và logistics phức tạp." } },
                    new { heading = "Cam kết chất lượng", body = new[] { "100% công trình bàn giao đúng tiến độ trong 5 năm gần nhất.", "Bảo hành 24 tháng cho phần xây dựng, 12 tháng cho phần MEP." } },
                }),
                HighlightsJson  = JsonSerializer.Serialize(new[] { "Mega-project 250.000m²", "Procurement chuyên nghiệp", "Bảo hành 24 tháng", "Đa quốc gia" }),
                IntroBlocksJson = JsonSerializer.Serialize(new[] {
                    new { text = "", imageUrl = "/images/upload/services/2b6c2d8c9936439993180016b4654242.jpg" },
                }),
            },
            new
            {
                Slug = "mep-contractor", ShortTitle = "MEP Contractor", SortOrder = 3,
                Title   = "Dịch vụ Tổng thầu MEP",
                Tagline = "Hệ thống Cơ – Điện – Nước đồng bộ và tối ưu vận hành.",
                Intro   = "MEP (Mechanical – Electrical – Plumbing) là phần quan trọng quyết định hiệu quả vận hành nhà máy. NICON cung cấp dịch vụ tổng thầu MEP độc lập hoặc tích hợp trong gói D&B, với đội ngũ kỹ sư chuyên ngành giàu kinh nghiệm.",
                SectionsJson = JsonSerializer.Serialize(new[] {
                    new { heading = "Phạm vi MEP của NICON", body = new[] { "Hệ thống điện công nghiệp: trung – hạ thế, máy phát dự phòng, UPS, hệ chiếu sáng năng lượng cao.", "Hệ HVAC, thông gió và phòng sạch theo cấp ISO Class 5/7/8.", "Hệ cấp – thoát nước, nước nóng năng lượng mặt trời, hệ xử lý nước thải.", "Hệ PCCC sprinkler, báo cháy địa chỉ theo TCVN và NFPA." } },
                    new { heading = "Tích hợp và bàn giao", body = new[] { "Quy trình T&C (Testing & Commissioning) bài bản, có sự chứng kiến của tư vấn giám sát và chủ đầu tư.", "Bàn giao kèm hồ sơ As-built, sách hướng dẫn vận hành – bảo trì (O&M Manual).", "Đào tạo vận hành cho đội ngũ kỹ thuật của chủ đầu tư." } },
                    new { heading = "Quản lý dự án bằng BIM", body = new[] { "Mô hình MEP 3D phát hiện xung đột hạng mục trước khi thi công, giảm 80% chỉnh sửa hiện trường.", "Tài liệu BIM bàn giao cho chủ đầu tư phục vụ vận hành – bảo trì lâu dài." } },
                }),
                HighlightsJson  = JsonSerializer.Serialize(new[] { "BIM MEP 3D", "Phòng sạch ISO 5-8", "T&C chuyên nghiệp", "O&M training" }),
                IntroBlocksJson = JsonSerializer.Serialize(new[] {
                    new { text = "1. Quản lý\nBằng cách sử dụng Nicon làm đầu mối quản lý duy nhất, các công việc quản lý thông tin và quản lý nguồn nhân lực trở nên thuận tiện hơn.\n\n2. Chất lượng\nChất lượng dự án được đảm bảo bởi danh tiếng, uy tín và sự cam kết của Nicon.\n\n3. Tiến độ\nTiến độ dự án được đảm bảo bởi năng lực và sự cam kết của Nicon.\n\n4. Chi phí\nNgân sách dự án được quản lý một cách hiệu quả, giúp gia tăng lợi nhuận.", imageUrl = "/images/upload/services/cfec2296483c4862b38062bb787a03bb.jpg" },
                    new { text = "", imageUrl = "/images/upload/services/be65ae444d6b48c7b3eb4f79badfc363.jpg" },
                }),
            },
        };

        var existing = db.ServiceItems.ToDictionary(s => s.Slug);

        foreach (var seed in seeds)
        {
            if (existing.TryGetValue(seed.Slug, out var item))
            {
                item.Title = seed.Title;
                item.ShortTitle = seed.ShortTitle;
                item.Tagline = seed.Tagline;
                item.Intro = seed.Intro;
                item.SectionsJson = seed.SectionsJson;
                item.HighlightsJson = seed.HighlightsJson;
                item.IntroBlocksJson = seed.IntroBlocksJson;
                item.SortOrder = seed.SortOrder;
                item.UpdatedAt = now;
            }
            else
            {
                db.ServiceItems.Add(new ServiceItem
                {
                    Slug = seed.Slug,
                    Title = seed.Title,
                    ShortTitle = seed.ShortTitle,
                    Tagline = seed.Tagline,
                    Intro = seed.Intro,
                    SectionsJson = seed.SectionsJson,
                    HighlightsJson = seed.HighlightsJson,
                    IntroBlocksJson = seed.IntroBlocksJson,
                    SortOrder = seed.SortOrder,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
            }
        }

        db.SaveChanges();
    }

    // ─── Logos ───────────────────────────────────────────────────────

    private static void SeedLogos(AppDbContext db)
    {
        var logos = new List<ClientLogo>();
        var i = 0;

        if (!db.ClientLogos.Any(l => l.Kind == LogoKind.Client))
        {
            string[][] clients = [
                ["CLOTEX", "/images/logos/clients/clotex.png"],
                ["SBMT", "/images/logos/clients/sbmt.png"],
                ["SMITH MULLER", "/images/logos/clients/smith-muller.jpeg"],
                ["LAM HIEP HUNG", "/images/logos/clients/lam-hiep-hung.jpeg"],
                ["NESTLE", "/images/logos/clients/nestle.jpeg"],
                ["REBISCO", "/images/logos/clients/rebisco.jpeg"],
                ["S.T.FOOD MARKETING", "/images/logos/clients/stfood-marketing.png"],
                ["PHAM-ASSET", "/images/logos/clients/pham-asset.png"],
                ["WATTENS", "/images/logos/clients/wattens.jpeg"],
                ["GREAT LOTUS", "/images/logos/clients/great-lotus.jpeg"],
                ["ADVANCED CASTING ASIA", "/images/logos/clients/advanced-casting-asia.jpeg"],
                ["EVERGREEN", "/images/logos/clients/evergreen.jpeg"],
                ["APM SPRINGS", "/images/logos/clients/apm-springs.jpeg"],
                ["RED BULL", "/images/logos/clients/red-bull.png"],
                ["SALADSTOP", "/images/logos/clients/saladstop.jpeg"],
                ["BMT GROUP", "/images/logos/clients/bmt-group.jpeg"],
                ["LAVIE", "/images/logos/clients/lavie.jpeg"],
                ["DOMINSNANT", "/images/logos/clients/akati-wood.png"],
                ["TLC", "/images/logos/clients/tlc.png"],
                ["JAPAN PLUS", "/images/logos/clients/japan-plus.jpeg"],
                ["AMPHARCO U.S.A", "/images/logos/clients/ampharco-usa.png"],
                ["NISSI", "/images/logos/clients/nissi.png"],
                ["SCTV", "/images/logos/clients/sctv.png"],
                ["H.B.FULLER", "/images/logos/clients/hb-fuller.png"],
                ["SONADEZI", "/images/logos/clients/sonadezi.jpeg"],
                ["MYUNGBO", "/images/logos/clients/myungbo.jpeg"],
                ["HEART OF DARKNESS", "/images/logos/clients/heart-of-darkness.jpeg"],
            ];
            foreach (var c in clients)
                logos.Add(new ClientLogo { Name = c[0], ImageUrl = c[1], Kind = LogoKind.Client, SortOrder = i++ });
        }

        if (!db.ClientLogos.Any(l => l.Kind == LogoKind.Partner))
        {
            string[][] partners = [
                ["VSIP", "/images/logos/partners/vsip.jpeg"],
                ["RESCO", "/images/logos/partners/resco.jpeg"],
                ["TECHCONS", "/images/logos/partners/techcons.jpeg"],
                ["ZONA", "/images/logos/partners/zona.png"],
                ["HAM KIEM I", "/images/logos/partners/ham-kiem-i.png"],
                ["CHAU DUC", "/images/logos/partners/chau-duc.jpeg"],
                ["PHU MY 3", "/images/logos/partners/phu-my-3.png"],
                ["AMATA", "/images/logos/partners/amata.jpeg"],
                ["TIN NGHIA", "/images/logos/partners/tin-nghia.jpeg"],
                ["IDICO", "/images/logos/partners/idico.jpeg"],
                ["VIETNAM RUBBER", "/images/logos/partners/vietnam-rubber.jpeg"],
                ["LONG DUC", "/images/logos/partners/long-duc.jpeg"],
                ["SONADEZI", "/images/logos/partners/sonadezi.jpeg"],
                ["PROTRADE", "/images/logos/partners/protrade.png"],
                ["LHC", "/images/logos/partners/lhc.png"],
                ["THANH YEN", "/images/logos/partners/thanh-yen.png"],
                ["VIETCOMBANK", "/images/logos/partners/vietcombank.jpeg"],
                ["HIEP PHUOC", "/images/logos/partners/hiep-phuoc.jpeg"],
                ["ACB", "/images/logos/partners/acb.jpeg"],
            ];
            i = 0;
            foreach (var p in partners)
                logos.Add(new ClientLogo { Name = p[0], ImageUrl = p[1], Kind = LogoKind.Partner, SortOrder = i++ });
        }

        if (!db.ClientLogos.Any(l => l.Kind == LogoKind.Supplier))
        {
            string[][] suppliers = [
                ["MPE-Inc", "/images/logos/suppliers/mpe-inc.jpeg"],
                ["Chi Thanh Steel", "/images/logos/suppliers/chi-thanh-steel.jpeg"],
                ["Hoa Phat Steel", "/images/logos/suppliers/hoa-phat-steel.jpeg"],
                ["Cadivi", "/images/logos/suppliers/cadivi.png"],
                ["EVN", "/images/logos/suppliers/evn.png"],
                ["Schneider", "/images/logos/suppliers/schneider.png"],
                ["Thinh Phat", "/images/logos/suppliers/thinh-phat.jpeg"],
                ["LS-Vina", "/images/logos/suppliers/ls-vina.jpeg"],
                ["Posco VN", "/images/logos/suppliers/posco-vn.png"],
                ["WhiteHorse Ceramic", "/images/logos/suppliers/whitehorse-ceramic.png"],
                ["Minh Viet Son", "/images/logos/suppliers/minh-viet-son.jpeg"],
                ["Song Hop Luc", "/images/logos/suppliers/song-hop-luc.jpeg"],
                ["Duhal Led", "/images/logos/suppliers/duhal-led.jpeg"],
                ["Eurowindow", "/images/logos/suppliers/eurowindow.jpeg"],
                ["SINO", "/images/logos/suppliers/sino.jpeg"],
                ["Caesar", "/images/logos/suppliers/caesar.jpeg"],
                ["Tai Truong Thanh", "/images/logos/suppliers/tai-truong-thanh.jpeg"],
                ["Holcim", "/images/logos/suppliers/holcim.jpeg"],
                ["Viglacera", "/images/logos/suppliers/viglacera.jpeg"],
                ["American Standard", "/images/logos/suppliers/american-standard.jpeg"],
                ["Vina Kyoei", "/images/logos/suppliers/vina-kyoei.jpeg"],
                ["Binh Minh", "/images/logos/suppliers/binh-minh.jpeg"],
                ["Taicera", "/images/logos/suppliers/taicera.jpeg"],
                ["LHC", "/images/logos/suppliers/lhc.png"],
                ["ACG", "/images/logos/suppliers/acg.png"],
            ];
            i = 0;
            foreach (var s in suppliers)
                logos.Add(new ClientLogo { Name = s[0], ImageUrl = s[1], Kind = LogoKind.Supplier, SortOrder = i++ });
        }

        if (!db.ClientLogos.Any(l => l.Kind == LogoKind.Award))
        {
            string[][] awards = [
                ["Top 10 Vietnam Leading Brands 2018", "/images/activities/activity-ceremony.jpg"],
                ["Vietnam Golden FDI 2019", "/images/activities/activity-opening.jpg"],
                ["Outstanding Design & Build Contractor", "/images/activities/activity-handover.jpg"],
            ];
            i = 0;
            foreach (var a in awards)
                logos.Add(new ClientLogo { Name = a[0], ImageUrl = a[1], Kind = LogoKind.Award, SortOrder = i++ });
        }

        if (logos.Count > 0)
        {
            db.ClientLogos.AddRange(logos);
            db.SaveChanges();
        }

        // Always-run upserts: replace CLOTEX → BIDV, SCON → SBMT, AKATI WOOD → DOMINSNANT, remove AMPHACO
        var amphacoLogo = db.ClientLogos.FirstOrDefault(l => l.Name == "AMPHACO" && l.Kind == LogoKind.Client);
        if (amphacoLogo != null)
            db.ClientLogos.Remove(amphacoLogo);

        var akatiLogo = db.ClientLogos.FirstOrDefault(l => l.Name == "AKATI WOOD" && l.Kind == LogoKind.Client);
        if (akatiLogo != null)
        {
            akatiLogo.Name = "DOMINSNANT";
            akatiLogo.ImageUrl = "/images/logos/clients/akati-wood.png";
        }

        var sconLogo = db.ClientLogos.FirstOrDefault(l => l.Name == "SCON" && l.Kind == LogoKind.Client);
        if (sconLogo != null)
        {
            sconLogo.Name = "SBMT";
            sconLogo.ImageUrl = "/images/logos/clients/sbmt.png";
        }

        var clotexLogo = db.ClientLogos.FirstOrDefault(l => l.Name == "CLOTEX" && l.Kind == LogoKind.Client);
        if (clotexLogo != null)
        {
            clotexLogo.Name = "BIDV";
            clotexLogo.ImageUrl = "/images/logos/clients/bidv.png";
        }

        if (!db.ClientLogos.Any(l => l.Name == "MEDICARE" && l.Kind == LogoKind.Client))
        {
            var maxOrder = db.ClientLogos
                .Where(l => l.Kind == LogoKind.Client)
                .Max(l => (int?)l.SortOrder) ?? 0;
            db.ClientLogos.Add(new ClientLogo
            {
                Name = "MEDICARE",
                ImageUrl = "/images/logos/clients/medicare.png",
                Kind = LogoKind.Client,
                SortOrder = maxOrder + 1,
            });
        }

        // Ensure AGC partner logo exists
        if (!db.ClientLogos.Any(l => l.Name == "AGC" && l.Kind == LogoKind.Partner))
        {
            var maxPartnerOrder = db.ClientLogos
                .Where(l => l.Kind == LogoKind.Partner)
                .Max(l => (int?)l.SortOrder) ?? 0;
            db.ClientLogos.Add(new ClientLogo
            {
                Name = "AGC",
                ImageUrl = "/images/logos/partners/agc.png",
                Kind = LogoKind.Partner,
                SortOrder = maxPartnerOrder + 1,
            });
        }

        // Remove obsolete supplier logos
        string[] obsoleteSuppliers = [
            "Seamasterpaint", "Nippon", "Vicem Cement", "Fico Cement",
            "Dong Tam Group", "Dulux", "Sika", "Shell",
            "VN Steel", "QSB Steel", "Zamil Steel", "BlueScope", "TungShin",
        ];
        var toRemove = db.ClientLogos
            .Where(l => l.Kind == LogoKind.Supplier && obsoleteSuppliers.Contains(l.Name))
            .ToList();
        if (toRemove.Count > 0)
            db.ClientLogos.RemoveRange(toRemove);

        // Add LHC supplier if not present
        if (!db.ClientLogos.Any(l => l.Name == "LHC" && l.Kind == LogoKind.Supplier))
        {
            var maxSupOrder = db.ClientLogos
                .Where(l => l.Kind == LogoKind.Supplier)
                .Max(l => (int?)l.SortOrder) ?? 0;
            db.ClientLogos.Add(new ClientLogo
            {
                Name = "LHC",
                ImageUrl = "/images/logos/suppliers/lhc.png",
                Kind = LogoKind.Supplier,
                SortOrder = maxSupOrder + 1,
            });
        }

        // Add ACG supplier if not present
        if (!db.ClientLogos.Any(l => l.Name == "ACG" && l.Kind == LogoKind.Supplier))
        {
            var maxSupOrder = db.ClientLogos
                .Where(l => l.Kind == LogoKind.Supplier)
                .Max(l => (int?)l.SortOrder) ?? 0;
            db.ClientLogos.Add(new ClientLogo
            {
                Name = "ACG",
                ImageUrl = "/images/logos/suppliers/acg.png",
                Kind = LogoKind.Supplier,
                SortOrder = maxSupOrder + 1,
            });
        }

        // Fix any localhost URLs in award logos
        var awardLogoFixes = new Dictionary<string, string>
        {
            ["Top 10 Vietnam Leading Brands 2018"] = "/images/activities/activity-ceremony.jpg",
            ["Vietnam Golden FDI 2019"] = "/images/activities/activity-opening.jpg",
            ["Outstanding Design & Build Contractor"] = "/images/activities/activity-handover.jpg",
        };
        foreach (var (name, relUrl) in awardLogoFixes)
        {
            var logo = db.ClientLogos.FirstOrDefault(l => l.Kind == LogoKind.Award && l.Name == name);
            if (logo != null && logo.ImageUrl != relUrl)
                logo.ImageUrl = relUrl;
        }

        db.SaveChanges();
    }

    // ─── Processes ──────────────────────────────────────────────────

    private static void SeedProcesses(AppDbContext db)
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "nihomebackend.Data.Seeds.content.processes.json";
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null) return;

        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var seedItems = JsonSerializer.Deserialize<List<ProcessSeedItem>>(stream, opts) ?? [];

        // Re-seed when count mismatches OR when asset JSON columns haven't been populated yet
        var seedWithAssets = seedItems.Any(s => s.Images.Count > 0 || s.Files.Count > 0);
        var dbHasAssets = db.ProcessDocuments.Any(p => p.ImagesJson != null || p.FilesJson != null);
        if (db.ProcessDocuments.Count() == seedItems.Count && (!seedWithAssets || dbHasAssets)) return;

        db.ProcessDocuments.RemoveRange(db.ProcessDocuments);
        db.SaveChanges();

        var items = seedItems.Select(seed => new ProcessDocument
        {
            GroupKey = seed.GroupKey,
            Code = seed.Code,
            Title = seed.Title,
            SortOrder = seed.SortOrder,
            ImagesJson = seed.Images.Count > 0 ? JsonSerializer.Serialize(seed.Images) : null,
            FilesJson = seed.Files.Count > 0 ? JsonSerializer.Serialize(seed.Files) : null,
        }).ToList();

        db.ProcessDocuments.AddRange(items);
        db.SaveChanges();
    }

    private sealed class ProcessSeedItem
    {
        public string GroupKey { get; set; } = "";
        public string? Code { get; set; }
        public string Title { get; set; } = "";
        public int SortOrder { get; set; }
        public List<ProcessAssetSeedItem> Images { get; set; } = [];
        public List<ProcessAssetSeedItem> Files { get; set; } = [];
    }

    private sealed class ProcessAssetSeedItem
    {
        public string DisplayName { get; set; } = "";
        public string Url { get; set; } = "";
        public string OriginalFileName { get; set; } = "";
        public string ContentType { get; set; } = "";
        public int SortOrder { get; set; }
    }

    // ─── Slideshow ──────────────────────────────────────────────────

    private static void SeedSlideshow(AppDbContext db)
    {
        var seeds = new[]
        {
            new { Slug = "hero-factory",      ImageUrl = "/images/projects/project-bma.jpg",    Title = "Tổng thầu Thiết kế & Thi công Nhà máy",    Subtitle = "Hơn 18 năm kinh nghiệm — 150+ dự án công nghiệp",                            LinkUrl = "/projects",                   LinkText = "Xem dự án",     IsActive = true, SortOrder = 0 },
            new { Slug = "hero-design-build", ImageUrl = "/images/projects/project-nbdc.jpg",   Title = "Design & Build — Giải pháp trọn gói",       Subtitle = "Một đầu mối — toàn bộ vòng đời dự án từ thiết kế đến bàn giao",              LinkUrl = "/services/design-and-build",  LinkText = "Tìm hiểu thêm", IsActive = true, SortOrder = 1 },
            new { Slug = "hero-industrial",   ImageUrl = "/images/projects/project-lhh.jpg",    Title = "Nhà máy Công nghiệp Quy mô lớn",            Subtitle = "Tổ hợp 250.000 m² — Tiêu chuẩn Nhật Bản cùng Mori Group",                   LinkUrl = "/projects/nha-may-lhh",       LinkText = "Xem chi tiết",  IsActive = true, SortOrder = 2 },
            new { Slug = "hero-sports-center",ImageUrl = "/images/projects/project-sports.jpg", Title = "Công trình Thể dục Thể thao",               Subtitle = "Thiết kế không gian thể thao đa năng phục vụ cộng đồng",                     LinkUrl = "/projects/ttdtt-thu-duc",     LinkText = "Khám phá",      IsActive = true, SortOrder = 3 },
            new { Slug = "hero-office",       ImageUrl = "/images/projects/project-office.jpg", Title = "Nội thất Văn phòng Hiện đại",               Subtitle = "Phong cách tối giản — Không gian mở — Tiêu chuẩn quốc tế",                   LinkUrl = "/projects/noi-that-b37",      LinkText = "Xem dự án",     IsActive = true, SortOrder = 4 },
        };

        var existing = db.SlideshowItems.ToDictionary(s => s.Slug);
        var now = DateTime.UtcNow;

        foreach (var s in seeds)
        {
            if (existing.TryGetValue(s.Slug, out var item))
            {
                item.ImageUrl = s.ImageUrl;
                item.Title = s.Title;
                item.Subtitle = s.Subtitle;
                item.LinkUrl = s.LinkUrl;
                item.LinkText = s.LinkText;
                item.IsActive = s.IsActive;
                item.SortOrder = s.SortOrder;
                item.UpdatedAt = now;
            }
            else
            {
                db.SlideshowItems.Add(new SlideshowItem
                {
                    Slug = s.Slug,
                    ImageUrl = s.ImageUrl,
                    Title = s.Title,
                    Subtitle = s.Subtitle,
                    LinkUrl = s.LinkUrl,
                    LinkText = s.LinkText,
                    IsActive = s.IsActive,
                    SortOrder = s.SortOrder,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
            }
        }

        db.SaveChanges();
    }

    // ─── Recruitment ────────────────────────────────────────────────

    private static void SeedAboutSections(AppDbContext db)
    {
        EnsureCatalogueDownloadsSection(db);

        var now = DateTime.UtcNow;

        var seeds = new AboutSectionContent[]
        {
            new() { Slug = "about-main", SortOrder = 0, IsActive = true, Eyebrow = "GIỚI THIỆU CHUNG", TitleA = "Đối tác của sự", TitleB = "phát triển từ 2006", Paragraph1 = "Hơn 18 năm đồng hành cùng các nhà đầu tư trong và ngoài nước, NICON kiến tạo những công trình công nghiệp và dân dụng đạt chuẩn quốc tế.", Paragraph2 = "NICON chuyên thiết kế và thi công xây dựng công nghiệp, dân dụng chất lượng cao. Là đơn vị tiên phong áp dụng phương pháp tích hợp từ khâu nghiên cứu, thiết kế đến thi công, NICON giúp tối ưu hóa quy trình và mang lại hiệu quả cao nhất cho khách hàng.", ImageUrl = "/images/upload/about/102daea54ad641cdb16005d9fd1ec3b6.png" },
            new() { Slug = "stats-main", SortOrder = 1, IsActive = true, Eyebrow = "CHỈ SỐ NỔI BẬT", ItemsJson = JsonSerializer.Serialize(new[] { new { iconKey = "calendar", sortOrder = 0, isActive = true, num = "18+", label = "Năm kinh nghiệm" }, new { iconKey = "building", sortOrder = 1, isActive = true, num = "150+", label = "Dự án hoàn thành" }, new { iconKey = "users", sortOrder = 2, isActive = true, num = "80+", label = "Khách hàng đồng hành" }, new { iconKey = "award", sortOrder = 3, isActive = true, num = "ISO", label = "Chuẩn hóa chất lượng" } }) },
            new() { Slug = "values-main", SortOrder = 2, IsActive = true, Eyebrow = "GIÁ TRỊ CỐT LÕI", TitleA = "Nền tảng phát triển", TitleB = "NICON", ItemsJson = JsonSerializer.Serialize(new[] { new { iconKey = "target", sortOrder = 0, isActive = true, title = "Mục tiêu rõ ràng", desc = "Mọi quyết định đều hướng đến hiệu quả đầu tư và mục tiêu dài hạn của khách hàng." }, new { iconKey = "shield", sortOrder = 1, isActive = true, title = "Kỷ luật chất lượng", desc = "Quy trình thi công, giám sát và nghiệm thu được kiểm soát nghiêm ngặt." }, new { iconKey = "compass", sortOrder = 2, isActive = true, title = "Định hướng bền vững", desc = "Ưu tiên giải pháp tối ưu vận hành, chi phí và vòng đời công trình." }, new { iconKey = "heart", sortOrder = 3, isActive = true, title = "Tận tâm đồng hành", desc = "Xây dựng niềm tin bằng cách làm việc minh bạch và trách nhiệm đến cùng." } }) },
            new() { Slug = "strategy-main", SortOrder = 3, IsActive = true, Eyebrow = "CHIẾN LƯỢC", TitleA = "Tư duy hệ thống cho", TitleB = "mỗi dự án", Paragraph1 = "Tầm nhìn: Trở thành tổng thầu thiết kế - thi công uy tín hàng đầu trong lĩnh vực công nghiệp và dân dụng tại Việt Nam. Không chỉ là xây dựng các công trình chất lượng mà còn là xây dựng những mối quan hệ bền vững với khách hàng, đối tác, và cộng đồng.", Paragraph2 = "Định hướng tương lai: Liên tục nâng cao năng lực thiết kế, quản lý và công nghệ để đáp ứng các tiêu chuẩn quốc tế ngày càng cao.", ItemsJson = JsonSerializer.Serialize(new[] { new { iconKey = "home", sortOrder = 0, isActive = true, title = "Công trình dân dụng", desc = "Xây dựng các dự án dân dụng từ nhà ở, trường học, đến các tòa nhà thương mại." }, new { iconKey = "hammer", sortOrder = 1, isActive = true, title = "Công trình công nghiệp", desc = "Đảm nhận các dự án xây dựng nhà xưởng, khu công nghiệp, và các công trình liên quan đến sản xuất." }, new { iconKey = "layers", sortOrder = 2, isActive = true, title = "Xây dựng hạ tầng", desc = "Phát triển cơ sở hạ tầng từ giao thông, cấp thoát nước, đến các công trình năng lượng." }, new { iconKey = "wrench", sortOrder = 3, isActive = true, title = "Thiết kế công trình", desc = "Bao gồm các hạng mục kiến trúc, kết cấu, điện nước, hạ tầng, cảnh quan, và quy hoạch tổng thể." }, new { iconKey = "briefcase", sortOrder = 4, isActive = true, title = "Tư vấn đầu tư và đầu tư xây dựng", desc = "Cung cấp các giải pháp đầu tư hiệu quả và hỗ trợ trong việc triển khai dự án xây dựng." }, new { iconKey = "users-group", sortOrder = 5, isActive = true, title = "Tư vấn quản lý xây dựng", desc = "Đảm bảo quá trình thi công đạt chất lượng và tiến độ như cam kết." }, new { iconKey = "handshake", sortOrder = 6, isActive = true, title = "Cung cấp vật liệu xây dựng", desc = "Đảm bảo nguồn cung ứng vật liệu xây dựng chất lượng cao, phù hợp với yêu cầu kỹ thuật của từng dự án." }, new { iconKey = "building", sortOrder = 7, isActive = true, title = "Giao dịch bất động sản", desc = "Tư vấn và hỗ trợ các hoạt động mua bán, chuyển nhượng và quản lý bất động sản." } }) },
            new() { Slug = "organization-main", SortOrder = 4, IsActive = true, Eyebrow = "TỔ CHỨC", TitleA = "Bộ máy điều hành", TitleB = "vững mạnh", ItemsJson = JsonSerializer.Serialize(new { board = new[] { new { sortOrder = 0, role = "Chủ tịch", name = "Kiến trúc sư. Võ Trí Nguyên" }, new { sortOrder = 1, role = "Phó chủ tịch", name = "Kỹ sư. Yoshihiro Mori" }, new { sortOrder = 2, role = "Phó chủ tịch", name = "Kiến trúc sư. Lê Thị Yến" }, new { sortOrder = 3, role = "Thư ký", name = "MBA.Võ Tố Uyên" } }, directors = new[] { new { sortOrder = 0, role = "Tổng giám đốc", name = "Kiến trúc sư. Võ Trí Nguyên" }, new { sortOrder = 1, role = "Giám đốc phát triển kinh doanh Nhật Bản", name = "Ông Yoshihiro Mori" }, new { sortOrder = 2, role = "phát triển kinh doanh khu vực châu Á", name = "Ông Richard Penalosa" }, new { sortOrder = 3, role = "Giám đốc thiết kế", name = "Kiến trúc sư. Lê Thị Yến" } }, companyChartUrl = "/images/upload/about/26ff4d672035407c989e7aaf1231f5d3.png", siteChartUrl = "/images/upload/about/9972ac8d4ff643a0a88acac542a1ee13.jpg" }) },
            new() { Slug = "timeline-main", SortOrder = 5, IsActive = true, Eyebrow = "LỊCH SỬ", TitleA = "Dấu mốc phát triển", TitleB = "qua từng giai đoạn", ImageUrl = "/images/upload/cac99fa59b264bd7ade9789960bf781e.jpeg", ItemsJson = JsonSerializer.Serialize(new[] { new { sortOrder = 0, year = "2006", title = "Thành lập NICON", desc = "Đặt nền móng cho hành trình phát triển trong lĩnh vực xây dựng công nghiệp." }, new { sortOrder = 1, year = "2007", title = "Mở rộng đội ngũ", desc = "Tăng cường năng lực triển khai và quản lý dự án." }, new { sortOrder = 2, year = "2008", title = "Nhà thầu Thiết kế và Thi công", desc = "Bắt đầu đồng hành cùng nhiều nhà đầu tư nước ngoài." }, new { sortOrder = 3, year = "2010", title = "Tăng cường hợp tác", desc = "Tăng cường kết nối với các đối tác trong và ngoài nước." }, new { sortOrder = 4, year = "2016", title = "M&A với Mori Group (Nhật Bản)", desc = "Với sự hợp tác này, MORI INDUSTRY GROUP đã trở thành Đối tác chiến lược của NICON" }, new { sortOrder = 5, year = "2018", title = "Top 10 Thương hiệu dẫn đầu Việt Nam", desc = "Khẳng định vị thế tổng thầu uy tín với nhiều dự án quy mô lớn." }, new { sortOrder = 6, year = "2026", title = "Tiếp tục tăng trưởng", desc = "Khẳng định vị thế qua những công trình chất lượng và không ngừng mở rộng quy mô dự án." } }) },
            new() { Slug = "certs-main", SortOrder = 6, IsActive = true, Eyebrow = "CHỨNG NHẬN", TitleA = "Tiêu chuẩn vận hành", TitleB = "đáng tin cậy", ItemsJson = JsonSerializer.Serialize(new[] { new { sortOrder = 0, name = "ISO 9001:2008", desc = "Hệ thống quản lý chất lượng.", imageUrl = "/images/upload/about/c8bb1be2415a4f1d9ba028a20fbd6d76.jpg" }, new { sortOrder = 1, name = "ISO 9001:2015", desc = "Chuẩn hóa quy trình và cải tiến liên tục.", imageUrl = "/images/upload/about/cf5320dd3930445da2ed045758f2a60b.jpg" }, new { sortOrder = 2, name = "ISO 14001:2015", desc = "Quản lý môi trường trong thi công và vận hành.", imageUrl = "/images/upload/about/4bc5031a2cb941ee8aeeb644a29a4d55.jpg" }, new { sortOrder = 3, name = "Certifications of Nicon JSC", desc = "Tiêu chuẩn thiết kế và thi công phòng sạch, nhà xưởng chuyên biệt.", imageUrl = "/images/upload/about/9681ac567906459da806ffb8ee92df68.jpg" } }) },
            new() { Slug = "downloads-main", SortOrder = 7, IsActive = true, Eyebrow = "TÀI LIỆU", TitleA = "Hồ sơ năng lực", TitleB = "và tài liệu tham khảo", Paragraph1 = "Tổng hợp các tài liệu giới thiệu năng lực, chứng nhận và thông tin doanh nghiệp phục vụ đối tác, khách hàng và nhà đầu tư.", ItemsJson = JsonSerializer.Serialize(new[] { new { sortOrder = 0, name = "Company Profile", size = "77 KB", type = "JPEG", url = "/files/cv/d560cb8311a84a1784c8decf55c31884.jpeg" }, new { sortOrder = 1, name = "Brochure năng lực", size = "76.8 MB", type = "PDF", url = "/files/cv/71a61d81a6a14c25980dfbe9f01d1462.pdf" }, new { sortOrder = 2, name = "ISO Certificates", size = "82 KB", type = "JPG", url = "/files/cv/93ba59bb511e43f3a61c44ba4ea5b6f4.jpg" }, new { sortOrder = 3, name = "Danh mục dự án tiêu biểu", size = "74 KB", type = "JPEG", url = "/files/cv/25631b4fd7f64c538cbaeb9a7b1c0f83.jpeg" } }) },
        };

        var existing = db.AboutSectionContents.ToDictionary(a => a.Slug);
        foreach (var seed in seeds)
        {
            if (existing.TryGetValue(seed.Slug, out var item))
            {
                item.Eyebrow = seed.Eyebrow;
                item.TitleA = seed.TitleA;
                item.TitleB = seed.TitleB;
                item.Paragraph1 = seed.Paragraph1;
                item.Paragraph2 = seed.Paragraph2;
                item.ImageUrl = seed.ImageUrl;
                item.ItemsJson = seed.ItemsJson;
                item.IsActive = seed.IsActive;
                item.SortOrder = seed.SortOrder;
                item.UpdatedAt = now;
            }
            else
            {
                seed.CreatedAt = now;
                seed.UpdatedAt = now;
                db.AboutSectionContents.Add(seed);
            }
        }

        db.SaveChanges();
    }

    private static void EnsureCatalogueDownloadsSection(AppDbContext db)
    {
        var existing = db.AboutSectionContents.FirstOrDefault(x => x.Slug == "downloads-main");
        if (existing == null) return;

        // Only patch the placeholder seed (url = "#"). If admin has edited the
        // downloads, leave their content alone.
        if (existing.ItemsJson is null || !existing.ItemsJson.Contains("\"url\":\"#\"", StringComparison.Ordinal)) return;

        existing.Eyebrow = "CATALOGUE";
        existing.TitleA = "Catalogue";
        existing.TitleB = "& hồ sơ năng lực";
        existing.Paragraph1 = "Tải Catalogue và các tài liệu giới thiệu năng lực, chứng nhận của NICON dành cho đối tác, khách hàng và nhà đầu tư.";
        existing.ItemsJson = JsonSerializer.Serialize(new[]
        {
            new { sortOrder = 0, name = "NICON Brochure", size = "77 MB", type = "PDF", url = "/files/Nicon-brochure.pdf" },
        });
        existing.UpdatedAt = DateTime.UtcNow;
        db.SaveChanges();
    }

    private static void SeedRecruitment(AppDbContext db)
    {
        SeedEmploymentTypes(db);

        if (db.JobPositions.Any()) return;

        var now = DateTime.UtcNow;

        var positions = new JobPosition[]
        {
            new()
            {
                Title = "Kỹ sư Xây dựng (Site Engineer)",
                Department = "Phòng Thi công",
                Location = "TP. Hồ Chí Minh",
                EmploymentType = "full-time",
                ExperienceLevel = "mid",
                Description = "Giám sát thi công trực tiếp tại công trường, kiểm tra chất lượng vật liệu, đảm bảo tiến độ và an toàn lao động theo tiêu chuẩn ISO 9001:2015.",
                RequirementsJson = JsonSerializer.Serialize(new[]
                {
                    "Tốt nghiệp Đại học ngành Xây dựng Dân dụng & Công nghiệp",
                    "Ít nhất 2 năm kinh nghiệm thi công nhà xưởng, nhà máy",
                    "Đọc hiểu bản vẽ kết cấu, kiến trúc, MEP",
                    "Sử dụng thành thạo AutoCAD, MS Project",
                    "Có khả năng làm việc ngoài trời, chịu được áp lực tiến độ"
                }),
                IsActive = true,
                SortOrder = 0,
                CreatedAt = now,
                UpdatedAt = now
            },
            new()
            {
                Title = "Kiến trúc sư Thiết kế",
                Department = "Phòng Thiết kế",
                Location = "TP. Hồ Chí Minh",
                EmploymentType = "full-time",
                ExperienceLevel = "mid",
                Description = "Thiết kế kiến trúc cho các dự án nhà xưởng công nghiệp, văn phòng và công trình dân dụng. Phối hợp với đội kết cấu và MEP để hoàn thiện hồ sơ thiết kế.",
                RequirementsJson = JsonSerializer.Serialize(new[]
                {
                    "Tốt nghiệp Đại học ngành Kiến trúc",
                    "Thành thạo Revit, AutoCAD, SketchUp, Photoshop",
                    "Kinh nghiệm thiết kế công trình công nghiệp là lợi thế",
                    "Tư duy sáng tạo, cập nhật xu hướng thiết kế mới",
                    "Khả năng giao tiếp tốt với khách hàng và nội bộ"
                }),
                IsActive = true,
                SortOrder = 1,
                CreatedAt = now,
                UpdatedAt = now
            },
            new()
            {
                Title = "Nhân viên Kinh doanh Dự án",
                Department = "Phòng Kinh doanh",
                Location = "TP. Hồ Chí Minh",
                EmploymentType = "full-time",
                ExperienceLevel = "junior",
                Description = "Tìm kiếm và phát triển khách hàng doanh nghiệp, tư vấn giải pháp xây dựng trọn gói, theo dõi và chăm sóc khách hàng từ giai đoạn tiếp cận đến ký hợp đồng.",
                RequirementsJson = JsonSerializer.Serialize(new[]
                {
                    "Tốt nghiệp Đại học ngành Quản trị Kinh doanh, Marketing hoặc Xây dựng",
                    "Kỹ năng giao tiếp và thuyết trình xuất sắc",
                    "Có xe máy và sẵn sàng đi công tác",
                    "Ưu tiên có kinh nghiệm bán hàng B2B trong lĩnh vực xây dựng",
                    "Ngoại ngữ tiếng Anh hoặc tiếng Nhật là lợi thế lớn"
                }),
                IsActive = true,
                SortOrder = 2,
                CreatedAt = now,
                UpdatedAt = now
            },
            new()
            {
                Title = "Kỹ sư MEP (Cơ điện)",
                Department = "Phòng Thiết kế",
                Location = "TP. Hồ Chí Minh",
                EmploymentType = "full-time",
                ExperienceLevel = "senior",
                Description = "Thiết kế và giám sát hệ thống MEP (điện, nước, HVAC, PCCC) cho các dự án nhà xưởng công nghiệp và văn phòng quy mô lớn.",
                RequirementsJson = JsonSerializer.Serialize(new[]
                {
                    "Tốt nghiệp Đại học ngành Điện, Cơ khí hoặc Kỹ thuật Công trình",
                    "Tối thiểu 5 năm kinh nghiệm thiết kế MEP",
                    "Thành thạo Revit MEP, AutoCAD MEP",
                    "Am hiểu tiêu chuẩn PCCC, QCVN về hệ thống điện, nước",
                    "Kinh nghiệm dự án nhà máy, khu công nghiệp là bắt buộc"
                }),
                IsActive = true,
                SortOrder = 3,
                CreatedAt = now,
                UpdatedAt = now
            },
            new()
            {
                Title = "Thực tập sinh Kỹ thuật",
                Department = "Phòng Thi công",
                Location = "Bình Dương",
                EmploymentType = "intern",
                ExperienceLevel = "student",
                Description = "Hỗ trợ đội kỹ sư hiện trường trong công tác đo đạc, nghiệm thu, lập hồ sơ hoàn công. Cơ hội học hỏi thực tế tại các công trình nhà xưởng quy mô lớn.",
                RequirementsJson = JsonSerializer.Serialize(new[]
                {
                    "Sinh viên năm cuối ngành Xây dựng, Kiến trúc hoặc liên quan",
                    "Có thể thực tập toàn thời gian ít nhất 3 tháng",
                    "Chăm chỉ, ham học hỏi, chịu khó di chuyển",
                    "Biết sử dụng AutoCAD cơ bản"
                }),
                IsActive = true,
                SortOrder = 4,
                CreatedAt = now,
                UpdatedAt = now
            },
            new()
            {
                Title = "Kế toán Tổng hợp",
                Department = "Phòng Kế toán",
                Location = "TP. Hồ Chí Minh",
                EmploymentType = "full-time",
                ExperienceLevel = "mid",
                Description = "Theo dõi công nợ, lập báo cáo tài chính, xử lý thuế GTGT, TNCN và phối hợp kiểm toán. Hỗ trợ Ban Giám đốc phân tích chi phí dự án.",
                RequirementsJson = JsonSerializer.Serialize(new[]
                {
                    "Tốt nghiệp Đại học ngành Kế toán – Tài chính",
                    "Kinh nghiệm 2+ năm, ưu tiên ngành xây dựng",
                    "Thành thạo phần mềm kế toán (MISA, Fast, SAP)",
                    "Nắm vững Luật Thuế và chuẩn mực kế toán Việt Nam",
                    "Cẩn thận, trung thực, chịu được áp lực deadline"
                }),
                IsActive = false, // Đã đóng
                SortOrder = 5,
                CreatedAt = now,
                UpdatedAt = now
            },
        };

        db.JobPositions.AddRange(positions);
        db.SaveChanges();

        // Seed sample applications
        var posIds = db.JobPositions.Select(p => new { p.Id, p.Title }).ToList();

        var applications = new JobApplication[]
        {
            new()
            {
                JobPositionId = posIds[0].Id, // Kỹ sư Xây dựng
                CandidateName = "Nguyễn Minh Tuấn",
                Email = "tuan.nguyen@gmail.com",
                Phone = "0901234567",
                ExperienceYears = 4,
                CoverLetter = "Tôi có 4 năm kinh nghiệm giám sát thi công nhà xưởng tại các KCN Long An và Bình Dương. Rất mong được gia nhập đội ngũ NICON.",
                Status = "interview",
                AppliedAt = now.AddDays(-10),
                UpdatedAt = now.AddDays(-7)
            },
            new()
            {
                JobPositionId = posIds[0].Id, // Kỹ sư Xây dựng
                CandidateName = "Trần Thị Hương",
                Email = "huong.tran@outlook.com",
                Phone = "0912345678",
                ExperienceYears = 2,
                CoverLetter = "Tốt nghiệp ĐH Bách Khoa TP.HCM, đã tham gia 3 dự án nhà xưởng trong vai trò kỹ sư hiện trường.",
                Status = "new",
                AppliedAt = now.AddDays(-3),
                UpdatedAt = now.AddDays(-3)
            },
            new()
            {
                JobPositionId = posIds[1].Id, // Kiến trúc sư
                CandidateName = "Lê Quốc Bảo",
                Email = "bao.le.arch@gmail.com",
                Phone = "0933456789",
                ExperienceYears = 5,
                CoverLetter = "Kiến trúc sư với 5 năm kinh nghiệm tại công ty thiết kế hàng đầu. Đam mê thiết kế công nghiệp và muốn phát triển sự nghiệp tại NICON.",
                Status = "hired",
                AppliedAt = now.AddDays(-30),
                UpdatedAt = now.AddDays(-15)
            },
            new()
            {
                JobPositionId = posIds[2].Id, // Nhân viên Kinh doanh
                CandidateName = "Phạm Anh Dũng",
                Email = "dung.pham.sales@gmail.com",
                Phone = "0944567890",
                ExperienceYears = 1,
                CoverLetter = "Tôi vừa tốt nghiệp và có 1 năm kinh nghiệm sales B2B ngành vật liệu xây dựng. Giao tiếp tốt tiếng Anh (IELTS 7.0).",
                Status = "new",
                AppliedAt = now.AddDays(-1),
                UpdatedAt = now.AddDays(-1)
            },
            new()
            {
                JobPositionId = posIds[2].Id, // Nhân viên Kinh doanh
                CandidateName = "Võ Thị Mai",
                Email = "mai.vo@yahoo.com",
                Phone = "0955678901",
                ExperienceYears = 3,
                CoverLetter = "3 năm kinh doanh dự án xây dựng, đã ký được nhiều hợp đồng lớn tại khu vực miền Nam.",
                Status = "rejected",
                AppliedAt = now.AddDays(-20),
                UpdatedAt = now.AddDays(-18)
            },
            new()
            {
                JobPositionId = posIds[3].Id, // Kỹ sư MEP
                CandidateName = "Đặng Văn Hải",
                Email = "hai.dang.mep@gmail.com",
                Phone = "0966789012",
                ExperienceYears = 7,
                CoverLetter = "Senior MEP Engineer với 7 năm kinh nghiệm tại Samsung Engineering. Chuyên thiết kế hệ thống HVAC và PCCC cho nhà máy.",
                Status = "interview",
                AppliedAt = now.AddDays(-5),
                UpdatedAt = now.AddDays(-2)
            },
            new()
            {
                JobPositionId = posIds[4].Id, // Thực tập sinh
                CandidateName = "Hoàng Minh Khôi",
                Email = "khoi.hoang.sv@gmail.com",
                Phone = "0977890123",
                ExperienceYears = 0,
                CoverLetter = "Sinh viên năm cuối ĐH Xây dựng Hà Nội, muốn thực tập tại công trường thực tế để tích lũy kinh nghiệm trước khi ra trường.",
                Status = "new",
                AppliedAt = now.AddDays(-2),
                UpdatedAt = now.AddDays(-2)
            },
        };

        db.JobApplications.AddRange(applications);
        db.SaveChanges();
    }

    private static void SeedEmploymentTypes(AppDbContext db)
    {
        if (db.EmploymentTypes.Any()) return;

        var items = new EmploymentType[]
        {
            new() { Code = "full-time", Name = "Toàn thời gian", IsActive = true, SortOrder = 1 },
            new() { Code = "part-time", Name = "Bán thời gian", IsActive = true, SortOrder = 2 },
            new() { Code = "intern", Name = "Thực tập sinh", IsActive = true, SortOrder = 3 },
        };

        db.EmploymentTypes.AddRange(items);
        db.SaveChanges();
    }

    // ─── Contact Messages ───────────────────────────────────────────

    private static void SeedContactMessages(AppDbContext db)
    {
        if (db.ContactMessages.Any()) return;

        var now = DateTime.UtcNow;
        var items = new ContactMessage[]
        {
            new()
            {
                Name = "Nguyễn Minh Anh",
                Email = "minhanh.client@gmail.com",
                Phone = "0901122334",
                Subject = "Tư vấn thiết kế nhà xưởng 5.000m2",
                Message = "Chúng tôi cần tư vấn giải pháp tổng thầu thiết kế và thi công cho nhà xưởng sản xuất tại Bình Dương.",
                IsReplied = true,
                ReplyContent = "NICON đã tiếp nhận yêu cầu và sẽ liên hệ trong 24 giờ để khảo sát nhu cầu chi tiết.",
                RepliedAt = now.AddDays(-6),
                CreatedAt = now.AddDays(-7),
                UpdatedAt = now.AddDays(-6),
            },
            new()
            {
                Name = "Trần Quốc Huy",
                Email = "huy.tran@abc-industrial.vn",
                Phone = "0912345678",
                Subject = "Báo giá thi công MEP",
                Message = "Vui lòng gửi báo giá tham khảo cho hạng mục MEP nhà máy diện tích 12.000m2 tại Long An.",
                IsReplied = false,
                CreatedAt = now.AddDays(-4),
                UpdatedAt = now.AddDays(-4),
            },
            new()
            {
                Name = "Lê Thu Phương",
                Email = "phuong.le@thmcorp.vn",
                Phone = "0987654321",
                Subject = "Hợp tác dự án mới 2026",
                Message = "Công ty chúng tôi dự kiến triển khai dự án kho logistics và mong muốn trao đổi cơ hội hợp tác cùng NICON.",
                IsReplied = true,
                ReplyContent = "Cảm ơn chị Phương. Bộ phận kinh doanh đã đặt lịch họp sơ bộ vào tuần tới.",
                RepliedAt = now.AddDays(-2),
                CreatedAt = now.AddDays(-3),
                UpdatedAt = now.AddDays(-2),
            },
            new()
            {
                Name = "Phạm Gia Bảo",
                Email = "baopham@gmail.com",
                Phone = "0977000111",
                Subject = "Yêu cầu tư vấn cải tạo văn phòng",
                Message = "Tôi cần tư vấn cải tạo không gian văn phòng 800m2 theo phong cách hiện đại, tối ưu công năng.",
                IsReplied = false,
                CreatedAt = now.AddHours(-18),
                UpdatedAt = now.AddHours(-18),
            },
        };

        db.ContactMessages.AddRange(items);
        db.SaveChanges();
    }

    // ─── Entity Translations (en/zh/ja for Activities & News) ───────

    private static void SeedEntityTranslations(AppDbContext db)
    {
        var existingKeys = db.EntityTranslations
            .Select(t => t.EntityType + "|" + t.EntityId + "|" + t.FieldName + "|" + t.LanguageCode)
            .ToHashSet();

        var translations = new List<EntityTranslation>();
        var now = DateTime.UtcNow;

        void Add(string entityType, int entityId, string field, string lang, string value)
        {
            var key = entityType + "|" + entityId + "|" + field + "|" + lang;
            if (existingKeys.Contains(key)) return;
            translations.Add(new EntityTranslation
            {
                EntityType = entityType,
                EntityId = entityId,
                FieldName = field,
                LanguageCode = lang,
                Value = value,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        // Activity (1..16) and News (1..20) translations are now seeded
        // by SeedActivities()/SeedNews() directly from the manifests under
        // Data/Seeds/content/{activities,news}.json, so non-VI translations
        // stay in sync with real entity IDs after re-seeds.
        // Do not re-add hardcoded ID-based blocks here.

        // --- Slideshow items ---
        Add(EntityTypes.Slideshow, 1, "Title", "en", "General Contractor for Factory Design & Build");
        Add(EntityTypes.Slideshow, 1, "Subtitle", "en", "Over 18 years of experience — 150+ industrial projects");
        Add(EntityTypes.Slideshow, 1, "LinkText", "en", "View Projects");

        Add(EntityTypes.Slideshow, 2, "Title", "en", "Design & Build — Turnkey Solutions");
        Add(EntityTypes.Slideshow, 2, "Subtitle", "en", "One point of contact — full project lifecycle from design to handover");
        Add(EntityTypes.Slideshow, 2, "LinkText", "en", "Learn More");

        Add(EntityTypes.Slideshow, 3, "Title", "en", "Large-Scale Industrial Factories");
        Add(EntityTypes.Slideshow, 3, "Subtitle", "en", "250,000 m² complex — Japanese standards with Mori Group");
        Add(EntityTypes.Slideshow, 3, "LinkText", "en", "View Details");

        Add(EntityTypes.Slideshow, 4, "Title", "en", "Sports & Recreation Facilities");
        Add(EntityTypes.Slideshow, 4, "Subtitle", "en", "Multi-purpose sports facility design serving the community");
        Add(EntityTypes.Slideshow, 4, "LinkText", "en", "Explore");

        Add(EntityTypes.Slideshow, 5, "Title", "en", "Modern Office Interiors");
        Add(EntityTypes.Slideshow, 5, "Subtitle", "en", "Minimalist style — Open space — International standards");
        Add(EntityTypes.Slideshow, 5, "LinkText", "en", "View Project");

        // Project translations are now handled by SeedProjectTranslations() via project-translations.json (slug-based).

        // ─── Slideshow: ZH translations ───
        Add(EntityTypes.Slideshow, 1, "Title", "zh", "工厂设计施工总承包商");
        Add(EntityTypes.Slideshow, 1, "Subtitle", "zh", "18年以上经验 — 150+工业项目");
        Add(EntityTypes.Slideshow, 1, "LinkText", "zh", "查看项目");
        Add(EntityTypes.Slideshow, 2, "Title", "zh", "设计施工一体化 — 交钥匙方案");
        Add(EntityTypes.Slideshow, 2, "Subtitle", "zh", "单一联系窗口 — 从设计到移交的全项目周期");
        Add(EntityTypes.Slideshow, 2, "LinkText", "zh", "了解更多");
        Add(EntityTypes.Slideshow, 3, "Title", "zh", "大规模工业工厂");
        Add(EntityTypes.Slideshow, 3, "Subtitle", "zh", "250,000㎡综合体 — 与Mori集团合作的日本标准");
        Add(EntityTypes.Slideshow, 3, "LinkText", "zh", "查看详情");
        Add(EntityTypes.Slideshow, 4, "Title", "zh", "体育休闲设施");
        Add(EntityTypes.Slideshow, 4, "Subtitle", "zh", "服务社区的多功能体育设施设计");
        Add(EntityTypes.Slideshow, 4, "LinkText", "zh", "探索");
        Add(EntityTypes.Slideshow, 5, "Title", "zh", "现代办公室内装");
        Add(EntityTypes.Slideshow, 5, "Subtitle", "zh", "极简风格 — 开放空间 — 国际标准");
        Add(EntityTypes.Slideshow, 5, "LinkText", "zh", "查看项目");

        // ─── Slideshow: JA translations ───
        Add(EntityTypes.Slideshow, 1, "Title", "ja", "工場設計施工の総合請負");
        Add(EntityTypes.Slideshow, 1, "Subtitle", "ja", "18年以上の実績 — 150件超の産業プロジェクト");
        Add(EntityTypes.Slideshow, 1, "LinkText", "ja", "プロジェクトを見る");
        Add(EntityTypes.Slideshow, 2, "Title", "ja", "設計＆施工 — ターンキーソリューション");
        Add(EntityTypes.Slideshow, 2, "Subtitle", "ja", "単一窓口 — 設計から引渡しまでの全工程");
        Add(EntityTypes.Slideshow, 2, "LinkText", "ja", "詳しく見る");
        Add(EntityTypes.Slideshow, 3, "Title", "ja", "大規模産業工場");
        Add(EntityTypes.Slideshow, 3, "Subtitle", "ja", "250,000㎡の複合施設 — Moriグループとの日本基準");
        Add(EntityTypes.Slideshow, 3, "LinkText", "ja", "詳細を見る");
        Add(EntityTypes.Slideshow, 4, "Title", "ja", "スポーツ・レクリエーション施設");
        Add(EntityTypes.Slideshow, 4, "Subtitle", "ja", "地域に貢献する多目的スポーツ施設の設計");
        Add(EntityTypes.Slideshow, 4, "LinkText", "ja", "探索する");
        Add(EntityTypes.Slideshow, 5, "Title", "ja", "モダンオフィスインテリア");
        Add(EntityTypes.Slideshow, 5, "Subtitle", "ja", "ミニマリスト — オープンスペース — 国際基準");
        Add(EntityTypes.Slideshow, 5, "LinkText", "ja", "プロジェクトを見る");

        // --- Job Positions ---
        var jobPositions = db.JobPositions.Select(p => new { p.Id, p.Title }).ToList();
        int jobId(string title) => jobPositions.FirstOrDefault(p => p.Title == title)?.Id ?? 0;

        // Position 1: Kỹ sư Xây dựng (Site Engineer)
        var jpSiteEng = jobId("Kỹ sư Xây dựng (Site Engineer)");
        if (jpSiteEng > 0)
        {
            Add(EntityTypes.JobPosition, jpSiteEng, "Title", "en", "Site Engineer");
            Add(EntityTypes.JobPosition, jpSiteEng, "Title", "zh", "施工工程师");
            Add(EntityTypes.JobPosition, jpSiteEng, "Title", "ja", "現場エンジニア");
            Add(EntityTypes.JobPosition, jpSiteEng, "Department", "en", "Construction Dept.");
            Add(EntityTypes.JobPosition, jpSiteEng, "Department", "zh", "施工部");
            Add(EntityTypes.JobPosition, jpSiteEng, "Department", "ja", "施工部門");
            Add(EntityTypes.JobPosition, jpSiteEng, "Description", "en", "Directly supervise construction on-site, inspect material quality, and ensure schedule and occupational safety in accordance with ISO 9001:2015 standards.");
            Add(EntityTypes.JobPosition, jpSiteEng, "Description", "zh", "直接驻场监督施工，检验材料质量，按ISO 9001:2015标准确保施工进度与安全。");
            Add(EntityTypes.JobPosition, jpSiteEng, "Description", "ja", "現場で直接施工を監督し、資材品質を検査、ISO 9001:2015基準に基づき工程と労働安全を確保します。");
            Add(EntityTypes.JobPosition, jpSiteEng, "Requirements", "en", JsonSerializer.Serialize(new[] {
                "University degree in Civil & Industrial Construction Engineering",
                "At least 2 years' experience in workshop or factory construction",
                "Ability to read structural, architectural and MEP drawings",
                "Proficient in AutoCAD and MS Project",
                "Ability to work outdoors and handle schedule pressure"
            }));
            Add(EntityTypes.JobPosition, jpSiteEng, "Requirements", "zh", JsonSerializer.Serialize(new[] {
                "土木与工业建筑专业大学学历",
                "至少2年厂房/工厂施工经验",
                "能读懂结构、建筑及MEP图纸",
                "熟练使用AutoCAD、MS Project",
                "能在户外工作并承受进度压力"
            }));
            Add(EntityTypes.JobPosition, jpSiteEng, "Requirements", "ja", JsonSerializer.Serialize(new[] {
                "建築・工業建設工学専攻の大学卒業",
                "工場・倉庫建設2年以上の経験",
                "構造・建築・MEP図面の読解能力",
                "AutoCAD、MS Project習熟",
                "屋外作業・スケジュールプレッシャーへの対応能力"
            }));
        }

        // Position 2: Kiến trúc sư Thiết kế
        var jpArchitect = jobId("Kiến trúc sư Thiết kế");
        if (jpArchitect > 0)
        {
            Add(EntityTypes.JobPosition, jpArchitect, "Title", "en", "Architectural Designer");
            Add(EntityTypes.JobPosition, jpArchitect, "Title", "zh", "建筑设计师");
            Add(EntityTypes.JobPosition, jpArchitect, "Title", "ja", "建築デザイナー");
            Add(EntityTypes.JobPosition, jpArchitect, "Department", "en", "Design Dept.");
            Add(EntityTypes.JobPosition, jpArchitect, "Department", "zh", "设计部");
            Add(EntityTypes.JobPosition, jpArchitect, "Department", "ja", "設計部門");
            Add(EntityTypes.JobPosition, jpArchitect, "Description", "en", "Design architecture for industrial workshop, office and civil construction projects. Collaborate with structural and MEP teams to finalize design documents.");
            Add(EntityTypes.JobPosition, jpArchitect, "Description", "zh", "为工业厂房、办公及民用建筑项目进行建筑设计，与结构和MEP团队协作完善设计文件。");
            Add(EntityTypes.JobPosition, jpArchitect, "Description", "ja", "工業工場・オフィス・民間建築プロジェクトの建築設計を担当。構造・MEPチームと連携して設計書類を完成させます。");
            Add(EntityTypes.JobPosition, jpArchitect, "Requirements", "en", JsonSerializer.Serialize(new[] {
                "University degree in Architecture",
                "Proficient in Revit, AutoCAD, SketchUp, Photoshop",
                "Experience in industrial construction design is an advantage",
                "Creative thinking, up-to-date with new design trends",
                "Good communication skills with clients and internal teams"
            }));
            Add(EntityTypes.JobPosition, jpArchitect, "Requirements", "zh", JsonSerializer.Serialize(new[] {
                "建筑专业大学学历",
                "熟练使用Revit、AutoCAD、SketchUp、Photoshop",
                "有工业建筑设计经验者优先",
                "思维创新，紧跟新设计潮流",
                "与客户及内部团队沟通能力强"
            }));
            Add(EntityTypes.JobPosition, jpArchitect, "Requirements", "ja", JsonSerializer.Serialize(new[] {
                "建築専攻の大学卒業",
                "Revit、AutoCAD、SketchUp、Photoshop習熟",
                "工業建築設計経験は優遇",
                "革新的思考と最新デザイントレンドへの対応",
                "クライアント・社内チームとの良好なコミュニケーション能力"
            }));
        }

        // Position 3: Nhân viên Kinh doanh Dự án
        var jpSales = jobId("Nhân viên Kinh doanh Dự án");
        if (jpSales > 0)
        {
            Add(EntityTypes.JobPosition, jpSales, "Title", "en", "Project Sales Executive");
            Add(EntityTypes.JobPosition, jpSales, "Title", "zh", "项目销售专员");
            Add(EntityTypes.JobPosition, jpSales, "Title", "ja", "プロジェクト営業担当");
            Add(EntityTypes.JobPosition, jpSales, "Department", "en", "Sales Dept.");
            Add(EntityTypes.JobPosition, jpSales, "Department", "zh", "销售部");
            Add(EntityTypes.JobPosition, jpSales, "Department", "ja", "営業部門");
            Add(EntityTypes.JobPosition, jpSales, "Description", "en", "Find and develop corporate clients, advise on turnkey construction solutions, and follow up with clients from initial contact to contract signing.");
            Add(EntityTypes.JobPosition, jpSales, "Description", "zh", "开发企业客户，提供全包建设解决方案咨询，从初次接触到签约全程跟进维护客户关系。");
            Add(EntityTypes.JobPosition, jpSales, "Description", "ja", "法人クライアントの開拓・育成、ターンキー建設ソリューションの提案、初期接触から契約締結まで顧客フォローを担当します。");
            Add(EntityTypes.JobPosition, jpSales, "Requirements", "en", JsonSerializer.Serialize(new[] {
                "University degree in Business Administration, Marketing or Civil Engineering",
                "Excellent communication and presentation skills",
                "Own a motorbike and willing to travel",
                "B2B sales experience in the construction industry preferred",
                "English or Japanese language skills are a major advantage"
            }));
            Add(EntityTypes.JobPosition, jpSales, "Requirements", "zh", JsonSerializer.Serialize(new[] {
                "工商管理、市场营销或建筑专业大学学历",
                "出色的沟通与演示能力",
                "自备摩托车，可出差",
                "有建筑行业B2B销售经验优先",
                "英语或日语能力是很大优势"
            }));
            Add(EntityTypes.JobPosition, jpSales, "Requirements", "ja", JsonSerializer.Serialize(new[] {
                "経営学・マーケティング・建築専攻の大学卒業",
                "優れたコミュニケーション・プレゼンテーション能力",
                "バイク所持・出張可",
                "建設業界B2B営業経験者優遇",
                "英語または日本語能力は大きな強み"
            }));
        }

        // Position 4: Kỹ sư MEP
        var jpMep = jobId("Kỹ sư MEP (Cơ điện)");
        if (jpMep > 0)
        {
            Add(EntityTypes.JobPosition, jpMep, "Title", "en", "MEP Engineer");
            Add(EntityTypes.JobPosition, jpMep, "Title", "zh", "MEP工程师");
            Add(EntityTypes.JobPosition, jpMep, "Title", "ja", "MEPエンジニア");
            Add(EntityTypes.JobPosition, jpMep, "Department", "en", "Design Dept.");
            Add(EntityTypes.JobPosition, jpMep, "Department", "zh", "设计部");
            Add(EntityTypes.JobPosition, jpMep, "Department", "ja", "設計部門");
            Add(EntityTypes.JobPosition, jpMep, "Description", "en", "Design and supervise MEP systems (electrical, plumbing, HVAC, fire protection) for large-scale industrial workshop and office projects.");
            Add(EntityTypes.JobPosition, jpMep, "Description", "zh", "为大型工业厂房和办公项目设计和监督MEP系统（电气、给排水、HVAC、消防）。");
            Add(EntityTypes.JobPosition, jpMep, "Description", "ja", "大規模工業工場・オフィスプロジェクトのMEPシステム（電気・給排水・HVAC・消防）の設計・監督を担当します。");
            Add(EntityTypes.JobPosition, jpMep, "Requirements", "en", JsonSerializer.Serialize(new[] {
                "University degree in Electrical, Mechanical or Building Services Engineering",
                "Minimum 5 years' MEP design experience",
                "Proficient in Revit MEP and AutoCAD MEP",
                "Familiar with fire safety standards and QCVN for electrical and plumbing systems",
                "Experience in factory or industrial park projects is mandatory"
            }));
            Add(EntityTypes.JobPosition, jpMep, "Requirements", "zh", JsonSerializer.Serialize(new[] {
                "电气、机械或建筑设备专业大学学历",
                "至少5年MEP设计经验",
                "熟练使用Revit MEP和AutoCAD MEP",
                "熟悉消防标准及电气、给排水系统QCVN规范",
                "工厂/工业园区项目经验必须具备"
            }));
            Add(EntityTypes.JobPosition, jpMep, "Requirements", "ja", JsonSerializer.Serialize(new[] {
                "電気・機械・建築設備専攻の大学卒業",
                "MEP設計5年以上の経験",
                "Revit MEP、AutoCAD MEP習熟",
                "消防基準・電気/給排水QCVNへの精通",
                "工場/工業団地プロジェクト経験必須"
            }));
        }

        // Position 5: Thực tập sinh Kỹ thuật
        var jpIntern = jobId("Thực tập sinh Kỹ thuật");
        if (jpIntern > 0)
        {
            Add(EntityTypes.JobPosition, jpIntern, "Title", "en", "Engineering Intern");
            Add(EntityTypes.JobPosition, jpIntern, "Title", "zh", "技术实习生");
            Add(EntityTypes.JobPosition, jpIntern, "Title", "ja", "技術インターン");
            Add(EntityTypes.JobPosition, jpIntern, "Department", "en", "Construction Dept.");
            Add(EntityTypes.JobPosition, jpIntern, "Department", "zh", "施工部");
            Add(EntityTypes.JobPosition, jpIntern, "Department", "ja", "施工部門");
            Add(EntityTypes.JobPosition, jpIntern, "Description", "en", "Support the site engineering team with surveying, inspection and as-built documentation. Opportunity to learn hands-on at large-scale workshop construction sites.");
            Add(EntityTypes.JobPosition, jpIntern, "Description", "zh", "协助现场工程师团队进行测量、验收和竣工档案整理，在大型厂房建设工地获得实际学习机会。");
            Add(EntityTypes.JobPosition, jpIntern, "Description", "ja", "現場エンジニアチームの測量・検査・竣工図書作成をサポート。大規模工場建設現場での実践的な学習機会。");
            Add(EntityTypes.JobPosition, jpIntern, "Requirements", "en", JsonSerializer.Serialize(new[] {
                "Final-year student in Construction, Architecture or a related field",
                "Ability to intern full-time for at least 3 months",
                "Hardworking, eager to learn, willing to commute",
                "Basic AutoCAD skills"
            }));
            Add(EntityTypes.JobPosition, jpIntern, "Requirements", "zh", JsonSerializer.Serialize(new[] {
                "建筑、建筑学或相关专业大四学生",
                "可全职实习至少3个月",
                "勤奋、好学、能接受通勤",
                "基本AutoCAD操作能力"
            }));
            Add(EntityTypes.JobPosition, jpIntern, "Requirements", "ja", JsonSerializer.Serialize(new[] {
                "建築・建築学または関連分野の最終学年生",
                "最低3か月フルタイムインターン可能",
                "勤勉・学習意欲旺盛・通勤可能",
                "基本的なAutoCAD操作スキル"
            }));
        }

        db.EntityTranslations.AddRange(translations);
        db.SaveChanges();
    }

    // ─── Manifest-driven content seeding helpers ───────────────────

    private static List<ContentSeedItem> LoadContentSeed(string entityName)
    {
        // Each domain entity has its own JSON in Data/Seeds/content/. The folder
        // is reflected in the embedded-resource name as a dotted path.
        var resourceName = $"nihomebackend.Data.Seeds.content.{entityName}.json";
        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream(resourceName);
        if (stream is null) return [];
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<List<ContentSeedItem>>(stream, opts) ?? [];
    }

    private static void SeedManifestTranslations(AppDbContext db, string entityType, List<ContentSeedItem> manifest, IDictionary<string, int> bySlug)
    {
        var existingKeys = db.EntityTranslations
            .Where(t => t.EntityType == entityType)
            .Select(t => t.EntityId + "|" + t.FieldName + "|" + t.LanguageCode)
            .ToHashSet();

        var now = DateTime.UtcNow;
        var rows = new List<EntityTranslation>();

        void Add(int entityId, string field, string lang, string value)
        {
            var key = $"{entityId}|{field}|{lang}";
            if (existingKeys.Contains(key)) return; // never overwrite admin-edited translations
            rows.Add(new EntityTranslation { EntityType = entityType, EntityId = entityId, FieldName = field, LanguageCode = lang, Value = value, CreatedAt = now, UpdatedAt = now });
        }

        foreach (var item in manifest)
        {
            if (!bySlug.TryGetValue(item.Slug, out var entityId)) continue;
            foreach (var (lang, t) in item.Translations)
            {
                if (lang == "vi") continue; // VI lives on the entity itself
                if (!string.IsNullOrEmpty(t.Title)) Add(entityId, "Title", lang, t.Title);
                if (!string.IsNullOrEmpty(t.Excerpt)) Add(entityId, "Excerpt", lang, t.Excerpt);
                if (t.Content is { Count: > 0 }) Add(entityId, "Content", lang, JsonSerializer.Serialize(t.Content));
            }
        }
        if (rows.Count > 0)
        {
            db.EntityTranslations.AddRange(rows);
            db.SaveChanges();
        }
    }

    private sealed class ContentSeedItem
    {
        public string Slug { get; set; } = "";
        public string LegacySlug { get; set; } = "";
        public string ImageUrl { get; set; } = "";
        public List<string> Gallery { get; set; } = [];
        public string Date { get; set; } = "";
        public string Category { get; set; } = "";
        public int SortOrder { get; set; }
        public Dictionary<string, ContentSeedTranslation> Translations { get; set; } = new();

        public ContentSeedTranslation GetTranslation(string lang)
            => Translations.TryGetValue(lang, out var t) ? t : new ContentSeedTranslation();
    }

    private sealed class ContentSeedTranslation
    {
        public string Slug { get; set; } = "";
        public string Title { get; set; } = "";
        public string Excerpt { get; set; } = "";

        // Each entry is either a plain string (paragraph) or an object like
        // { "type": "image", "url": "/images/news/<slug>/img-03.jpg" } — keep
        // the raw JsonElement so dict items survive a Serialize round-trip.
        public List<JsonElement> Content { get; set; } = [];

        public string Date { get; set; } = "";
    }
}
