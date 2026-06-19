using System.Reflection;
using System.Text.Json;
using NihomeBackend.Constants;
using NihomeBackend.Models;

namespace NihomeBackend.Data;

public static class ContentSeeder
{
    public static void Seed(AppDbContext db)
    {
        SeedActivities(db);
        SeedNews(db);
        SeedProjects(db);
        SeedServices(db);
        SeedLogos(db);
        SeedProcesses(db);
        SeedSlideshow(db);
        SeedAboutSections(db);
        SeedRecruitment(db);
        SeedContactMessages(db);
        SeedEntityTranslations(db);
        LinkCategories(db);
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
            .Select(c => c.Name.ToLower())
            .ToHashSet();
        var nextOrder = (db.ActivityCategories.Max(c => (int?)c.SortOrder) ?? 0) + 1;
        foreach (var name in activityCategoryNames)
        {
            var trimmed = name.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;
            if (existingActivityNames.Contains(trimmed.ToLower())) continue;
            db.ActivityCategories.Add(new ActivityCategory { Name = trimmed, IsActive = true, SortOrder = nextOrder++ });
            existingActivityNames.Add(trimmed.ToLower());
        }

        // Ensure ProjectCategory rows exist for every distinct Project.Category
        var projectCategoryNames = db.Projects
            .Select(p => p.Category)
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct()
            .ToList();
        var existingProjectNames = db.ProjectCategories
            .Select(c => c.Name.ToLower())
            .ToHashSet();
        var nextProjectOrder = (db.ProjectCategories.Max(c => (int?)c.SortOrder) ?? 0) + 1;
        foreach (var name in projectCategoryNames)
        {
            var trimmed = (name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;
            if (existingProjectNames.Contains(trimmed.ToLower())) continue;
            db.ProjectCategories.Add(new ProjectCategory { Name = trimmed, IsActive = true, SortOrder = nextProjectOrder++ });
            existingProjectNames.Add(trimmed.ToLower());
        }
        db.SaveChanges();

        // Backfill FK on Activity rows
        var activityCategoryMap = db.ActivityCategories.ToDictionary(c => c.Name.ToLower(), c => c.Id);
        foreach (var activity in db.Activities.Where(a => a.ActivityCategoryId == null && a.Category != ""))
        {
            if (activityCategoryMap.TryGetValue(activity.Category.Trim().ToLower(), out var id))
            {
                activity.ActivityCategoryId = id;
            }
        }

        // Backfill FK on Project rows
        var projectCategoryMap = db.ProjectCategories.ToDictionary(c => c.Name.ToLower(), c => c.Id);
        foreach (var project in db.Projects.Where(p => p.ProjectCategoryId == null && p.Category != null && p.Category != ""))
        {
            if (projectCategoryMap.TryGetValue(project.Category!.Trim().ToLower(), out var id))
            {
                project.ProjectCategoryId = id;
            }
        }
        db.SaveChanges();
    }

    // ─── Activities (manifest-driven from legacy nicon.vn) ──────────

    private static void SeedActivities(AppDbContext db)
    {
        var manifest = LoadContentSeed("activities");
        if (manifest.Count == 0) return;

        if (NeedsContentReseed(db.Activities, manifest.Count, a => a.ImageUrl, IsLegacyStockActivityImage))
        {
            ReseedFromManifest(db, EntityTypes.Activity, manifest, item =>
            {
                var vi = item.GetTranslation("vi");
                return new Activity
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
                };
            });

            var bySlug = db.Activities.Select(a => new { a.Id, a.Slug }).ToList().ToDictionary(x => x.Slug, x => x.Id);
            SeedManifestTranslations(db, EntityTypes.Activity, manifest, bySlug);
        }
    }

    // ─── News (manifest-driven from legacy nicon.vn) ───────────────

    private static void SeedNews(AppDbContext db)
    {
        var manifest = LoadContentSeed("news");
        if (manifest.Count == 0) return;

        if (NeedsContentReseed(db.NewsArticles, manifest.Count, a => a.ImageUrl, IsLegacyStockNewsImage))
        {
            ReseedFromManifest(db, EntityTypes.News, manifest, item =>
            {
                var vi = item.GetTranslation("vi");
                return new NewsArticle
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
                };
            });

            var bySlug = db.NewsArticles.Select(n => new { n.Id, n.Slug }).ToList().ToDictionary(x => x.Slug, x => x.Id);
            SeedManifestTranslations(db, EntityTypes.News, manifest, bySlug);
        }
    }

    // ─── Projects ───────────────────────────────────────────────────

    private static void SeedProjects(AppDbContext db)
    {
        var existingSlugs = db.Projects.Select(p => p.Slug).ToHashSet();

        var items = new Project[]
        {
            new() { Slug = "nha-may-bma", ImageUrl = "/images/projects/nha-may-bma/01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/project-bma.jpg", "/images/projects/nha-may-bma/01.jpg" }), Name = "Nhà Máy BMA", Client = "Bảo Minh Ân Việt Nam", Location = "KCN Hựu Thạnh, Tây Ninh", Scale = "15.000 m²", Scope = "Thiết kế và Thi công", Status = "ongoing", Year = "2024", Category = "Nhà máy công nghiệp", Description = "Dự án Nhà Máy BMA là tổ hợp sản xuất hiện đại với quy mô 15.000 m², được thiết kế theo tiêu chuẩn công nghiệp quốc tế.", ChallengesJson = JsonSerializer.Serialize(new[] { "Yêu cầu tiến độ chặt chẽ trong vòng 10 tháng từ khởi công đến vận hành.", "Giải pháp kết cấu nhà xưởng nhịp lớn không cột giữa cho dây chuyền sản xuất.", "Tối ưu hệ thống thông gió và chiếu sáng tự nhiên để tiết kiệm năng lượng." }), SolutionsJson = JsonSerializer.Serialize(new[] { "Áp dụng kết cấu thép tiền chế nhịp 30m với mái lấy sáng polycarbonate.", "Thi công song song nhiều hạng mục, quản lý tiến độ bằng phần mềm BIM 4D.", "Hệ thống M&E đồng bộ, dự phòng công suất cho mở rộng tương lai 30%." }), HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "15.000 m²" }, new { label = "Thời gian", value = "10 tháng" }, new { label = "Nhịp kết cấu", value = "30 m" }, new { label = "Tiêu chuẩn", value = "ISO 9001" } }), SortOrder = 0 },
            new() { Slug = "nha-xuong-nbdc", ImageUrl = "/images/projects/nha-xuong-nbdc/01.png", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/project-nbdc.jpg", "/images/projects/ttdtt-thu-duc/02.png" }), Name = "Nhà Xưởng NBDC", Client = "Công ty TNHH NBDC VN", Location = "KCN Giang Điền, Đồng Nai", Scale = "8.500 m²", Scope = "Thiết kế", Status = "ongoing", Year = "2024", Category = "Nhà xưởng sản xuất", Description = "NICON cung cấp dịch vụ thiết kế kiến trúc và kết cấu trọn gói cho nhà xưởng sản xuất NBDC tại KCN Giang Điền.", ChallengesJson = JsonSerializer.Serialize(new[] { "Bố cục dây chuyền sản xuất phức tạp với nhiều khu vực chức năng.", "Yêu cầu tích hợp khu văn phòng điều hành và sản xuất trong cùng một khối." }), SolutionsJson = JsonSerializer.Serialize(new[] { "Phân khu rõ ràng với luồng di chuyển một chiều, giảm chéo nhau.", "Thiết kế khu văn phòng 2 tầng tích hợp với view nhìn xuống xưởng." }), HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "8.500 m²" }, new { label = "Khu chức năng", value = "5" }, new { label = "Nhân sự dự kiến", value = "200" }, new { label = "Năm", value = "2024" } }), SortOrder = 1 },
            new() { Slug = "nha-may-lhh", ImageUrl = "/images/projects/nha-may-lhh/01.png", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/nha-may-lhh/02.png", "/images/projects/nha-may-lhh/03.png" }), Name = "Nhà Máy Lâm Hiệp Hưng – Tân Toàn Phát", Client = "Lam Hiệp Hưng & Tân Toàn Phát", Location = "Tỉnh Bình Dương", Scale = "250.000 m²", Scope = "Thiết kế", Status = "ongoing", Year = "2023", Category = "Tổ hợp công nghiệp", Description = "Một trong những dự án quy mô lớn nhất NICON đã thực hiện: tổ hợp nhà máy 250.000 m².", ChallengesJson = JsonSerializer.Serialize(new[] { "Quy hoạch tổng mặt bằng quy mô siêu lớn với nhiều khối công trình.", "Đồng bộ hạ tầng kỹ thuật trên diện tích lớn." }), SolutionsJson = JsonSerializer.Serialize(new[] { "Quy hoạch theo mô-đun, dễ dàng mở rộng và thay đổi công năng.", "Hệ thống đường nội bộ thiết kế cho xe container 40 feet." }), HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Tổng diện tích", value = "250.000 m²" }, new { label = "Khối công trình", value = "12" }, new { label = "Đường nội bộ", value = "5,2 km" }, new { label = "Năm", value = "2023" } }), SortOrder = 2 },
            new() { Slug = "ttdtt-thu-duc", ImageUrl = "/images/projects/ttdtt-thu-duc/01.png", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/ttdtt-thu-duc/02.png", "/images/projects/ttdtt-thu-duc/03.png", "/images/projects/ttdtt-thu-duc/04.png" }), Name = "Trung Tâm Thể Dục Thể Thao Thủ Đức", Client = "Thủ Thiêm Group", Location = "Thủ Đức, TP.HCM", Scale = "12.000 m²", Scope = "Thiết kế", Status = "ongoing", Year = "2024", Category = "Công trình công cộng", Description = "Trung tâm thể dục thể thao đa năng phục vụ cộng đồng tại Thủ Đức.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "12.000 m²" }, new { label = "Nhịp mái", value = "45 m" }, new { label = "Sức chứa", value = "2.000 chỗ" }, new { label = "Năm", value = "2024" } }), SortOrder = 3 },
            new() { Slug = "noi-that-b37", ImageUrl = "/images/projects/noi-that-b37/01.png", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/noi-that-b37/02.jpg", "/images/projects/noi-that-b37/03.jpg", "/images/projects/noi-that-b37/04.png" }), Name = "Văn Phòng B37", Client = "Nihome", Location = "Thủ Đức, TP.HCM", Scale = "1.200 m²", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2024", Category = "Nội thất văn phòng", Description = "Thiết kế nội thất văn phòng hiện đại với phong cách tối giản, không gian mở.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "1.200 m²" }, new { label = "Sức chứa", value = "80 người" }, new { label = "Phòng họp", value = "6" }, new { label = "Năm", value = "2024" } }), SortOrder = 4 },
            new() { Slug = "nha-may-trimas", ImageUrl = "/images/projects/nha-may-trimas/01.png", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/nha-may-trimas/02.png", "/images/projects/nha-may-trimas/03.png", "/images/projects/nha-may-trimas/04.png" }), Name = "Nhà Máy Trimas Việt Nam", Client = "Rieke Packaging Vietnam Co., Ltd", Location = "VSIP IIA, TP.HCM", Scale = "10.000 m²", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2022", Category = "Nhà máy công nghiệp", Description = "Dự án trọn gói thiết kế và thi công nhà máy sản xuất bao bì cho Trimas tại VSIP IIA.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "10.000 m²" }, new { label = "Tiêu chuẩn", value = "ISO Class 8" }, new { label = "Thời gian", value = "9 tháng" }, new { label = "Năm", value = "2022" } }), SortOrder = 5 },
            new() { Slug = "nha-kho-apm", ImageUrl = "/images/projects/nha-kho-apm/01.png", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/nha-kho-apm/02.png", "/images/projects/nha-kho-apm/03.png", "/images/projects/nha-kho-apm/04.png" }), Name = "Nhà Kho APM", Client = "Auto Components Việt Nam", Location = "KCN Việt Nam – Singapore, Bình Hòa, Thuận An, Bình Dương", Scale = "6.500 m²", Scope = "Thiết kế", Status = "completed", Year = "2022", Category = "Nhà kho logistics", Description = "Thiết kế nhà kho logistics cho Auto Components Việt Nam.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "6.500 m²" }, new { label = "Chiều cao", value = "12 m" }, new { label = "Dock loading", value = "6" }, new { label = "Năm", value = "2022" } }), SortOrder = 6 },
            new() { Slug = "nha-may-jojo", ImageUrl = "/images/projects/nha-may-jojo/01.png", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/nha-may-jojo/02.png", "/images/projects/nha-may-jojo/03.png" }), Name = "Nhà Máy JOJO", Client = "Phạm – Asset", Location = "KCN Hựu Thạnh, Long An", Scale = "7.800 m²", Scope = "Thiết kế", Status = "ongoing", Year = "2024", Category = "Nhà máy công nghiệp", Description = "Thiết kế nhà máy sản xuất JOJO với yêu cầu cao về vệ sinh an toàn thực phẩm.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "7.800 m²" }, new { label = "Tiêu chuẩn", value = "HACCP" }, new { label = "Khu sạch", value = "3" }, new { label = "Năm", value = "2024" } }), SortOrder = 7 },
            new() { Slug = "khach-san-d22", ImageUrl = "/images/projects/khach-san-d22/01.png", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/khach-san-d22/02.png", "/images/projects/khach-san-d22/03.png" }), Name = "Khách sạn D22", Client = "Nihome", Location = "Thủ Đức, TP.HCM", Scale = "4.500 m²", Scope = "Thiết kế và Thi công", Status = "ongoing", Year = "2024", Category = "Khách sạn", Description = "Khách sạn 4 sao với 80 phòng nghỉ, nhà hàng tầng trệt và khu spa.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "4.500 m²" }, new { label = "Số phòng", value = "80" }, new { label = "Tầng cao", value = "12" }, new { label = "Năm", value = "2024" } }), SortOrder = 8 },
            // ── Ongoing projects from nicon.vn ──
            new() { Slug = "nbdc-canteen", ImageUrl = "/images/projects/nbdc-canteen/01.png", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/nbdc-canteen/02.png", "/images/projects/nbdc-canteen/03.png" }), Name = "NBDC Canteen", Client = "Công ty TNHH NBDC VN", Location = "KCN Giang Điền, Đồng Nai", Scale = "", Scope = "Thiết kế", Status = "ongoing", Year = "2024", Category = "Nhà xưởng sản xuất", Description = "Thiết kế nhà ăn cho khu công nghiệp NBDC tại KCN Giang Điền.", SortOrder = 9 },
            new() { Slug = "nbdc-office", ImageUrl = "/images/projects/nbdc-office/01.png", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/nbdc-office/02.png", "/images/projects/nbdc-office/03.png" }), Name = "NBDC Office", Client = "Công ty TNHH NBDC VN", Location = "KCN Giang Điền, Đồng Nai", Scale = "", Scope = "Thiết kế", Status = "ongoing", Year = "2024", Category = "Văn phòng", Description = "Thiết kế văn phòng điều hành cho NBDC tại KCN Giang Điền.", SortOrder = 10 },
            new() { Slug = "nha-may-ttp", ImageUrl = "/images/projects/nha-may-ttp/01.png", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/nha-may-ttp/02.png", "/images/projects/nha-may-ttp/03.png" }), Name = "Nhà Máy Tân Toàn Phát", Client = "Lam Hiệp Hưng & Tân Toàn Phát", Location = "Tỉnh Bình Dương", Scale = "250.000 m²", Scope = "Thiết kế", Status = "ongoing", Year = "2024", Category = "Nhà máy công nghiệp", Description = "Nhà máy thuộc tổ hợp Lâm Hiệp Hưng – Tân Toàn Phát tại Bình Dương.", SortOrder = 11 },
            new() { Slug = "nha-may-lhh-2", ImageUrl = "/images/projects/nha-may-lhh-2/01.png", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/nha-may-lhh-2/02.png", "/images/projects/nha-may-lhh-2/03.png" }), Name = "Nhà Máy Lâm Hiệp Hưng", Client = "Lam Hiệp Hưng", Location = "Tỉnh Bình Dương", Scale = "250.000 m²", Scope = "Thiết kế", Status = "ongoing", Year = "2024", Category = "Nhà máy công nghiệp", Description = "Nhà máy Lâm Hiệp Hưng tại Bình Dương, mở rộng dây chuyền sản xuất.", SortOrder = 12 },
            // ── Completed projects from nicon.vn ──
            new() { Slug = "nha-may-stfood", ImageUrl = "/images/projects/nha-may-stfood/01.png", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/nha-may-stfood/02.png", "/images/projects/nha-may-stfood/03.png", "/images/projects/nha-may-stfood/04.png" }), Name = "Nhà Máy S.T.Food Marketing Việt Nam", Client = "S.T.FOOD MARKETING Vietnam Co. Ltd.", Location = "Đường 24, VSIP II-A, Tân Uyên, Bình Dương", Scale = "20.000 m²", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2022", Category = "Nhà máy công nghiệp", Description = "Nhà máy sản xuất thực phẩm từ chủ đầu tư Thái Lan, thiết kế theo tiêu chuẩn GMP và HACCP.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "20.000 m²" }, new { label = "Tiêu chuẩn", value = "GMP/HACCP" }, new { label = "Thời gian", value = "14 tháng" }, new { label = "Năm", value = "2022" } }), SortOrder = 13 },
            new() { Slug = "medicare-shop", ImageUrl = "/images/projects/medicare-shop/01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/medicare-shop/02.jpg", "/images/projects/medicare-shop/03.jpg", "/images/projects/medicare-shop/04.jpg" }), Name = "Medicare Shop", Client = "Medicare Company", Location = "G3, Aeon Mall Bình Dương Canary", Scale = "250 m²", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2020", Category = "Thương mại", Description = "Cửa hàng Medicare tại Aeon Mall Bình Dương Canary.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "250 m²" }, new { label = "Vị trí", value = "Aeon Mall" }, new { label = "Năm", value = "2020" } }), SortOrder = 14 },
            new() { Slug = "nha-may-lhh-completed", ImageUrl = "/images/projects/nha-may-lhh-completed/01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/nha-may-lhh-completed/02.jpg", "/images/projects/nha-may-lhh-completed/03.jpg", "/images/projects/nha-may-lhh-completed/04.jpg" }), Name = "Nhà Máy Lâm Hiệp Hưng (Hoàn thành)", Client = "Lam Hiệp Hưng & Tân Toàn Phát", Location = "Tỉnh Bình Dương", Scale = "18.000 m²", Scope = "Thiết kế", Status = "completed", Year = "2019", Category = "Nhà máy công nghiệp", Description = "Dự án nhà máy Lâm Hiệp Hưng giai đoạn 1 đã hoàn thành.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "18.000 m²" }, new { label = "Năm", value = "2019" } }), SortOrder = 15 },
            new() { Slug = "sctv-office", ImageUrl = "/images/projects/sctv-office/01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/sctv-office/02.jpg" }), Name = "Văn Phòng SCTV", Client = "Đài Truyền hình Cáp Sài Gòn", Location = "Quận 2, TP.HCM", Scale = "4.000 m²", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2020", Category = "Văn phòng", Description = "Thiết kế và thi công văn phòng SCTV tại Quận 2, TP.HCM.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "4.000 m²" }, new { label = "Năm", value = "2020" } }), SortOrder = 16 },
            new() { Slug = "nha-may-hbfuller", ImageUrl = "/images/projects/nha-may-hbfuller/01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/nha-may-hbfuller/02.jpg", "/images/projects/nha-may-hbfuller/03.jpg" }), Name = "Nhà Máy H.B.Fuller", Client = "H.B.Fuller Co., Ltd.", Location = "Tỉnh Bình Dương", Scale = "", Scope = "MEP", Status = "completed", Year = "2019", Category = "Nhà máy công nghiệp", Description = "Thi công hệ thống MEP cho nhà máy H.B.Fuller tại Bình Dương.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Phạm vi", value = "MEP" }, new { label = "Năm", value = "2019" } }), SortOrder = 17 },
            new() { Slug = "red-bull-expansion", ImageUrl = "/images/projects/red-bull-expansion/01.png", Name = "Dự Án Mở Rộng Red Bull", Client = "Red Bull (Việt Nam) Co., Ltd", Location = "Xa lộ Hà Nội, Bình Thắng, Dĩ An, Bình Dương", Scale = "2.000 m²", Scope = "Thiết kế", Status = "completed", Year = "2024", Category = "Nhà máy công nghiệp", Description = "Thiết kế mở rộng nhà máy Red Bull tại Bình Dương, giữ nguyên bản sắc thương hiệu.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "2.000 m²" }, new { label = "Năm", value = "2024" } }), SortOrder = 18 },
            new() { Slug = "nha-may-great-lotus", ImageUrl = "/images/projects/nha-may-great-lotus/01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/nha-may-great-lotus/02.jpg", "/images/projects/nha-may-great-lotus/03.jpg", "/images/projects/nha-may-great-lotus/04.jpg" }), Name = "Nhà Máy Great Lotus Việt Nam", Client = "Great Lotus Manufacturing Vietnam Co. Ltd.", Location = "VSIP II-A, Tân Uyên, Bình Dương", Scale = "31.187 m²", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2019", Category = "Nhà máy công nghiệp", Description = "Thiết kế và thi công nhà máy Great Lotus với quy mô hơn 31.000 m².", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "31.187 m²" }, new { label = "Năm", value = "2019" } }), SortOrder = 19 },
            new() { Slug = "nha-may-advanced-casting", ImageUrl = "/images/projects/nha-may-advanced-casting/01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/nha-may-advanced-casting/02.jpg", "/images/projects/nha-may-advanced-casting/03.jpg" }), Name = "Nhà Máy Advanced Casting Asia", Client = "Advanced Casting Asia Co. Ltd.", Location = "VSIP II-A, Tân Uyên, Bình Dương", Scale = "15.000 m²", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2018", Category = "Nhà máy công nghiệp", Description = "Nhà máy sản xuất Advanced Casting Asia tại VSIP II-A.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "15.000 m²" }, new { label = "Năm", value = "2018" } }), SortOrder = 20 },
            new() { Slug = "sctv-studio", ImageUrl = "/images/projects/sctv-studio/01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/sctv-studio/02.jpg", "/images/projects/sctv-studio/03.jpg", "/images/projects/sctv-studio/04.jpg" }), Name = "SCTV Studio & Văn Phòng", Client = "Đài Truyền hình Cáp Sài Gòn", Location = "Quận 2, TP.HCM", Scale = "", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2018", Category = "Studio", Description = "Trường quay truyền hình quy mô lớn nhất Việt Nam tại thời điểm xây dựng.", SortOrder = 21 },
            new() { Slug = "nha-may-bkl", ImageUrl = "/images/projects/nha-may-bkl/01.jpg", Name = "Nhà Máy BKL", Client = "BKL International Ltd., Co", Location = "KCN Thịnh Phát, Bến Lức, Long An", Scale = "5.000 m²", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2018", Category = "Nhà máy công nghiệp", Description = "Thiết kế và thi công nhà máy BKL tại KCN Thịnh Phát.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "5.000 m²" }, new { label = "Năm", value = "2018" } }), SortOrder = 22 },
            new() { Slug = "nha-may-rebisco", ImageUrl = "/images/projects/nha-may-rebisco/01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/nha-may-rebisco/02.jpg", "/images/projects/nha-may-rebisco/03.jpg", "/images/projects/nha-may-rebisco/04.jpg" }), Name = "Nhà Máy Rebisco", Client = "Republic Biscuit Corporation", Location = "VSIP II-A, Tân Uyên, Bình Dương", Scale = "20.000 m²", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2017", Category = "Nhà máy công nghiệp", Description = "Nhà máy sản xuất bánh kẹo Rebisco (Philippines) tại VSIP II-A.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "20.000 m²" }, new { label = "Năm", value = "2017" } }), SortOrder = 23 },
            new() { Slug = "nha-may-nestle", ImageUrl = "/images/projects/nha-may-nestle/01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/nha-may-nestle/02.jpg", "/images/projects/nha-may-nestle/03.jpg", "/images/projects/nha-may-nestle/04.jpg" }), Name = "Nhà Máy & Văn Phòng Nestlé Bình An", Client = "Nestlé Việt Nam", Location = "KCN Biên Hòa II, Đồng Nai", Scale = "", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2015", Category = "Nhà máy công nghiệp", Description = "Thiết kế và thi công nhà máy và văn phòng Nestlé Bình An.", SortOrder = 24 },
            new() { Slug = "nha-may-ampharco", ImageUrl = "/images/projects/nha-may-ampharco/01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/nha-may-ampharco/02.jpg" }), Name = "Nhà Máy Ampharco U.S.A", Client = "Ampharco U.S.A", Location = "KCN Nhơn Trạch 3, Đồng Nai", Scale = "", Scope = "Thi công", Status = "completed", Year = "2016", Category = "Nhà máy dược phẩm", Description = "Thi công nhà máy dược phẩm Ampharco U.S.A tại Nhơn Trạch.", SortOrder = 25 },
            new() { Slug = "konimiyaki-restaurant", ImageUrl = "/images/projects/konimiyaki-restaurant/01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/konimiyaki-restaurant/02.jpg", "/images/projects/konimiyaki-restaurant/03.jpg", "/images/projects/konimiyaki-restaurant/04.jpg" }), Name = "Nhà Hàng Konimiyaki", Client = "Konimiyaki Restaurant", Location = "Quận 1, TP.HCM", Scale = "400 m²", Scope = "Thiết kế", Status = "completed", Year = "2018", Category = "Nhà hàng", Description = "Thiết kế nhà hàng Konimiyaki tại Quận 1, TP.HCM.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "400 m²" }, new { label = "Năm", value = "2018" } }), SortOrder = 26 },
            new() { Slug = "nha-may-scon", ImageUrl = "/images/projects/nha-may-scon/01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/nha-may-scon/02.jpg", "/images/projects/nha-may-scon/03.jpg", "/images/projects/nha-may-scon/04.jpg" }), Name = "Nhà Máy SCON", Client = "SCON Co. Ltd.", Location = "VSIP II-A, Tân Uyên, Bình Dương", Scale = "8.337 m²", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2018", Category = "Nhà máy công nghiệp", Description = "Thiết kế và thi công nhà máy SCON tại VSIP II-A.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "8.337 m²" }, new { label = "Năm", value = "2018" } }), SortOrder = 27 },
            new() { Slug = "nha-may-clotex", ImageUrl = "/images/projects/nha-may-clotex/01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/nha-may-clotex/02.jpg" }), Name = "Nhà Máy Clotex Labels Việt Nam", Client = "Clotex Labels (VN) Co. Ltd.", Location = "VSIP II-A, Tân Uyên, Bình Dương", Scale = "8.565 m²", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2018", Category = "Nhà máy công nghiệp", Description = "Nhà máy nhãn mác Clotex Labels Vietnam tại VSIP II-A.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "8.565 m²" }, new { label = "Năm", value = "2018" } }), SortOrder = 28 },
            new() { Slug = "nha-may-amiba", ImageUrl = "/images/projects/nha-may-amiba/01.jpg", Name = "Nhà Máy Amiba", Client = "Amiba Vietnam Company Limited", Location = "VSIP II-A, Tân Uyên, Bình Dương", Scale = "20.000 m²", Scope = "Thi công", Status = "completed", Year = "2018", Category = "Nhà máy công nghiệp", Description = "Thi công nhà máy Amiba với diện tích 2 hecta tại VSIP II-A.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "20.000 m²" }, new { label = "Năm", value = "2018" } }), SortOrder = 29 },
            new() { Slug = "nha-may-akati-wood", ImageUrl = "/images/projects/nha-may-akati-wood/01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/nha-may-akati-wood/02.jpg", "/images/projects/nha-may-akati-wood/03.jpg", "/images/projects/nha-may-akati-wood/04.jpg" }), Name = "Nhà Máy Akati Wood", Client = "Akati Dominant (Malaysia)", Location = "Bình Dương", Scale = "", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2016", Category = "Nhà máy công nghiệp", Description = "Nhà máy gỗ Akati Wood, chi nhánh của Akati Dominant từ Malaysia.", SortOrder = 30 },
            new() { Slug = "nha-may-japan-plus", ImageUrl = "/images/projects/nha-may-japan-plus/01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/nha-may-japan-plus/02.jpg", "/images/projects/nha-may-japan-plus/03.jpg", "/images/projects/nha-may-japan-plus/04.jpg" }), Name = "Nhà Máy Japan Plus", Client = "Japan Plus (Nhật Bản)", Location = "KCN Đông Nam Củ Chi", Scale = "", Scope = "Thi công", Status = "completed", Year = "2016", Category = "Nhà máy công nghiệp", Description = "Nhà máy Japan Plus sản xuất hộp PE tại KCN Đông Nam Củ Chi.", SortOrder = 31 },
            new() { Slug = "duoc-pham-trung-uong", ImageUrl = "/images/projects/duoc-pham-trung-uong/01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/duoc-pham-trung-uong/02.jpg", "/images/projects/duoc-pham-trung-uong/03.jpg", "/images/projects/duoc-pham-trung-uong/04.jpg" }), Name = "Dược Phẩm Trung Ương TP.HCM", Client = "Công ty TNHH Dược Phẩm Trung Ương 1", Location = "TP.HCM", Scale = "", Scope = "Thiết kế", Status = "completed", Year = "2020", Category = "Nhà máy dược phẩm", Description = "Thiết kế nhà máy dược phẩm Trung Ương tại TP.HCM.", SortOrder = 32 },
            new() { Slug = "kumgang-office", ImageUrl = "/images/projects/kumgang-office/01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/kumgang-office/02.jpg" }), Name = "Văn Phòng Kumgang", Client = "KUMGANG VINA CO., LTD", Location = "KCN Giang Điền, Trảng Bom, Đồng Nai", Scale = "180 m²", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2018", Category = "Văn phòng", Description = "Thiết kế và thi công văn phòng Kumgang Vina.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "180 m²" }, new { label = "Năm", value = "2018" } }), SortOrder = 33 },
            new() { Slug = "nha-may-vda-hcm", ImageUrl = "/images/projects/nha-may-vda-hcm/01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/nha-may-vda-hcm/02.jpg", "/images/projects/nha-may-vda-hcm/03.jpg", "/images/projects/nha-may-vda-hcm/04.jpg" }), Name = "Nhà Máy VDA-HCM", Client = "VDA-HCM", Location = "KCN Cầu Tràm, Cần Đước, Long An", Scale = "", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2017", Category = "Nhà máy công nghiệp", Description = "Thiết kế và thi công nhà máy VDA-HCM tại KCN Cầu Tràm.", SortOrder = 34 },
            new() { Slug = "thu-thiem-dragon", ImageUrl = "/images/projects/thu-thiem-dragon/01.jpeg", Name = "Thu Thiêm Dragon Show Flat", Client = "Thu Thiêm Group", Location = "Quận 2, TP.HCM", Scale = "", Scope = "Thi công", Status = "completed", Year = "2015", Category = "Bất động sản", Description = "Thi công căn hộ mẫu Thu Thiêm Dragon tại Quận 2.", SortOrder = 35 },
            new() { Slug = "nha-may-nam-ha-viet", ImageUrl = "/images/projects/nha-may-nam-ha-viet/01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/nha-may-nam-ha-viet/02.jpg" }), Name = "Nhà Máy Nam Hà Việt", Client = "Nam Hà Việt Co., Ltd.", Location = "KCN Rạch Bắp, Bến Cát, Bình Dương", Scale = "", Scope = "Thiết kế", Status = "completed", Year = "2020", Category = "Nhà máy công nghiệp", Description = "Nhà máy sản xuất que hàn Nam Hà Việt.", SortOrder = 36 },
            new() { Slug = "nha-may-yc-tec", ImageUrl = "/images/projects/nha-may-yc-tec/01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/nha-may-yc-tec/02.jpg" }), Name = "Nhà Máy YC TEC", Client = "YC TEC Group", Location = "KCN Sóng Thần II, Dĩ An, Bình Dương", Scale = "", Scope = "Thiết kế", Status = "completed", Year = "2020", Category = "Nhà máy công nghiệp", Description = "Thiết kế nhà máy YC TEC tại KCN Sóng Thần II.", SortOrder = 37 },
            // ── Additional ongoing projects from nicon.vn ──
            new() { Slug = "d56-house", ImageUrl = "/images/projects/d56-house/01.png", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/d56-house/01.png", "/images/projects/d56-house/02.png" }), Name = "D56 House", Client = "Nihome Co., Ltd.", Location = "Thủ Đức, TP.HCM", Scale = "", Scope = "Thiết kế và Thi công", Status = "ongoing", Year = "2024", Category = "Nhà ở", Description = "Thiết kế và thi công nhà ở D56 tại Thủ Đức cho Nihome.", SortOrder = 38 },
            new() { Slug = "swimming-pool-service-building", ImageUrl = "/images/projects/swimming-pool-service-building/01.png", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/swimming-pool-service-building/01.png", "/images/projects/swimming-pool-service-building/02.png", "/images/projects/swimming-pool-service-building/03.png", "/images/projects/swimming-pool-service-building/04.png", "/images/projects/swimming-pool-service-building/05.png", "/images/projects/swimming-pool-service-building/06.png", "/images/projects/swimming-pool-service-building/07.png" }), Name = "Swimming Pool Service Building", Client = "Thu Thiem Group Joint Stock Company", Location = "Thạnh Mỹ Lợi, Thủ Đức, TP.HCM", Scale = "", Scope = "Thiết kế", Status = "ongoing", Year = "2024", Category = "Công trình công cộng", Description = "Thiết kế khu dịch vụ hồ bơi thuộc tổ hợp Thủ Đức Sport Center.", SortOrder = 39 },
            new() { Slug = "thu-duc-multi-purpose-building", ImageUrl = "/images/projects/thu-duc-multi-purpose-building/01.png", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/thu-duc-multi-purpose-building/01.png", "/images/projects/thu-duc-multi-purpose-building/02.png", "/images/projects/thu-duc-multi-purpose-building/03.png", "/images/projects/thu-duc-multi-purpose-building/04.png", "/images/projects/thu-duc-multi-purpose-building/05.png", "/images/projects/thu-duc-multi-purpose-building/06.png", "/images/projects/thu-duc-multi-purpose-building/07.png", "/images/projects/thu-duc-multi-purpose-building/08.png" }), Name = "Thủ Đức Multi-Purpose Building", Client = "Thu Thiem Group Joint Stock Company", Location = "Thạnh Mỹ Lợi, Thủ Đức, TP.HCM", Scale = "", Scope = "Thiết kế", Status = "ongoing", Year = "2024", Category = "Công trình công cộng", Description = "Thiết kế công trình đa năng thuộc khu liên hợp Thủ Đức.", SortOrder = 40 },
            new() { Slug = "thu-duc-wedding-banquet", ImageUrl = "/images/projects/thu-duc-wedding-banquet/01.png", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/thu-duc-wedding-banquet/01.png", "/images/projects/thu-duc-wedding-banquet/02.png", "/images/projects/thu-duc-wedding-banquet/03.png", "/images/projects/thu-duc-wedding-banquet/04.png", "/images/projects/thu-duc-wedding-banquet/05.png", "/images/projects/thu-duc-wedding-banquet/06.png", "/images/projects/thu-duc-wedding-banquet/07.png", "/images/projects/thu-duc-wedding-banquet/08.png" }), Name = "Thủ Đức Wedding Banquet Restaurant", Client = "Thu Thiem Group Joint Stock Company", Location = "Thạnh Mỹ Lợi, Thủ Đức, TP.HCM", Scale = "", Scope = "Thiết kế", Status = "ongoing", Year = "2024", Category = "Nhà hàng", Description = "Thiết kế nhà hàng tiệc cưới thuộc tổ hợp Thủ Đức Sport Center.", SortOrder = 41 },
            new() { Slug = "thu-duc-sport-service-building", ImageUrl = "/images/projects/thu-duc-sport-service-building/01.png", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/thu-duc-sport-service-building/01.png", "/images/projects/thu-duc-sport-service-building/02.png", "/images/projects/thu-duc-sport-service-building/03.png", "/images/projects/thu-duc-sport-service-building/04.png", "/images/projects/thu-duc-sport-service-building/05.png", "/images/projects/thu-duc-sport-service-building/06.png", "/images/projects/thu-duc-sport-service-building/07.png", "/images/projects/thu-duc-sport-service-building/08.png", "/images/projects/thu-duc-sport-service-building/09.png", "/images/projects/thu-duc-sport-service-building/10.png", "/images/projects/thu-duc-sport-service-building/11.png", "/images/projects/thu-duc-sport-service-building/12.png" }), Name = "Service Building - Thủ Đức Sport Center", Client = "Thu Thiem Group Joint Stock Company", Location = "Thạnh Mỹ Lợi, Thủ Đức, TP.HCM", Scale = "", Scope = "Thiết kế", Status = "ongoing", Year = "2024", Category = "Công trình công cộng", Description = "Khu dịch vụ thuộc trung tâm thể thao Thủ Đức.", SortOrder = 42 },
            new() { Slug = "thu-duc-sport-coffee", ImageUrl = "/images/projects/thu-duc-sport-coffee/01.png", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/thu-duc-sport-coffee/01.png", "/images/projects/thu-duc-sport-coffee/02.png", "/images/projects/thu-duc-sport-coffee/03.png" }), Name = "Coffee - Thủ Đức Sport Center", Client = "Thu Thiem Group Joint Stock Company", Location = "Thạnh Mỹ Lợi, Thủ Đức, TP.HCM", Scale = "", Scope = "Thiết kế", Status = "ongoing", Year = "2024", Category = "Thương mại", Description = "Thiết kế quán cà phê thuộc trung tâm thể thao Thủ Đức.", SortOrder = 43 },
            new() { Slug = "b37-interior", ImageUrl = "/images/projects/b37-interior/01.png", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/b37-interior/01.png", "/images/projects/b37-interior/02.png", "/images/projects/b37-interior/03.png", "/images/projects/b37-interior/04.png", "/images/projects/b37-interior/05.png", "/images/projects/b37-interior/06.png", "/images/projects/b37-interior/07.png", "/images/projects/b37-interior/08.png", "/images/projects/b37-interior/09.png", "/images/projects/b37-interior/10.png", "/images/projects/b37-interior/11.png", "/images/projects/b37-interior/12.png", "/images/projects/b37-interior/13.png", "/images/projects/b37-interior/14.png", "/images/projects/b37-interior/15.png", "/images/projects/b37-interior/16.png", "/images/projects/b37-interior/17.png", "/images/projects/b37-interior/18.png", "/images/projects/b37-interior/19.png", "/images/projects/b37-interior/20.png", "/images/projects/b37-interior/21.png", "/images/projects/b37-interior/22.png", "/images/projects/b37-interior/23.png", "/images/projects/b37-interior/24.png", "/images/projects/b37-interior/25.png", "/images/projects/b37-interior/26.png" }), Name = "Interior - Văn Phòng B37", Client = "Nihome Co., Ltd.", Location = "Thủ Đức, TP.HCM", Scale = "", Scope = "Thiết kế", Status = "ongoing", Year = "2024", Category = "Nội thất văn phòng", Description = "Thiết kế nội thất văn phòng B37 cho Nihome.", SortOrder = 44 },
            new() { Slug = "d22-factory", ImageUrl = "/images/projects/d22-factory/01.png", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/d22-factory/01.png", "/images/projects/d22-factory/02.png", "/images/projects/d22-factory/03.png", "/images/projects/d22-factory/04.png", "/images/projects/d22-factory/05.png" }), Name = "Nhà Máy D22", Client = "NATCO Vietnam Co., Ltd.", Location = "Thuận An, Bình Dương", Scale = "", Scope = "Thiết kế", Status = "ongoing", Year = "2024", Category = "Nhà máy công nghiệp", Description = "Thiết kế nhà máy D22 cho NATCO Vietnam tại Thuận An.", SortOrder = 45 },
            new() { Slug = "salad-stop-restaurant", ImageUrl = "/images/projects/salad-stop-restaurant/01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/salad-stop-restaurant/01.jpg", "/images/projects/salad-stop-restaurant/02.jpg", "/images/projects/salad-stop-restaurant/03.jpg", "/images/projects/salad-stop-restaurant/04.jpg", "/images/projects/salad-stop-restaurant/05.jpg", "/images/projects/salad-stop-restaurant/06.jpg", "/images/projects/salad-stop-restaurant/07.jpg" }), Name = "Nhà Hàng Salad Stop", Client = "Salad Stop Company", Location = "TP.HCM", Scale = "", Scope = "Thiết kế", Status = "ongoing", Year = "2024", Category = "Nhà hàng", Description = "Thiết kế chuỗi nhà hàng Salad Stop.", SortOrder = 46 },
            new() { Slug = "champion-lee-factory", ImageUrl = "/images/projects/champion-lee-factory/01.png", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/champion-lee-factory/01.png", "/images/projects/champion-lee-factory/02.png", "/images/projects/champion-lee-factory/03.png", "/images/projects/champion-lee-factory/04.png" }), Name = "Nhà Máy Champion Lee Group", Client = "Champion Lee Group", Location = "N-8A, Đường Số 4, KCN Long Hậu", Scale = "", Scope = "Thiết kế", Status = "ongoing", Year = "2024", Category = "Nhà máy công nghiệp", Description = "Thiết kế nhà máy Champion Lee Group tại KCN Long Hậu.", SortOrder = 47 },
            new() { Slug = "jakob-workshop", ImageUrl = "/images/projects/jakob-workshop/01.png", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/jakob-workshop/01.png", "/images/projects/jakob-workshop/02.png", "/images/projects/jakob-workshop/03.png", "/images/projects/jakob-workshop/04.png", "/images/projects/jakob-workshop/05.png", "/images/projects/jakob-workshop/06.png", "/images/projects/jakob-workshop/07.png" }), Name = "Xưởng Jakob (Đề Xuất)", Client = "Jakob", Location = "Bình Dương", Scale = "", Scope = "Thiết kế", Status = "ongoing", Year = "2024", Category = "Nhà máy công nghiệp", Description = "Thiết kế đề xuất xưởng Jakob tại Bình Dương.", SortOrder = 48 },
            new() { Slug = "siegwerk-factory", ImageUrl = "/images/projects/siegwerk-factory/01.png", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/siegwerk-factory/01.png", "/images/projects/siegwerk-factory/02.png", "/images/projects/siegwerk-factory/03.png", "/images/projects/siegwerk-factory/04.png", "/images/projects/siegwerk-factory/05.png", "/images/projects/siegwerk-factory/06.png", "/images/projects/siegwerk-factory/07.png" }), Name = "Nhà Máy Siegwerk Việt Nam", Client = "Siegwerk", Location = "VSIP IIA, Bình Dương", Scale = "", Scope = "Thiết kế", Status = "ongoing", Year = "2024", Category = "Nhà máy công nghiệp", Description = "Thiết kế nhà máy Siegwerk tại VSIP IIA.", SortOrder = 49 },
            new() { Slug = "great-lotus-interior", ImageUrl = "/images/projects/great-lotus-interior/01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/great-lotus-interior/01.jpg", "/images/projects/great-lotus-interior/02.jpg", "/images/projects/great-lotus-interior/03.jpg", "/images/projects/great-lotus-interior/04.jpg", "/images/projects/great-lotus-interior/05.jpg", "/images/projects/great-lotus-interior/06.jpg", "/images/projects/great-lotus-interior/07.jpg", "/images/projects/great-lotus-interior/08.jpg", "/images/projects/great-lotus-interior/09.jpg", "/images/projects/great-lotus-interior/10.jpg", "/images/projects/great-lotus-interior/11.jpg", "/images/projects/great-lotus-interior/12.jpg" }), Name = "Nội Thất Nhà Máy Great Lotus", Client = "Great Lotus Manufacturing Vietnam Co. Ltd.", Location = "Lot 3, Đường 24, VSIP II-A, Bình Dương", Scale = "31.187 m²", Scope = "Turnkey", Status = "ongoing", Year = "2024", Category = "Nội thất công nghiệp", Description = "Hạng mục nội thất trọn gói (turnkey) cho nhà máy Great Lotus.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "31.187 m²" }, new { label = "Phạm vi", value = "Turnkey" } }), SortOrder = 50 },
            new() { Slug = "quan-chi-factory", ImageUrl = "/images/projects/quan-chi-factory/01.png", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/quan-chi-factory/01.png", "/images/projects/quan-chi-factory/02.png", "/images/projects/quan-chi-factory/03.png", "/images/projects/quan-chi-factory/04.png", "/images/projects/quan-chi-factory/05.png" }), Name = "Nhà Máy Quan Chi", Client = "Quan Chi Co., Ltd.", Location = "VSIP IIA, Bình Dương", Scale = "", Scope = "Thiết kế", Status = "ongoing", Year = "2024", Category = "Nhà máy công nghiệp", Description = "Thiết kế nhà máy Quan Chi tại VSIP IIA.", SortOrder = 51 },
            new() { Slug = "velrco-office", ImageUrl = "/images/projects/velrco-office/01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/velrco-office/01.jpg", "/images/projects/velrco-office/02.jpg", "/images/projects/velrco-office/03.jpg", "/images/projects/velrco-office/04.jpg", "/images/projects/velrco-office/05.jpg" }), Name = "Văn Phòng Velrco", Client = "Velrco Company", Location = "", Scale = "", Scope = "Thiết kế", Status = "ongoing", Year = "2024", Category = "Văn phòng", Description = "Thiết kế văn phòng Velrco.", SortOrder = 52 },
            new() { Slug = "semivina-nissi-factory", ImageUrl = "/images/projects/semivina-nissi-factory/01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/semivina-nissi-factory/01.jpg", "/images/projects/semivina-nissi-factory/02.jpg", "/images/projects/semivina-nissi-factory/03.jpg", "/images/projects/semivina-nissi-factory/04.jpg", "/images/projects/semivina-nissi-factory/05.jpg" }), Name = "Nhà Máy Semivina – Nissi", Client = "Semivina – Nissi (Hàn Quốc)", Location = "VSIP II, Bình Dương", Scale = "", Scope = "Thiết kế và Thi công", Status = "ongoing", Year = "2024", Category = "Nhà máy công nghiệp", Description = "Nhà máy thiết bị chiếu sáng của nhà đầu tư Hàn Quốc tại VSIP II.", SortOrder = 53 },
            // ── Additional legacy projects (NIH-23 sync) ──
            new() { Slug = "nha-may-tien-len", ImageUrl = "/images/projects/nha-may-tien-len/img-01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/nha-may-tien-len/img-02.jpg", "/images/projects/nha-may-tien-len/img-03.jpg", "/images/projects/nha-may-tien-len/img-04.jpg" }), Name = "Nhà Máy Tiến Lên", Client = "", Location = "", Scale = "", Scope = "Thiết kế", Status = "completed", Year = "", Category = "Nhà máy công nghiệp", Description = "Khách hàng: Công ty Cổ phần Tiến Lên", SortOrder = 54 },
            new() { Slug = "stc-canteen-coffee", ImageUrl = "/images/projects/quan-ca-phe-stc/img-02.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/quan-ca-phe-stc/img-03.jpg", "/images/projects/quan-ca-phe-stc/img-01.jpg", "/images/projects/quan-ca-phe-stc/img-04.jpg", "/images/projects/quan-ca-phe-stc/img-05.jpg", "/images/projects/quan-ca-phe-stc/img-07.jpg", "/images/projects/quan-ca-phe-stc/img-08.jpg", "/images/projects/quan-ca-phe-stc/img-09.jpg", "/images/projects/quan-ca-phe-stc/img-11.jpg", "/images/projects/stc-canteen-coffee-4/img-10.jpg", "/images/projects/quan-ca-phe-stc/img-10.jpg" }), Name = "STC Canteen – Coffee", Client = "", Location = "", Scale = "", Scope = "Thiết kế", Status = "completed", Year = "", Category = "Nhà hàng", Description = "Khách hàng: Công ty Phương Nam Star", SortOrder = 55 },
            new() { Slug = "nha-hang-wrap-roll", ImageUrl = "/images/projects/nha-hang-wrap-roll/img-01.JPG", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/nha-hang-wrap-roll/img-02.JPG", "/images/projects/nha-hang-wrap-roll/img-03.JPG", "/images/projects/nha-hang-wrap-roll/img-04.JPG", "/images/projects/nha-hang-wrap-roll/img-05.JPG", "/images/projects/nha-hang-wrap-roll/img-06.JPG" }), Name = "Nhà Hàng Wrap & Roll", Client = "", Location = "", Scale = "", Scope = "Thiết kế", Status = "completed", Year = "", Category = "Nhà hàng", Description = "Khách hàng: Công ty cổ phần nhà hàng gói & cuốn", SortOrder = 56 },
            new() { Slug = "nha-o-gia-dinh", ImageUrl = "/images/projects/nha-o-gia-dinh/img-01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/nha-o-gia-dinh/img-02.jpg", "/images/projects/nha-o-gia-dinh/img-03.jpg", "/images/projects/nha-o-gia-dinh/img-04.jpg", "/images/projects/nha-o-gia-dinh/img-06.jpg", "/images/projects/nha-o-gia-dinh/img-08.jpg", "/images/projects/nha-o-gia-dinh/img-10.jpg", "/images/projects/nha-o-gia-dinh/img-11.jpg", "/images/projects/nha-o-gia-dinh/img-12.jpg", "/images/projects/nha-o-gia-dinh/img-13.jpg", "/images/projects/nha-o-gia-dinh/img-14.jpg", "/images/projects/nha-o-gia-dinh/img-15.jpg", "/images/projects/nha-o-gia-dinh/img-16.jpg", "/images/projects/nha-o-gia-dinh/img-17.jpg", "/images/projects/nha-o-gia-dinh/img-18.jpg", "/images/projects/nha-o-gia-dinh/img-19.jpg", "/images/projects/nha-o-gia-dinh/img-20.jpg", "/images/projects/nha-o-gia-dinh/img-22.jpg", "/images/projects/nha-o-gia-dinh/img-23.jpg", "/images/projects/nha-o-gia-dinh/img-24.jpg", "/images/projects/nha-o-gia-dinh/img-25.jpg", "/images/projects/nha-o-gia-dinh/img-26.jpg", "/images/projects/nha-o-gia-dinh/img-27.jpg", "/images/projects/nha-o-gia-dinh/img-28.jpg", "/images/projects/nha-o-gia-dinh/img-29.jpg", "/images/projects/nha-o-gia-dinh/img-30.jpg", "/images/projects/nha-o-gia-dinh/img-31.jpg", "/images/projects/nha-o-gia-dinh/img-32.jpg", "/images/projects/nha-o-gia-dinh/img-33.jpg", "/images/projects/nha-o-gia-dinh/img-34.jpg", "/images/projects/nha-o-gia-dinh/img-35.jpg" }), Name = "Nhà Ở Gia Đình", Client = "", Location = "", Scale = "", Scope = "Thiết kế", Status = "completed", Year = "", Category = "Nhà ở", Description = null, SortOrder = 57 },
            new() { Slug = "nha-may-tan-thanh-long", ImageUrl = "/images/projects/nha-may-tan-thanh-long-4/img-01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/nha-may-tan-thanh-long-4/img-02.jpg", "/images/projects/nha-may-tan-thanh-long-4/img-03.jpg", "/images/projects/nha-may-tan-thanh-long-4/img-04.jpg" }), Name = "Nhà Máy Tân Thành Long", Client = "", Location = "", Scale = "", Scope = "Thiết kế", Status = "completed", Year = "", Category = "Nhà máy công nghiệp", Description = "Khách hàng: Công ty TNHH MTV Thiết Kế In Bao Bì Tân Thành Long", SortOrder = 58 },
            new() { Slug = "ray-river-resort", ImageUrl = "/images/projects/ray-river-resort-2/img-01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/ray-river-resort-2/img-02.jpg", "/images/projects/ray-river-resort-2/img-03.jpg", "/images/projects/ray-river-resort-2/img-04.jpg" }), Name = "Resort Sông Ray", Client = "", Location = "", Scale = "", Scope = "Thiết kế", Status = "completed", Year = "", Category = "Khách sạn", Description = null, SortOrder = 59 },
            new() { Slug = "nha-hang-okonomiyaki", ImageUrl = "/images/projects/konimiyaki-restaurant/01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/konimiyaki-restaurant/02.jpg", "/images/projects/konimiyaki-restaurant/03.jpg", "/images/projects/konimiyaki-restaurant/04.jpg", "/images/projects/nha-hang-konimiyaki/img-05.jpg" }), Name = "Nhà Hàng Okonomiyaki", Client = "", Location = "", Scale = "", Scope = "Thiết kế", Status = "completed", Year = "", Category = "Nhà hàng", Description = "Khách hàng: Nhà hàng Okonomiyaki", SortOrder = 60 },
            new() { Slug = "nha-may-bmt", ImageUrl = "/images/projects/nha-may-bmt/img-01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/nha-may-bmt/img-02.jpg", "/images/projects/nha-may-bmt/img-03.jpg", "/images/projects/nha-may-bmt/img-04.jpg", "/images/projects/nha-may-bmt/img-05.jpg" }), Name = "Nhà Máy BMT", Client = "", Location = "", Scale = "", Scope = "Thiết kế", Status = "completed", Year = "", Category = "Nhà máy công nghiệp", Description = "Địa điểm: số 51, 52 Nhựt Chánh Khu công nghiệp-Bến Lức tỉnh Long An", SortOrder = 61 },
            new() { Slug = "nha-hang-hokkaido", ImageUrl = "/images/projects/nha-hang-hokkaido/img-01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/nha-hang-hokkaido/img-02.jpg" }), Name = "Nhà Hàng Hokkaido", Client = "", Location = "", Scale = "", Scope = "Thiết kế", Status = "completed", Year = "", Category = "Nhà hàng", Description = null, SortOrder = 62 },
            new() { Slug = "nha-may-inahvina", ImageUrl = "/images/projects/nha-may-cong-ty-inahvina/img-01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/nha-may-cong-ty-inahvina/img-02.jpg", "/images/projects/nha-may-cong-ty-inahvina/img-03.jpg", "/images/projects/nha-may-cong-ty-inahvina/img-05.jpg" }), Name = "Nhà Máy Inahvina", Client = "", Location = "", Scale = "", Scope = "Thiết kế", Status = "completed", Year = "", Category = "Nhà máy công nghiệp", Description = "Khách hàng: Nhà máy Công ty TNHH INAHVINA", SortOrder = 63 },
            new() { Slug = "nha-may-dong-nhan", ImageUrl = "/images/projects/nha-may-dong-nhan/img-01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/nha-may-dong-nhan/img-02.jpg", "/images/projects/nha-may-dong-nhan/img-03.jpg" }), Name = "Nhà Máy Đông Nhân", Client = "", Location = "", Scale = "", Scope = "Thiết kế", Status = "completed", Year = "", Category = "Nhà máy công nghiệp", Description = "Khách hàng: Công ty TNHH Đồng Nhân", SortOrder = 64 },
            new() { Slug = "khach-san-toan-vinh", ImageUrl = "/images/projects/khach-san-toan-vinh/img-01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/khach-san-toan-vinh/img-02.jpg", "/images/projects/khach-san-toan-vinh/img-03.jpg" }), Name = "Khách Sạn Toàn Vinh", Client = "", Location = "", Scale = "", Scope = "Thiết kế", Status = "completed", Year = "", Category = "Khách sạn", Description = null, SortOrder = 65 },
            new() { Slug = "khach-san-y-linh", ImageUrl = "/images/projects/khach-san-y-linh/img-01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/khach-san-y-linh/img-02.jpg" }), Name = "Khách Sạn Y Linh", Client = "", Location = "", Scale = "", Scope = "Thiết kế", Status = "completed", Year = "", Category = "Khách sạn", Description = null, SortOrder = 66 },
            new() { Slug = "ky-tuc-xa-soul-gear", ImageUrl = "/images/projects/mo-rong-nha-may-red-bull-2/thumb.png", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/ky-tuc-xa-soul-gear/img-01.jpg" }), Name = "Ký Túc Xá Soul Gear", Client = "", Location = "", Scale = "", Scope = "Thiết kế", Status = "completed", Year = "", Category = "Nhà ở", Description = "Khách hàng: Công ty Soul Gear VINA (Hàn Quốc)", SortOrder = 67 },
            new() { Slug = "nha-may-js", ImageUrl = "/images/projects/nha-may-js/img-01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/nha-may-js/img-02.jpg", "/images/projects/nha-may-js/img-03.jpg", "/images/projects/nha-may-js/img-04.jpg" }), Name = "Nhà Máy JS", Client = "", Location = "", Scale = "", Scope = "Thiết kế", Status = "completed", Year = "", Category = "Nhà máy công nghiệp", Description = "Địa điểm: Mỹ Phước 2 Khu công nghiệp, Tỉnh Bình Dương", SortOrder = 68 },
            new() { Slug = "truong-quoc-te-acg", ImageUrl = "/images/projects/truong-quoc-te-acg/img-01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/truong-quoc-te-acg/img-02.jpg", "/images/projects/truong-quoc-te-acg/img-03.jpg", "/images/projects/truong-quoc-te-acg/img-04.jpg" }), Name = "Trường Quốc Tế ACG", Client = "", Location = "", Scale = "", Scope = "Thiết kế", Status = "completed", Year = "", Category = "Giáo dục", Description = "Địa điểm: Quận 2, TP Hồ Chí Minh", SortOrder = 69 },
            new() { Slug = "khach-san-eden", ImageUrl = "/images/projects/khach-san-eden/img-01.jpg", Name = "Khách Sạn Eden", Client = "", Location = "", Scale = "", Scope = "Thiết kế", Status = "completed", Year = "", Category = "Khách sạn", Description = "Địa điểm: 60 Mai Xuân Thưởng, Thành phố Quy Nhơn", SortOrder = 70 },
            new() { Slug = "nha-may-dream-mekong", ImageUrl = "/images/projects/nha-may-dream-mekong/img-01.jpg", Name = "Nhà Máy Dream Mekong", Client = "", Location = "", Scale = "", Scope = "Thiết kế", Status = "completed", Year = "", Category = "Nhà máy công nghiệp", Description = "Khách hàng: Dream Mekong Viet nam (Hàn Quốc)", SortOrder = 71 },
            new() { Slug = "long-khanh-hotel-resort", ImageUrl = "/images/projects/long-khanh-hotel-resort/img-01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/long-khanh-hotel-resort/img-02.jpg" }), Name = "Long Khánh Hotel & Resort", Client = "", Location = "", Scale = "", Scope = "Thiết kế", Status = "completed", Year = "", Category = "Khách sạn", Description = "Khách hàng: Long Khánh Hotel & Resort", SortOrder = 72 },
            new() { Slug = "suzuki-showroom", ImageUrl = "/images/projects/suzuki-showroom/img-01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/suzuki-showroom/img-02.jpg", "/images/projects/suzuki-showroom/img-03.jpg", "/images/projects/suzuki-showroom/img-04.jpg" }), Name = "Suzuki Showroom", Client = "", Location = "", Scale = "", Scope = "Thiết kế", Status = "completed", Year = "", Category = "Thương mại", Description = "Địa điểm: Quận 5, TP Hồ Chí Minh", SortOrder = 73 },
            new() { Slug = "kv-battery", ImageUrl = "/images/projects/kv-battery-2/img-01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/kv-battery-2/img-02.jpg" }), Name = "K&V Battery", Client = "", Location = "", Scale = "", Scope = "Thiết kế", Status = "completed", Year = "", Category = "Nhà máy công nghiệp", Description = "Khách hàng: K & V Battery Co., LTD (Hàn Quốc)", SortOrder = 74 },

        };

        var newItems = items.Where(p => !existingSlugs.Contains(p.Slug)).ToArray();
        if (newItems.Length == 0) return;

        db.Projects.AddRange(newItems);
        db.SaveChanges();
    }

    // ─── Services ───────────────────────────────────────────────────

    private static void SeedServices(AppDbContext db)
    {
        if (db.ServiceItems.Any()) return;

        var items = new ServiceItem[]
        {
            new() { Slug = "design-and-build", ShortTitle = "Design & Build", Title = "Tổng thầu Thiết kế và Thi công (D&B)", Tagline = "Một đầu mối — toàn bộ vòng đời dự án.", Intro = "Phương thức Design & Build (D&B) và EPC là hai phương pháp phổ biến nhất trong xây dựng công nghiệp và dân dụng. NICON đã hệ thống hoá quy trình D&B từ ngày đầu thành lập và liên tục hoàn thiện qua hơn 150 dự án.", SectionsJson = JsonSerializer.Serialize(new[] { new { heading = "Lợi thế của phương thức D&B / EPC", body = new[] { "Tối thiểu hóa nghĩa vụ quản lý cho chủ đầu tư — NICON đảm nhận toàn bộ điều phối và quản lý dự án.", "Giảm thiểu rủi ro không nhất quán giữa thiết kế và thi công.", "Linh hoạt đẩy nhanh tiến độ ngay cả khi thiết kế chưa hoàn chỉnh, giảm chi phí phát sinh.", "Chi phí quản lý hợp lý — chủ đầu tư dễ ước lượng và kiểm soát chất lượng do chỉ làm việc với một nhà thầu." } }, new { heading = "Phương pháp quản lý dự án tiên tiến", body = new[] { "Hợp tác chặt chẽ cùng Mori Construction (Nhật Bản), NICON ứng dụng quy trình BIM (Building Information Modeling) cho mọi giai đoạn.", "Đội ngũ chuyên viên BIM giàu kinh nghiệm cung cấp giải pháp đồng bộ, giúp chủ đầu tư có toàn bộ thông tin dự án và dự đoán rủi ro sớm." } }, new { heading = "Sản phẩm tốt nhất từ những con người tốt nhất", body = new[] { "NICON sở hữu mạng lưới đối tác quản lý quốc tế trong các lĩnh vực kiến trúc, kết cấu, nội thất và M&E.", "Đội ngũ giàu kinh nghiệm gồm project manager, kiến trúc sư, kỹ sư và công nhân lành nghề có thể xử lý các dự án QUY MÔ LỚN – TIẾN ĐỘ GẤP – CHẤT LƯỢNG CAO." } } }), HighlightsJson = JsonSerializer.Serialize(new[] { "BIM 4D / 5D", "ISO 9001:2015", "150+ dự án D&B", "Mori Group partner" }), SortOrder = 0 },
            new() { Slug = "main-contractor", ShortTitle = "Main Contractor", Title = "Dịch vụ Tổng thầu chính (Main Contractor)", Tagline = "Quản lý trọn gói thi công — bàn giao chìa khóa trao tay.", Intro = "Với vai trò Tổng thầu chính Việt – Nhật, NICON thực hiện đầy đủ các nhiệm vụ của một dự án xây dựng công nghiệp.", SectionsJson = JsonSerializer.Serialize(new[] { new { heading = "Phạm vi công việc của Tổng thầu chính", body = new[] { "Quản lý toàn bộ công trường, điều phối các nhà thầu phụ và nhà cung cấp.", "Đảm bảo tiến độ, chất lượng và an toàn lao động (HSE) tại công trường.", "Báo cáo định kỳ cho chủ đầu tư bằng tiếng Việt – Anh – Nhật." } }, new { heading = "Phương pháp quản lý chuẩn quốc tế", body = new[] { "Áp dụng tiêu chuẩn quản lý dự án PMP và phương pháp Lean Construction.", "Sử dụng phần mềm MS Project, Primavera P6 cho lập tiến độ và kiểm soát chi phí.", "Quy trình QA/QC theo ISO 9001:2015 cho từng hạng mục thi công." } }, new { heading = "Đối tác chiến lược cùng Mori Group", body = new[] { "Sự hợp tác cùng Mori Industry Group (Nhật Bản) mang đến tiêu chuẩn kỹ thuật và văn hóa làm việc chuẩn Nhật cho mọi dự án NICON đảm nhận." } } }), HighlightsJson = JsonSerializer.Serialize(new[] { "18+ năm kinh nghiệm", "Quản lý PMP", "QA/QC ISO 9001", "An toàn HSE chuẩn Nhật" }), SortOrder = 1 },
            new() { Slug = "general-contractor", ShortTitle = "General Contractor", Title = "Dịch vụ Tổng thầu (General Contractor)", Tagline = "Đảm nhận toàn bộ vòng đời thi công nhà máy công nghiệp.", Intro = "Với cương vị Tổng thầu Việt Nam – Nhật Bản, NICON thực hiện đầy đủ nhiệm vụ của một dự án xây dựng công nghiệp gồm thiết kế, xin phép, thi công và bàn giao trọn gói.", SectionsJson = JsonSerializer.Serialize(new[] { new { heading = "Vai trò Tổng thầu", body = new[] { "Quản lý toàn diện từ thiết kế cơ sở, thiết kế kỹ thuật đến bản vẽ thi công.", "Mua sắm vật tư – thiết bị (Procurement) và quản lý chuỗi cung ứng cho dự án.", "Tổ chức thi công, nghiệm thu từng phần và bàn giao công trình hoàn chỉnh." } }, new { heading = "Năng lực mega-project", body = new[] { "NICON đã thành công thực hiện các tổ hợp công nghiệp 250.000 m² như Lâm Hiệp Hưng – Tân Toàn Phát.", "Năng lực tổ chức công trường lớn với hàng trăm công nhân, thiết bị nặng và logistics phức tạp." } }, new { heading = "Cam kết chất lượng", body = new[] { "100% công trình bàn giao đúng tiến độ trong 5 năm gần nhất.", "Bảo hành 24 tháng cho phần xây dựng, 12 tháng cho phần MEP." } } }), HighlightsJson = JsonSerializer.Serialize(new[] { "Mega-project 250.000m²", "Procurement chuyên nghiệp", "Bảo hành 24 tháng", "Đa quốc gia" }), SortOrder = 2 },
            new() { Slug = "mep-contractor", ShortTitle = "MEP Contractor", Title = "Dịch vụ Tổng thầu MEP", Tagline = "Hệ thống Cơ – Điện – Nước đồng bộ và tối ưu vận hành.", Intro = "MEP (Mechanical – Electrical – Plumbing) là phần quan trọng quyết định hiệu quả vận hành nhà máy. NICON cung cấp dịch vụ tổng thầu MEP độc lập hoặc tích hợp trong gói D&B, với đội ngũ kỹ sư chuyên ngành giàu kinh nghiệm.", SectionsJson = JsonSerializer.Serialize(new[] { new { heading = "Phạm vi MEP của NICON", body = new[] { "Hệ thống điện công nghiệp: trung – hạ thế, máy phát dự phòng, UPS, hệ chiếu sáng năng lượng cao.", "Hệ HVAC, thông gió và phòng sạch theo cấp ISO Class 5/7/8.", "Hệ cấp – thoát nước, nước nóng năng lượng mặt trời, hệ xử lý nước thải.", "Hệ PCCC sprinkler, báo cháy địa chỉ theo TCVN và NFPA." } }, new { heading = "Tích hợp và bàn giao", body = new[] { "Quy trình T&C (Testing & Commissioning) bài bản, có sự chứng kiến của tư vấn giám sát và chủ đầu tư.", "Bàn giao kèm hồ sơ As-built, sách hướng dẫn vận hành – bảo trì (O&M Manual).", "Đào tạo vận hành cho đội ngũ kỹ thuật của chủ đầu tư." } }, new { heading = "Quản lý dự án bằng BIM", body = new[] { "Mô hình MEP 3D phát hiện xung đột hạng mục trước khi thi công, giảm 80% chỉnh sửa hiện trường.", "Tài liệu BIM bàn giao cho chủ đầu tư phục vụ vận hành – bảo trì lâu dài." } } }), HighlightsJson = JsonSerializer.Serialize(new[] { "BIM MEP 3D", "Phòng sạch ISO 5-8", "T&C chuyên nghiệp", "O&M training" }), SortOrder = 3 },
        };

        db.ServiceItems.AddRange(items);
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
        if (db.SlideshowItems.Any()) return;

        var items = new SlideshowItem[]
        {
            new()
            {
                Slug = "hero-factory",
                ImageUrl = "/images/projects/project-bma.jpg",
                Title = "Tổng thầu Thiết kế & Thi công Nhà máy",
                Subtitle = "Hơn 18 năm kinh nghiệm — 150+ dự án công nghiệp",
                LinkUrl = "/projects",
                LinkText = "Xem dự án",
                IsActive = true,
                SortOrder = 0,
            },
            new()
            {
                Slug = "hero-design-build",
                ImageUrl = "/images/projects/project-nbdc.jpg",
                Title = "Design & Build — Giải pháp trọn gói",
                Subtitle = "Một đầu mối — toàn bộ vòng đời dự án từ thiết kế đến bàn giao",
                LinkUrl = "/services/design-and-build",
                LinkText = "Tìm hiểu thêm",
                IsActive = true,
                SortOrder = 1,
            },
            new()
            {
                Slug = "hero-industrial",
                ImageUrl = "/images/projects/project-lhh.jpg",
                Title = "Nhà máy Công nghiệp Quy mô lớn",
                Subtitle = "Tổ hợp 250.000 m² — Tiêu chuẩn Nhật Bản cùng Mori Group",
                LinkUrl = "/projects/nha-may-lhh",
                LinkText = "Xem chi tiết",
                IsActive = true,
                SortOrder = 2,
            },
            new()
            {
                Slug = "hero-sports-center",
                ImageUrl = "/images/projects/project-sports.jpg",
                Title = "Công trình Thể dục Thể thao",
                Subtitle = "Thiết kế không gian thể thao đa năng phục vụ cộng đồng",
                LinkUrl = "/projects/ttdtt-thu-duc",
                LinkText = "Khám phá",
                IsActive = true,
                SortOrder = 3,
            },
            new()
            {
                Slug = "hero-office",
                ImageUrl = "/images/projects/project-office.jpg",
                Title = "Nội thất Văn phòng Hiện đại",
                Subtitle = "Phong cách tối giản — Không gian mở — Tiêu chuẩn quốc tế",
                LinkUrl = "/projects/noi-that-b37",
                LinkText = "Xem dự án",
                IsActive = true,
                SortOrder = 4,
            },
        };

        db.SlideshowItems.AddRange(items);
        db.SaveChanges();
    }

    // ─── Recruitment ────────────────────────────────────────────────

    private static void SeedAboutSections(AppDbContext db)
    {
        if (db.AboutSectionContents.Any()) return;

        var now = DateTime.UtcNow;

        db.AboutSectionContents.AddRange(
            new AboutSectionContent
            {
                Slug = "about-main",
                Eyebrow = "VỀ CHÚNG TÔI",
                TitleA = "Đối tác của sự",
                TitleB = "phát triển từ 2006",
                Paragraph1 = "Hơn 18 năm đồng hành cùng các nhà đầu tư trong và ngoài nước, NICON kiến tạo những công trình công nghiệp và dân dụng đạt chuẩn quốc tế.",
                Paragraph2 = "Chúng tôi tập trung vào chất lượng, tiến độ và an toàn, đảm bảo mỗi dự án đều mang lại hiệu quả đầu tư bền vững cho khách hàng.",
                ImageUrl = "/images/activities/activity-handover.jpg",
                IsActive = true,
                SortOrder = 0,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new AboutSectionContent
            {
                Slug = "values-main",
                Eyebrow = "GIÁ TRỊ CỐT LÕI",
                TitleA = "Nền tảng phát triển",
                TitleB = "NICON",
                ItemsJson = JsonSerializer.Serialize(new[]
                {
                    new { iconKey = "target", sortOrder = 0, isActive = true, title = "Mục tiêu rõ ràng", desc = "Mọi quyết định đều hướng đến hiệu quả đầu tư và mục tiêu dài hạn của khách hàng." },
                    new { iconKey = "shield", sortOrder = 1, isActive = true, title = "Kỷ luật chất lượng", desc = "Quy trình thi công, giám sát và nghiệm thu được kiểm soát nghiêm ngặt." },
                    new { iconKey = "compass", sortOrder = 2, isActive = true, title = "Định hướng bền vững", desc = "Ưu tiên giải pháp tối ưu vận hành, chi phí và vòng đời công trình." },
                    new { iconKey = "heart", sortOrder = 3, isActive = true, title = "Tận tâm đồng hành", desc = "Xây dựng niềm tin bằng cách làm việc minh bạch và trách nhiệm đến cùng." },
                }),
                IsActive = true,
                SortOrder = 2,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new AboutSectionContent
            {
                Slug = "stats-main",
                Eyebrow = "CHỈ SỐ NỔI BẬT",
                ItemsJson = JsonSerializer.Serialize(new[]
                {
                    new { iconKey = "calendar", sortOrder = 0, isActive = true, num = "18+", label = "Năm kinh nghiệm" },
                    new { iconKey = "building", sortOrder = 1, isActive = true, num = "150+", label = "Dự án hoàn thành" },
                    new { iconKey = "users", sortOrder = 2, isActive = true, num = "80+", label = "Khách hàng đồng hành" },
                    new { iconKey = "award", sortOrder = 3, isActive = true, num = "ISO", label = "Chuẩn hóa chất lượng" },
                }),
                IsActive = true,
                SortOrder = 1,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new AboutSectionContent
            {
                Slug = "strategy-main",
                Eyebrow = "CHIẾN LƯỢC",
                TitleA = "Tư duy hệ thống cho",
                TitleB = "mỗi dự án",
                Paragraph1 = "Tầm nhìn: Trở thành tổng thầu thiết kế - thi công uy tín hàng đầu trong lĩnh vực công nghiệp và dân dụng tại Việt Nam.",
                Paragraph2 = "Định hướng tương lai: Liên tục nâng cao năng lực thiết kế, quản lý và công nghệ để đáp ứng các tiêu chuẩn quốc tế ngày càng cao.",
                ItemsJson = JsonSerializer.Serialize(new[]
                {
                    new { iconKey = "building", sortOrder = 0, isActive = true, title = "Thiết kế - thi công tổng thể", desc = "Một đầu mối thống nhất giúp kiểm soát tiến độ, chi phí và chất lượng." },
                    new { iconKey = "hammer", sortOrder = 1, isActive = true, title = "Kết cấu và hạ tầng công nghiệp", desc = "Tối ưu giải pháp nền móng, kết cấu và hạ tầng kỹ thuật." },
                    new { iconKey = "layers", sortOrder = 2, isActive = true, title = "Cơ điện và hệ thống phụ trợ", desc = "Đảm bảo vận hành ổn định, an toàn và phù hợp tiêu chuẩn dự án." },
                    new { iconKey = "wrench", sortOrder = 3, isActive = true, title = "Bảo trì và cải tạo", desc = "Đồng hành cùng khách hàng trong suốt vòng đời vận hành công trình." },
                    new { iconKey = "briefcase", sortOrder = 4, isActive = true, title = "Tư vấn đầu tư", desc = "Hỗ trợ chủ đầu tư từ giai đoạn ý tưởng đến kế hoạch triển khai." },
                    new { iconKey = "users-group", sortOrder = 5, isActive = true, title = "Phát triển đội ngũ", desc = "Tăng cường năng lực tổ chức để đáp ứng dự án có quy mô ngày càng lớn." },
                }),
                IsActive = true,
                SortOrder = 3,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new AboutSectionContent
            {
                Slug = "organization-main",
                Eyebrow = "TỔ CHỨC",
                TitleA = "Bộ máy điều hành",
                TitleB = "vững mạnh",
                ItemsJson = JsonSerializer.Serialize(new
                {
                    board = new[]
                    {
                        new { sortOrder = 0, role = "Chủ tịch HĐQT", name = "Ông Võ Trí Nguyên" },
                        new { sortOrder = 1, role = "Phó chủ tịch HĐQT", name = "Ông Trần Văn A" },
                        new { sortOrder = 2, role = "Phó chủ tịch HĐQT", name = "Ông Nguyễn Văn B" },
                        new { sortOrder = 3, role = "Thư ký HĐQT", name = "Bà Lê Thị C" },
                    },
                    directors = new[]
                    {
                        new { sortOrder = 0, role = "Tổng giám đốc", name = "Ông Võ Trí Nguyên" },
                        new { sortOrder = 1, role = "Giám đốc BD Nhật Bản", name = "Ông Daisuke Mori" },
                        new { sortOrder = 2, role = "Giám đốc BD châu Á", name = "Ông Kenji Sato" },
                        new { sortOrder = 3, role = "Giám đốc thiết kế", name = "Bà Nguyễn Thị Lan" },
                    },
                }),
                IsActive = true,
                SortOrder = 4,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new AboutSectionContent
            {
                Slug = "timeline-main",
                Eyebrow = "LỊCH SỬ",
                TitleA = "Dấu mốc phát triển",
                TitleB = "qua từng giai đoạn",
                ImageUrl = "/images/activities/activity-opening.jpg",
                ItemsJson = JsonSerializer.Serialize(new[]
                {
                    new { sortOrder = 0, year = "2006", title = "Thành lập NICON", desc = "Đặt nền móng cho hành trình phát triển trong lĩnh vực xây dựng công nghiệp." },
                    new { sortOrder = 1, year = "2007", title = "Mở rộng đội ngũ", desc = "Tăng cường năng lực triển khai và quản lý dự án." },
                    new { sortOrder = 2, year = "2010", title = "Chinh phục dự án FDI", desc = "Bắt đầu đồng hành cùng nhiều nhà đầu tư nước ngoài." },
                    new { sortOrder = 3, year = "2016", title = "Chuẩn hóa quy trình", desc = "Nâng cao hiệu quả quản trị và kiểm soát chất lượng." },
                    new { sortOrder = 4, year = "2018", title = "Mở rộng hợp tác chiến lược", desc = "Tăng cường kết nối với các đối tác trong và ngoài nước." },
                    new { sortOrder = 5, year = "2024", title = "Tiếp tục tăng trưởng", desc = "Khẳng định vị thế tổng thầu uy tín với nhiều dự án quy mô lớn." },
                }),
                IsActive = true,
                SortOrder = 5,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new AboutSectionContent
            {
                Slug = "certs-main",
                Eyebrow = "CHỨNG NHẬN",
                TitleA = "Tiêu chuẩn vận hành",
                TitleB = "đáng tin cậy",
                ItemsJson = JsonSerializer.Serialize(new[]
                {
                    new { sortOrder = 0, name = "ISO 9001:2008", desc = "Hệ thống quản lý chất lượng." },
                    new { sortOrder = 1, name = "ISO 9001:2015", desc = "Chuẩn hóa quy trình và cải tiến liên tục." },
                    new { sortOrder = 2, name = "ISO 14001", desc = "Quản lý môi trường trong thi công và vận hành." },
                }),
                IsActive = true,
                SortOrder = 6,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new AboutSectionContent
            {
                Slug = "downloads-main",
                Eyebrow = "TÀI LIỆU",
                TitleA = "Hồ sơ năng lực",
                TitleB = "và tài liệu tham khảo",
                Paragraph1 = "Tổng hợp các tài liệu giới thiệu năng lực, chứng nhận và thông tin doanh nghiệp phục vụ đối tác, khách hàng và nhà đầu tư.",
                ItemsJson = JsonSerializer.Serialize(new[]
                {
                    new { sortOrder = 0, name = "Company Profile", size = "12 MB", type = "PDF", url = "#" },
                    new { sortOrder = 1, name = "Brochure năng lực", size = "8 MB", type = "PDF", url = "#" },
                    new { sortOrder = 2, name = "ISO Certificates", size = "4 MB", type = "PDF", url = "#" },
                    new { sortOrder = 3, name = "Danh mục dự án tiêu biểu", size = "10 MB", type = "PDF", url = "#" },
                }),
                IsActive = true,
                SortOrder = 7,
                CreatedAt = now,
                UpdatedAt = now,
            });

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

        // ─── Projects: English translations ───
        Add(EntityTypes.Project, 1, "Name", "en", "BMA Factory");
        Add(EntityTypes.Project, 1, "Description", "en", "The BMA Factory project is a modern 15,000 m² production complex, designed to international industrial standards.");

        Add(EntityTypes.Project, 2, "Name", "en", "NBDC Workshop");
        Add(EntityTypes.Project, 2, "Description", "en", "NICON provides full architecture and structural design for the NBDC production workshop at Giang Dien IP.");

        Add(EntityTypes.Project, 3, "Name", "en", "Lam Hiep Hung – Tan Toan Phat Complex");
        Add(EntityTypes.Project, 3, "Description", "en", "One of NICON's largest projects: a 250,000 m² industrial complex.");

        Add(EntityTypes.Project, 4, "Name", "en", "Thu Duc Sports Center");
        Add(EntityTypes.Project, 4, "Description", "en", "Multi-purpose sports and recreation center serving the Thu Duc community.");

        Add(EntityTypes.Project, 5, "Name", "en", "B37 Office");
        Add(EntityTypes.Project, 5, "Description", "en", "Modern office interior design with minimalist style and open space concept.");

        Add(EntityTypes.Project, 6, "Name", "en", "TriMas Vietnam Factory");
        Add(EntityTypes.Project, 6, "Description", "en", "Turnkey design and build of a packaging factory for TriMas at VSIP IIA.");

        Add(EntityTypes.Project, 7, "Name", "en", "APM Warehouse");
        Add(EntityTypes.Project, 7, "Description", "en", "Logistics warehouse design for Auto Components Vietnam.");

        Add(EntityTypes.Project, 8, "Name", "en", "JOJO Factory");
        Add(EntityTypes.Project, 8, "Description", "en", "Factory design with high food safety requirements, HACCP compliant.");

        Add(EntityTypes.Project, 9, "Name", "en", "D22 Hotel");
        Add(EntityTypes.Project, 9, "Description", "en", "4-star hotel with 80 rooms, ground-floor restaurant and spa.");

        Add(EntityTypes.Project, 10, "Name", "en", "NBDC Canteen");
        Add(EntityTypes.Project, 10, "Description", "en", "Canteen design for the NBDC industrial complex at Giang Dien IP.");

        Add(EntityTypes.Project, 11, "Name", "en", "NBDC Office");
        Add(EntityTypes.Project, 11, "Description", "en", "Executive office design for NBDC at Giang Dien IP.");

        Add(EntityTypes.Project, 12, "Name", "en", "Tan Toan Phat Factory");
        Add(EntityTypes.Project, 12, "Description", "en", "Factory within the Lam Hiep Hung – Tan Toan Phat complex in Binh Duong.");

        Add(EntityTypes.Project, 13, "Name", "en", "Lam Hiep Hung Factory");
        Add(EntityTypes.Project, 13, "Description", "en", "Lam Hiep Hung factory in Binh Duong, expanding production line.");

        Add(EntityTypes.Project, 14, "Name", "en", "S.T.Food Marketing Vietnam Factory");
        Add(EntityTypes.Project, 14, "Description", "en", "Food production factory for a Thai investor, designed to GMP and HACCP standards.");

        Add(EntityTypes.Project, 15, "Name", "en", "Medicare Shop");
        Add(EntityTypes.Project, 15, "Description", "en", "Medicare store at Aeon Mall Binh Duong Canary.");

        Add(EntityTypes.Project, 16, "Name", "en", "Lam Hiep Hung Factory (Phase 1)");
        Add(EntityTypes.Project, 16, "Description", "en", "Lam Hiep Hung factory phase 1 completed.");

        Add(EntityTypes.Project, 17, "Name", "en", "SCTV Office");
        Add(EntityTypes.Project, 17, "Description", "en", "Office design and construction for SCTV in District 2, HCMC.");

        Add(EntityTypes.Project, 18, "Name", "en", "H.B.Fuller Factory");
        Add(EntityTypes.Project, 18, "Description", "en", "MEP system construction for H.B.Fuller factory in Binh Duong.");

        Add(EntityTypes.Project, 19, "Name", "en", "Red Bull Expansion Project");
        Add(EntityTypes.Project, 19, "Description", "en", "Design for the Red Bull factory expansion in Binh Duong, preserving brand identity.");

        Add(EntityTypes.Project, 20, "Name", "en", "Great Lotus Vietnam Factory");
        Add(EntityTypes.Project, 20, "Description", "en", "Design and construction of the Great Lotus factory spanning over 31,000 m².");

        Add(EntityTypes.Project, 21, "Name", "en", "Advanced Casting Asia Factory");
        Add(EntityTypes.Project, 21, "Description", "en", "Advanced Casting Asia production factory at VSIP II-A.");

        Add(EntityTypes.Project, 22, "Name", "en", "SCTV Studio & Office");
        Add(EntityTypes.Project, 22, "Description", "en", "The largest TV studio in Vietnam at the time of construction.");

        Add(EntityTypes.Project, 23, "Name", "en", "BKL Factory");
        Add(EntityTypes.Project, 23, "Description", "en", "Design and construction of BKL factory at Thinh Phat IP.");

        Add(EntityTypes.Project, 24, "Name", "en", "Rebisco Factory");
        Add(EntityTypes.Project, 24, "Description", "en", "Rebisco confectionery factory (Philippines) at VSIP II-A.");

        Add(EntityTypes.Project, 25, "Name", "en", "Nestlé Binh An Factory & Office");
        Add(EntityTypes.Project, 25, "Description", "en", "Design and construction of Nestlé Binh An factory and office.");

        Add(EntityTypes.Project, 26, "Name", "en", "Ampharco U.S.A Factory");
        Add(EntityTypes.Project, 26, "Description", "en", "Pharmaceutical factory construction for Ampharco U.S.A at Nhon Trach.");

        Add(EntityTypes.Project, 27, "Name", "en", "Konimiyaki Restaurant");
        Add(EntityTypes.Project, 27, "Description", "en", "Restaurant design for Konimiyaki in District 1, HCMC.");

        Add(EntityTypes.Project, 28, "Name", "en", "SCON Factory");
        Add(EntityTypes.Project, 28, "Description", "en", "Design and construction of SCON factory at VSIP II-A.");

        Add(EntityTypes.Project, 29, "Name", "en", "Clotex Labels Vietnam Factory");
        Add(EntityTypes.Project, 29, "Description", "en", "Clotex Labels Vietnam factory at VSIP II-A.");

        Add(EntityTypes.Project, 30, "Name", "en", "Amiba Factory");
        Add(EntityTypes.Project, 30, "Description", "en", "Construction of the 2-hectare Amiba factory at VSIP II-A.");

        Add(EntityTypes.Project, 31, "Name", "en", "Akati Wood Factory");
        Add(EntityTypes.Project, 31, "Description", "en", "Akati Wood factory, a branch of Akati Dominant from Malaysia.");

        Add(EntityTypes.Project, 32, "Name", "en", "Japan Plus Factory");
        Add(EntityTypes.Project, 32, "Description", "en", "Japan Plus PE box production factory at Dong Nam Cu Chi IP.");

        Add(EntityTypes.Project, 33, "Name", "en", "Central Pharmaceutical HCMC");
        Add(EntityTypes.Project, 33, "Description", "en", "Pharmaceutical factory design in HCMC.");

        Add(EntityTypes.Project, 34, "Name", "en", "Kumgang Office");
        Add(EntityTypes.Project, 34, "Description", "en", "Office design and construction for Kumgang Vina.");

        Add(EntityTypes.Project, 35, "Name", "en", "VDA-HCM Factory");
        Add(EntityTypes.Project, 35, "Description", "en", "Design and construction of VDA-HCM factory at Cau Tram IP.");

        Add(EntityTypes.Project, 36, "Name", "en", "Thu Thiem Dragon Show Flat");
        Add(EntityTypes.Project, 36, "Description", "en", "Show flat construction for Thu Thiem Dragon in District 2.");

        Add(EntityTypes.Project, 37, "Name", "en", "Nam Ha Viet Factory");
        Add(EntityTypes.Project, 37, "Description", "en", "Welding rod production factory Nam Ha Viet.");

        Add(EntityTypes.Project, 38, "Name", "en", "YC TEC Factory");
        Add(EntityTypes.Project, 38, "Description", "en", "YC TEC factory design at Song Than II IP.");

        // ─── Services: English translations ───
        Add(EntityTypes.Service, 1, "Title", "en", "Design & Build General Contractor (D&B)");
        Add(EntityTypes.Service, 1, "ShortTitle", "en", "Design & Build");
        Add(EntityTypes.Service, 1, "Tagline", "en", "One point of contact — full project lifecycle from design to handover.");
        Add(EntityTypes.Service, 1, "Intro", "en", "Design & Build (D&B) and EPC are the two most popular methods in industrial and civil construction. NICON has systemized the D&B process since inception and continuously refined it through 150+ projects.");

        Add(EntityTypes.Service, 2, "Title", "en", "Main Contractor Services");
        Add(EntityTypes.Service, 2, "ShortTitle", "en", "Main Contractor");
        Add(EntityTypes.Service, 2, "Tagline", "en", "Full construction management — turnkey handover.");
        Add(EntityTypes.Service, 2, "Intro", "en", "As a Vietnamese–Japanese Main Contractor, NICON fulfills all tasks of an industrial construction project.");

        Add(EntityTypes.Service, 3, "Title", "en", "General Contractor Services");
        Add(EntityTypes.Service, 3, "ShortTitle", "en", "General Contractor");
        Add(EntityTypes.Service, 3, "Tagline", "en", "Handling the full lifecycle of industrial factory construction.");
        Add(EntityTypes.Service, 3, "Intro", "en", "As a Vietnamese–Japanese General Contractor, NICON undertakes design, permitting, construction and turnkey handover.");

        Add(EntityTypes.Service, 4, "Title", "en", "MEP Contractor Services");
        Add(EntityTypes.Service, 4, "ShortTitle", "en", "MEP Contractor");
        Add(EntityTypes.Service, 4, "Tagline", "en", "Synchronized Mechanical–Electrical–Plumbing systems optimized for operations.");
        Add(EntityTypes.Service, 4, "Intro", "en", "MEP (Mechanical–Electrical–Plumbing) is the key system determining factory operational efficiency. NICON provides standalone or integrated MEP contracting within D&B packages.");

        // ─── Service Sections: EN translations ───
        Add(EntityTypes.Service, 1, "Sections", "en", JsonSerializer.Serialize(new[] {
            new { heading = "Advantages of D&B / EPC", body = "Minimize management obligations for the investor — NICON handles all project coordination. Reduce inconsistency risk between design and construction. Accelerate schedule even before design is finalized, cutting change-order costs. Reasonable management fees — the investor works with a single contractor for easy cost estimation and quality control." },
            new { heading = "Advanced Project Management", body = "In close partnership with Mori Construction (Japan), NICON applies BIM (Building Information Modeling) throughout every phase. An experienced BIM team delivers synchronized solutions, giving investors full project visibility and early risk detection." },
            new { heading = "Best Products from the Best People", body = "NICON maintains an international partner network spanning architecture, structure, interiors and M&E. An experienced team of project managers, architects, engineers and skilled workers can handle LARGE-SCALE – TIGHT-SCHEDULE – HIGH-QUALITY projects." }
        }));
        Add(EntityTypes.Service, 2, "Sections", "en", JsonSerializer.Serialize(new[] {
            new { heading = "Main Contractor Scope", body = "Full site management, subcontractor and supplier coordination. Ensure schedule, quality and HSE (Health Safety Environment) on site. Regular reporting to investors in Vietnamese, English and Japanese." },
            new { heading = "International-Standard Management", body = "PMP project management standards and Lean Construction methods. MS Project and Primavera P6 for scheduling and cost control. QA/QC per ISO 9001:2015 for every work package." },
            new { heading = "Strategic Partnership with Mori Group", body = "The partnership with Mori Industry Group (Japan) brings Japanese technical standards and work culture to every NICON project." }
        }));
        Add(EntityTypes.Service, 3, "Sections", "en", JsonSerializer.Serialize(new[] {
            new { heading = "General Contractor Role", body = "Comprehensive management from schematic design, technical design to construction drawings. Material and equipment procurement and supply chain management. Construction execution, partial acceptance and complete handover." },
            new { heading = "Mega-Project Capability", body = "NICON has successfully delivered 250,000 m² industrial complexes like Lam Hiep Hung – Tan Toan Phat. Capability to manage large sites with hundreds of workers, heavy equipment and complex logistics." },
            new { heading = "Quality Commitment", body = "100% of projects delivered on schedule over the last 5 years. 24-month warranty for construction, 12-month warranty for MEP." }
        }));
        Add(EntityTypes.Service, 4, "Sections", "en", JsonSerializer.Serialize(new[] {
            new { heading = "NICON MEP Scope", body = "Industrial electrical systems: medium/low voltage, backup generators, UPS, high-efficiency lighting. HVAC, ventilation and cleanroom systems per ISO Class 5/7/8. Water supply/drainage, solar hot water, wastewater treatment. Fire sprinkler and addressable alarm systems per TCVN and NFPA." },
            new { heading = "Integration & Handover", body = "Rigorous T&C (Testing & Commissioning) process witnessed by supervision consultants and investors. Handover with as-built documentation, O&M manuals. Operational training for the investor's technical staff." },
            new { heading = "BIM-Powered Project Management", body = "3D MEP models detect clashes before construction, reducing 80% of field rework. BIM deliverables support long-term operations and maintenance for the investor." }
        }));

        // ─── Project Challenges & Solutions: EN translations (Projects 1-3) ───
        Add(EntityTypes.Project, 1, "Challenges", "en", JsonSerializer.Serialize(new[] { "Tight 10-month schedule from groundbreaking to commissioning.", "Large-span column-free structural solution for production lines.", "Optimizing ventilation and natural lighting for energy savings." }));
        Add(EntityTypes.Project, 1, "Solutions", "en", JsonSerializer.Serialize(new[] { "Pre-engineered steel with 30m spans and polycarbonate skylights.", "Parallel construction of multiple work packages, managed with BIM 4D software.", "Synchronized M&E systems with 30% capacity reserve for future expansion." }));
        Add(EntityTypes.Project, 2, "Challenges", "en", JsonSerializer.Serialize(new[] { "Complex production line layout with multiple functional zones.", "Requirement to integrate executive offices and production in one building." }));
        Add(EntityTypes.Project, 2, "Solutions", "en", JsonSerializer.Serialize(new[] { "Clear zoning with one-way traffic flow to minimize cross-movement.", "Two-story integrated office with production floor overlook." }));
        Add(EntityTypes.Project, 3, "Challenges", "en", JsonSerializer.Serialize(new[] { "Master planning for a mega-scale site with many building blocks.", "Synchronizing technical infrastructure across a large area." }));
        Add(EntityTypes.Project, 3, "Solutions", "en", JsonSerializer.Serialize(new[] { "Modular master plan for easy expansion and function changes.", "Internal road system designed for 40-foot container trucks." }));

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

    // Stock placeholders that the original hand-curated seeder cycled across
    // every Activity row. Their presence is the signal to re-seed from the
    // legacy manifest.
    private static bool IsLegacyStockActivityImage(string url) =>
        url is "/images/activities/activity-ceremony.jpg"
            or "/images/activities/activity-handover.jpg"
            or "/images/activities/activity-opening.jpg";

    // Stock thumbnails (news-fire-protection.jpeg, news-build-concept.jpeg, …)
    // used by the legacy hand-curated News seeder. Per-slug folders use
    // /images/news/<slug>/thumb.* so this prefix is unambiguous.
    private static bool IsLegacyStockNewsImage(string url) =>
        url.StartsWith("/images/news/news-", StringComparison.Ordinal);

    private static bool NeedsContentReseed<T>(IQueryable<T> set, int manifestCount, Func<T, string> imageUrlOf, Func<string, bool> isStockImage)
        where T : class
    {
        var existing = set.ToList();
        if (existing.Count == 0) return true;
        if (existing.Count != manifestCount) return true;
        return existing.Any(e => isStockImage(imageUrlOf(e)));
    }

    private static void ReseedFromManifest<T>(AppDbContext db, string entityType, List<ContentSeedItem> manifest, Func<ContentSeedItem, T> factory)
        where T : class
    {
        // Wipe dependent translation rows first so they don't dangle once the
        // entities (and their IDs) are recreated.
        var staleTranslations = db.EntityTranslations.Where(t => t.EntityType == entityType).ToList();
        if (staleTranslations.Count > 0)
        {
            db.EntityTranslations.RemoveRange(staleTranslations);
        }

        var dbSet = db.Set<T>();
        var existing = dbSet.ToList();
        if (existing.Count > 0)
        {
            dbSet.RemoveRange(existing);
        }
        db.SaveChanges();

        foreach (var item in manifest)
        {
            dbSet.Add(factory(item));
        }
        db.SaveChanges();
    }

    private static void SeedManifestTranslations(AppDbContext db, string entityType, List<ContentSeedItem> manifest, IDictionary<string, int> bySlug)
    {
        var now = DateTime.UtcNow;
        var rows = new List<EntityTranslation>();
        foreach (var item in manifest)
        {
            if (!bySlug.TryGetValue(item.Slug, out var entityId)) continue;
            foreach (var (lang, t) in item.Translations)
            {
                if (lang == "vi") continue; // VI lives on the entity itself
                if (!string.IsNullOrEmpty(t.Title))
                    rows.Add(new EntityTranslation { EntityType = entityType, EntityId = entityId, FieldName = "Title", LanguageCode = lang, Value = t.Title, CreatedAt = now, UpdatedAt = now });
                if (!string.IsNullOrEmpty(t.Excerpt))
                    rows.Add(new EntityTranslation { EntityType = entityType, EntityId = entityId, FieldName = "Excerpt", LanguageCode = lang, Value = t.Excerpt, CreatedAt = now, UpdatedAt = now });
                if (t.Content is { Count: > 0 })
                    rows.Add(new EntityTranslation { EntityType = entityType, EntityId = entityId, FieldName = "Content", LanguageCode = lang, Value = JsonSerializer.Serialize(t.Content), CreatedAt = now, UpdatedAt = now });
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
        public List<string> Content { get; set; } = [];
        public string Date { get; set; } = "";
    }
}
