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
            new { Name = "Nhà máy công nghiệp", SortOrder = 0 },
            new { Name = "Nhà xưởng sản xuất",  SortOrder = 1 },
            new { Name = "Tổ hợp công nghiệp",  SortOrder = 2 },
            new { Name = "Nhà kho logistics",    SortOrder = 3 },
            new { Name = "Văn phòng",            SortOrder = 4 },
            new { Name = "Nội thất văn phòng",   SortOrder = 5 },
            new { Name = "Nội thất công nghiệp", SortOrder = 6 },
            new { Name = "Công trình công cộng", SortOrder = 7 },
            new { Name = "Khách sạn",            SortOrder = 8 },
            new { Name = "Nhà hàng",             SortOrder = 9 },
            new { Name = "Thương mại",           SortOrder = 10 },
            new { Name = "Nhà ở",                SortOrder = 11 },
            new { Name = "Bất động sản",         SortOrder = 12 },
            new { Name = "Studio",               SortOrder = 13 },
            new { Name = "Nhà máy dược phẩm",   SortOrder = 14 },
            new { Name = "Giáo dục",             SortOrder = 15 },
        };

        var existingProjCats = db.ProjectCategories
            .ToDictionary(c => c.Name.ToLower());
        foreach (var seed in projectCats)
        {
            var key = seed.Name.ToLower();
            if (existingProjCats.TryGetValue(key, out var existing))
                existing.SortOrder = seed.SortOrder;
            else
                db.ProjectCategories.Add(new ProjectCategory
                {
                    Name = seed.Name,
                    IsActive = true,
                    SortOrder = seed.SortOrder,
                });
        }

        var activityCats = new[]
        {
            new { Name = "Khởi công",   SortOrder = 1 },
            new { Name = "Khánh thành", SortOrder = 2 },
            new { Name = "Sự kiện",     SortOrder = 3 },
            new { Name = "Dự án",       SortOrder = 4 },
            new { Name = "Giải thưởng", SortOrder = 5 },
            new { Name = "Triển lãm",   SortOrder = 6 },
            new { Name = "Cộng đồng",   SortOrder = 7 },
            new { Name = "Văn hóa",     SortOrder = 8 },
            new { Name = "Đào tạo",     SortOrder = 9 },
            new { Name = "Dịch vụ",     SortOrder = 10 },
        };

        var existingActCats = db.ActivityCategories
            .ToDictionary(c => c.Name.ToLower());
        foreach (var seed in activityCats)
        {
            var key = seed.Name.ToLower();
            if (existingActCats.TryGetValue(key, out var existing))
                existing.SortOrder = seed.SortOrder;
            else
                db.ActivityCategories.Add(new ActivityCategory
                {
                    Name = seed.Name,
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
        var existingSlugs = db.Projects.Select(p => p.Slug).ToHashSet();

        var items = new Project[]
        {
            new() { Slug = "nha-may-bma", ImageUrl = "/images/upload/projects/nha-may-bma/ac76817591734666a77302a6a585e597.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/upload/projects/nha-may-bma/be5ba734b9ef41d8bbbbc37cc0aa45a8.jpg", "/images/upload/projects/nha-may-bma/a229d8279b1f4fe49d1f262290694851.jpg", "/images/upload/projects/nha-may-bma/1c9cb5fa28dc484786dff3f1195b7c99.jpg" }), Name = "Nhà Máy BMA", Client = "Bảo Minh Ân Việt Nam", Location = "KCN Hựu Thạnh, Tây Ninh", Scale = "15.000 m²", Scope = "Thiết kế và Thi công", Status = "ongoing", Year = "2024", Category = "Nhà máy công nghiệp", Description = "Dự án Nhà Máy BMA là tổ hợp sản xuất hiện đại với quy mô 15.000 m², được thiết kế theo tiêu chuẩn công nghiệp quốc tế.", ChallengesJson = JsonSerializer.Serialize(new[] { "Yêu cầu tiến độ chặt chẽ trong vòng 10 tháng từ khởi công đến vận hành.", "Giải pháp kết cấu nhà xưởng nhịp lớn không cột giữa cho dây chuyền sản xuất.", "Tối ưu hệ thống thông gió và chiếu sáng tự nhiên để tiết kiệm năng lượng." }), SolutionsJson = JsonSerializer.Serialize(new[] { "Áp dụng kết cấu thép tiền chế nhịp 30m với mái lấy sáng polycarbonate.", "Thi công song song nhiều hạng mục, quản lý tiến độ bằng phần mềm BIM 4D.", "Hệ thống M&E đồng bộ, dự phòng công suất cho mở rộng tương lai 30%." }), HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "15.000 m²" }, new { label = "Thời gian", value = "10 tháng" }, new { label = "Nhịp kết cấu", value = "30 m" }, new { label = "Tiêu chuẩn", value = "ISO 9001" } }), SortOrder = 0 },
            new() { Slug = "nha-xuong-nbdc", ImageUrl = "/images/upload/projects/nha-xuong-nbdc/c12fa9a6fc3b470297e97694cf46a0df.png", GalleryJson = JsonSerializer.Serialize(new[] { "/images/upload/projects/nha-xuong-nbdc/64e0e3b0c3d0488e9fe8fbede7583c32.png", "/images/upload/projects/nha-xuong-nbdc/77bbf3568dcd4f688e53d2b36e00c9dc.png", "/images/upload/projects/nha-xuong-nbdc/fa7be40c997441d2ad8a771afe4657f0.png", "/images/upload/projects/nha-xuong-nbdc/36551c4836214561a9df7ff6011fa70d.png", "/images/upload/projects/nha-xuong-nbdc/b3457e0b4aa14c1bb0eafcf4459cd573.png", "/images/upload/projects/nha-xuong-nbdc/eb64b8623e3547c4b043eb7632c194c5.png", "/images/upload/projects/nha-xuong-nbdc/d155bd5adf2c4940ba8277d74f115209.png", "/images/upload/projects/nha-xuong-nbdc/3c98a37c7565451ba206ebfb6576d1a3.png", "/images/upload/projects/nha-xuong-nbdc/58ede7ac6651447ca98b04a1eb2b988c.png" }), Name = "Nhà Xưởng NBDC", Client = "Công ty TNHH NBDC VN", Location = "KCN Giang Điền, Đồng Nai", Scale = "8.500 m²", Scope = "Thiết kế", Status = "ongoing", Year = "2024", Category = "Nhà xưởng sản xuất", Description = "NICON cung cấp dịch vụ thiết kế kiến trúc và kết cấu trọn gói cho nhà xưởng sản xuất NBDC tại KCN Giang Điền.", ChallengesJson = JsonSerializer.Serialize(new[] { "Bố cục dây chuyền sản xuất phức tạp với nhiều khu vực chức năng.", "Yêu cầu tích hợp khu văn phòng điều hành và sản xuất trong cùng một khối." }), SolutionsJson = JsonSerializer.Serialize(new[] { "Phân khu rõ ràng với luồng di chuyển một chiều, giảm chéo nhau.", "Thiết kế khu văn phòng 2 tầng tích hợp với view nhìn xuống xưởng." }), HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "8.500 m²" }, new { label = "Khu chức năng", value = "5" }, new { label = "Nhân sự dự kiến", value = "200" }, new { label = "Năm", value = "2024" } }), SortOrder = 1 },
            new() { Slug = "nha-may-lhh", ImageUrl = "/images/upload/projects/nha-may-lhh/db52c54fb9f848bfb591d522a62c8636.png", GalleryJson = JsonSerializer.Serialize(new[] { "/images/upload/projects/nha-may-lhh/5cadef0f6bed451ba39e47c0c4e189bf.png", "/images/upload/projects/nha-may-lhh/edbc4bb0c1a04583bfc807613e599a16.png", "/images/upload/projects/nha-may-lhh/9ec23c2303db4848ab582bf9c8e574af.png", "/images/upload/projects/nha-may-lhh/2df4bfe6b05a4b55a3f2a9383bd85fe7.png", "/images/upload/projects/nha-may-lhh/7667d55fc8744b4eaef9928f21323d3d.png" }), Name = "Nhà Máy Lâm Hiệp Hưng – Tân Toàn Phát", Client = "Lam Hiệp Hưng & Tân Toàn Phát", Location = "Tỉnh Bình Dương", Scale = "250.000 m²", Scope = "Thiết kế", Status = "ongoing", Year = "2023", Category = "Tổ hợp công nghiệp", Description = "Một trong những dự án quy mô lớn nhất NICON đã thực hiện: tổ hợp nhà máy 250.000 m².", ChallengesJson = JsonSerializer.Serialize(new[] { "Quy hoạch tổng mặt bằng quy mô siêu lớn với nhiều khối công trình.", "Đồng bộ hạ tầng kỹ thuật trên diện tích lớn." }), SolutionsJson = JsonSerializer.Serialize(new[] { "Quy hoạch theo mô-đun, dễ dàng mở rộng và thay đổi công năng.", "Hệ thống đường nội bộ thiết kế cho xe container 40 feet." }), HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Tổng diện tích", value = "250.000 m²" }, new { label = "Khối công trình", value = "12" }, new { label = "Đường nội bộ", value = "5,2 km" }, new { label = "Năm", value = "2023" } }), SortOrder = 2 },
            new() { Slug = "ttdtt-thu-duc", ImageUrl = "/images/upload/projects/ttdtt-thu-duc/55946ed3ff3a425fa6fc9f0dc914fd03.png", GalleryJson = JsonSerializer.Serialize(new[] { "/images/upload/projects/ttdtt-thu-duc/8b775b3d64a64c489f87d64d7bc451e5.png", "/images/upload/projects/ttdtt-thu-duc/743e103ebc7e4bb9867ffd37af0079d0.png", "/images/upload/projects/ttdtt-thu-duc/6de3c19bc2c249528c167e88f95aa28d.png", "/images/upload/projects/ttdtt-thu-duc/e0e6fc44f67c4605aa49fe3d98cc7240.png", "/images/upload/projects/ttdtt-thu-duc/8ab6da60fd6d4bcba84edf7c20938bf1.png", "/images/upload/projects/ttdtt-thu-duc/ba6c85a50f2748e383b1809197b284c9.png", "/images/upload/projects/ttdtt-thu-duc/53f329739840480f84c7306974d55e24.png", "/images/upload/projects/ttdtt-thu-duc/951d66fd803a46099ca06ddaf5822592.png", "/images/upload/projects/ttdtt-thu-duc/3d100f4e0e5b4ae188060e78b79f5b61.png", "/images/upload/projects/ttdtt-thu-duc/8c33a8f6e32f43838d8443c54c087b46.png", "/images/upload/projects/ttdtt-thu-duc/be481d9da5654b349b59b27367263927.png", "/images/upload/projects/ttdtt-thu-duc/4c12e0c9dc1841728cb890eae19dc20a.png", "/images/upload/projects/ttdtt-thu-duc/e810e0ac796e4d01aad651367681ec41.png", "/images/upload/projects/ttdtt-thu-duc/c9902d37878947029418792f2b9ef9ab.png", "/images/upload/projects/ttdtt-thu-duc/6715df2b6b1148aabb74716addfef18f.png", "/images/upload/projects/ttdtt-thu-duc/2e375b843b5a41608a79946ad32f4197.png", "/images/upload/projects/ttdtt-thu-duc/0dbbea20eff94ac486525dd8ee663c31.png" }), Name = "Trung Tâm Thể Dục Thể Thao Thủ Đức", Client = "Thủ Thiêm Group", Location = "Thủ Đức, TP.HCM", Scale = "12.000 m²", Scope = "Thiết kế", Status = "ongoing", Year = "2024", Category = "Công trình công cộng", Description = "Trung tâm thể dục thể thao đa năng phục vụ cộng đồng tại Thủ Đức.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "12.000 m²" }, new { label = "Nhịp mái", value = "45 m" }, new { label = "Sức chứa", value = "2.000 chỗ" }, new { label = "Năm", value = "2024" } }), SortOrder = 3 },
            new() { Slug = "noi-that-b37", ImageUrl = "/images/upload/projects/noi-that-b37/14af960dd8fa43359832744672101249.png", GalleryJson = JsonSerializer.Serialize(new[] { "/images/upload/projects/noi-that-b37/a91967f105664cf1bb268bfbf8332145.png", "/images/upload/projects/noi-that-b37/a0dc78748de74f349cad2460be5947c4.png", "/images/upload/projects/noi-that-b37/f1f975a232504d6db5ca69ab64829b23.png", "/images/upload/projects/noi-that-b37/de3dedc5f65e4347a01402d7ab99fefa.png", "/images/upload/projects/noi-that-b37/b79633d6eeb94660900519addc498710.png", "/images/upload/projects/noi-that-b37/c8d7ac879bd74589bada875ea309bb76.png", "/images/upload/projects/noi-that-b37/3160410169964e4ea350fcaedf8e0018.png", "/images/upload/projects/noi-that-b37/8c814efdb22f429088cf94d5b3a52557.png", "/images/upload/projects/noi-that-b37/f6ecdfe3320340268378329b1d6aabf8.png", "/images/upload/projects/noi-that-b37/4ca0d33c3bd84fbc89935ddba3b6f3fe.png", "/images/upload/projects/noi-that-b37/23adab0444f54e71b36ea530008e9ab6.png", "/images/upload/projects/noi-that-b37/c4bee8e247484a01a9658201e009762e.png", "/images/upload/projects/noi-that-b37/7480665e8c564b79875b7730bb523c53.png", "/images/upload/projects/noi-that-b37/af3af23926f545c8a9a323d0bb38c8ac.png", "/images/upload/projects/noi-that-b37/2d679bfc864a45b0b0a7687e32b83005.png", "/images/upload/projects/noi-that-b37/ef502085e42a4fbd97286668bbc0649e.png" }), Name = "Văn Phòng B37", Client = "Nihome", Location = "Thủ Đức, TP.HCM", Scale = "1.200 m²", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2024", Category = "Nội thất văn phòng", Description = "Thiết kế nội thất văn phòng hiện đại với phong cách tối giản, không gian mở.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "1.200 m²" }, new { label = "Sức chứa", value = "80 người" }, new { label = "Phòng họp", value = "6" }, new { label = "Năm", value = "2024" } }), SortOrder = 4 },
            new() { Slug = "nha-may-trimas", ImageUrl = "/images/upload/projects/nha-may-trimas/49d5c340a6da447b9dd93de0a43a4e3a.png", Name = "Nhà Máy Trimas Việt Nam", Client = "Rieke Packaging Vietnam Co., Ltd", Location = "VSIP IIA, TP.HCM", Scale = "10.000 m²", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2022", Category = "Nhà máy công nghiệp", Description = "Dự án trọn gói thiết kế và thi công nhà máy sản xuất bao bì cho Trimas tại VSIP IIA.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "10.000 m²" }, new { label = "Tiêu chuẩn", value = "ISO Class 8" }, new { label = "Thời gian", value = "9 tháng" }, new { label = "Năm", value = "2022" } }), SortOrder = 5 },
            new() { Slug = "nha-kho-apm", ImageUrl = "/images/upload/projects/nha-kho-apm/cc78259bea534d598fe543d4a9cb4a85.png", GalleryJson = JsonSerializer.Serialize(new[] { "/images/upload/projects/nha-kho-apm/d53c466aba564aff8df63bede39d46b8.png", "/images/upload/projects/nha-kho-apm/19840031d2a5414db00468ecb899386f.png", "/images/upload/projects/nha-kho-apm/6042f209bb7f4a75851f1853b3b7beea.png", "/images/upload/projects/nha-kho-apm/f9ae2a3ac18141da9d90a1977158b960.png", "/images/upload/projects/nha-kho-apm/f1cdc6d853914e1493ca4106146c2a70.png", "/images/upload/projects/nha-kho-apm/556d4480d21e45d1b89b91058771e5d2.png", "/images/upload/projects/nha-kho-apm/65d57af1150a41dc98033629b2c4fdaa.png", "/images/upload/projects/nha-kho-apm/4d83af5ee08b42158f68ffce61046186.png", "/images/upload/projects/nha-kho-apm/8a2e6f80aefb4ad5b6935288c0a60e09.png", "/images/upload/projects/nha-kho-apm/52d330f6058248de80d769208f9bfd62.png", "/images/upload/projects/nha-kho-apm/b4f27d9a9c64435a8d1fea715ec2f32c.png", "/images/upload/projects/nha-kho-apm/5b238bbc28324eb5ac2772ae26077e99.png", "/images/upload/projects/nha-kho-apm/1ec3898ea2c14ddaa924fbac8f83265c.png", "/images/upload/projects/nha-kho-apm/f7164d37b64043a29aed2118dc762b74.png", "/images/upload/projects/nha-kho-apm/bab664ea95834e39a675daecfb9d5781.png", "/images/upload/projects/nha-kho-apm/ea927441ca844d42b357987341c4375c.png", "/images/upload/projects/nha-kho-apm/32f2688f8a444bab82f5686fcf64d864.png", "/images/upload/projects/nha-kho-apm/72d8f8b85b61476f89028cbdce004cb7.png", "/images/upload/projects/nha-kho-apm/20e6414ee90d46d7972176cf6065d918.png" }), Name = "Nhà Kho APM", Client = "Auto Components Việt Nam", Location = "KCN Việt Nam – Singapore, Bình Hòa, Thuận An, Bình Dương", Scale = "6.500 m²", Scope = "Thiết kế", Status = "completed", Year = "2022", Category = "Nhà kho logistics", Description = "Thiết kế nhà kho logistics cho Auto Components Việt Nam.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "6.500 m²" }, new { label = "Chiều cao", value = "12 m" }, new { label = "Dock loading", value = "6" }, new { label = "Năm", value = "2022" } }), SortOrder = 6 },
            new() { Slug = "nha-may-jojo", ImageUrl = "/images/upload/projects/nha-may-jojo/b6406647a5de4a129fa253ef951836f3.png", GalleryJson = JsonSerializer.Serialize(new[] { "/images/upload/projects/nha-may-jojo/bc65cb619900461ead380aa77f6324a5.png", "/images/upload/projects/nha-may-jojo/4e212ffb20f2417f9e171373cf7ed876.png", "/images/upload/projects/nha-may-jojo/f2deb325d47e42179a9c1b9d6513f7ca.png" }), Name = "Nhà Máy JOJO", Client = "Phạm – Asset", Location = "KCN Hựu Thạnh, Long An", Scale = "7.800 m²", Scope = "Thiết kế", Status = "ongoing", Year = "2024", Category = "Nhà máy công nghiệp", Description = "Thiết kế nhà máy sản xuất JOJO với yêu cầu cao về vệ sinh an toàn thực phẩm.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "7.800 m²" }, new { label = "Tiêu chuẩn", value = "HACCP" }, new { label = "Khu sạch", value = "3" }, new { label = "Năm", value = "2024" } }), SortOrder = 7 },
            new() { Slug = "khach-san-d22", ImageUrl = "/images/upload/projects/khach-san-d22/cdb90726fcbf46fda849d30c906b6a6e.png", GalleryJson = JsonSerializer.Serialize(new[] { "/images/upload/projects/khach-san-d22/46510a7d5f49410490b1b890c6328e33.png", "/images/upload/projects/khach-san-d22/504527a4a3e74c95b739d9cf9d86acfa.png", "/images/upload/projects/khach-san-d22/ce25820f50b64985bd72e152df708b0c.png", "/images/upload/projects/khach-san-d22/a6078e5fc2e945c49445ddf892514cee.png", "/images/upload/projects/khach-san-d22/7bb8aa00914a4041be6d8e021f278d37.png", "/images/upload/projects/khach-san-d22/52c1e89bee454e15b0b602027851e0eb.png", "/images/upload/projects/khach-san-d22/1ba449d3def9464a8ee588500b5627ce.png", "/images/upload/projects/khach-san-d22/fbe1a1c939b24e41aef015d64f9cc9b0.png", "/images/upload/projects/khach-san-d22/66d39575cc9b473995021738605e2520.png", "/images/upload/projects/khach-san-d22/7e1fb386e4c54f4e99c2cf69a37bb08a.png", "/images/upload/projects/khach-san-d22/3958b6fb5b3a4d8a9783ea661fb453cb.png", "/images/upload/projects/khach-san-d22/de11b5c151524304bbb2d4ed5c3822c4.png" }), Name = "Khách sạn D22", Client = "Nihome", Location = "Thủ Đức, TP.HCM", Scale = "4.500 m²", Scope = "Thiết kế và Thi công", Status = "ongoing", Year = "2024", Category = "Khách sạn", Description = "Khách sạn 4 sao với 80 phòng nghỉ, nhà hàng tầng trệt và khu spa.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "4.500 m²" }, new { label = "Số phòng", value = "80" }, new { label = "Tầng cao", value = "12" }, new { label = "Năm", value = "2024" } }), SortOrder = 8 },
            // ── Ongoing projects from nicon.vn ──
            new() { Slug = "nbdc-canteen", ImageUrl = "/images/upload/projects/nbdc-canteen/a6177c920fbc4dd1893981a91b54f649.png", GalleryJson = JsonSerializer.Serialize(new[] { "/images/upload/projects/nbdc-canteen/76476f94aa6940abba93908e2aeb7598.png", "/images/upload/projects/nbdc-canteen/645c36a1e51b41078c23e6038bf2237a.png", "/images/upload/projects/nbdc-canteen/649ecf3ffd114767ae5140ffd955c316.png", "/images/upload/projects/nbdc-canteen/6817204d1d364987b504a53d8357f51a.png", "/images/upload/projects/nbdc-canteen/06d9936f0bf542c3b5086008d374e134.png" }), Name = "NBDC Canteen", Client = "Công ty TNHH NBDC VN", Location = "KCN Giang Điền, Đồng Nai", Scale = "", Scope = "Thiết kế", Status = "ongoing", Year = "2024", Category = "Nhà xưởng sản xuất", Description = "Thiết kế nhà ăn cho khu công nghiệp NBDC tại KCN Giang Điền.", SortOrder = 9 },
            new() { Slug = "nbdc-office", ImageUrl = "/images/upload/projects/nbdc-office/0968f704869a4799a1af1b267c3e0e4f.png", GalleryJson = JsonSerializer.Serialize(new[] { "/images/upload/projects/nbdc-office/9ec1fdc08b29432faa233840af179caf.png", "/images/upload/projects/nbdc-office/ea1844918a8945b48298c025aa3a3494.png", "/images/upload/projects/nbdc-office/a0b49107270d439ca10fc76b9ea68a73.png", "/images/upload/projects/nbdc-office/d417a9cc79e742749e3974c19d7ea6d1.png", "/images/upload/projects/nbdc-office/83a6c09fc99140d1ad1957506099308c.png" }), Name = "NBDC Office", Client = "Công ty TNHH NBDC VN", Location = "KCN Giang Điền, Đồng Nai", Scale = "", Scope = "Thiết kế", Status = "ongoing", Year = "2024", Category = "Văn phòng", Description = "Thiết kế văn phòng điều hành cho NBDC tại KCN Giang Điền.", SortOrder = 10 },
            new() { Slug = "nha-may-ttp", ImageUrl = "/images/upload/projects/nha-may-ttp/d3ac14d62402491cbe14ff40ca861a32.png", GalleryJson = JsonSerializer.Serialize(new[] { "/images/upload/projects/nha-may-ttp/bde2da8736a84ac39147205f0de2217b.png", "/images/upload/projects/nha-may-ttp/e911665b790143c4822e8989efce9a57.png", "/images/upload/projects/nha-may-ttp/3790cf58c0e44a3c8caf65f41b22c642.png", "/images/upload/projects/nha-may-ttp/dfc12213b5764330af23f70d8278dd1b.png", "/images/upload/projects/nha-may-ttp/bf422326691a4eee8e7aec94522daa2f.png", "/images/upload/projects/nha-may-ttp/7b047e191dcc4f6fad88f58ce0b84179.png", "/images/upload/projects/nha-may-ttp/0d151b0786e1434988ae4bddaf2317df.png", "/images/upload/projects/nha-may-ttp/000d52f656c1404a80774c68c5bad82d.png", "/images/upload/projects/nha-may-ttp/edf5ceeccf0c4e9281bac0f1471d62f0.png", "/images/upload/projects/nha-may-ttp/6534b3a11ea6439490b3d2e0d01ce261.png", "/images/upload/projects/nha-may-ttp/055c074a3c5c44dca3995fc52944d1a5.png", "/images/upload/projects/nha-may-ttp/abe0c57c18854907bb9a88ccbf684c55.png", "/images/upload/projects/nha-may-ttp/af9763f086a341a79bc21ae74a860942.png", "/images/upload/projects/nha-may-ttp/08445805ffba44b3b58eb47c2da2a895.png" }), Name = "Nhà Máy Tân Toàn Phát", Client = "Lam Hiệp Hưng & Tân Toàn Phát", Location = "Tỉnh Bình Dương", Scale = "250.000 m²", Scope = "Thiết kế", Status = "ongoing", Year = "2024", Category = "Nhà máy công nghiệp", Description = "Nhà máy thuộc tổ hợp Lâm Hiệp Hưng – Tân Toàn Phát tại Bình Dương.", SortOrder = 11 },
            new() { Slug = "nha-may-lhh-2", ImageUrl = "/images/upload/projects/nha-may-lhh-2/bb73ba8ddcfb49c9806276066bef0f47.png", GalleryJson = JsonSerializer.Serialize(new[] { "/images/upload/projects/nha-may-lhh-2/e34a40d385f149c0a4a38fdd90cf03e6.png", "/images/upload/projects/nha-may-lhh-2/f0200b03c23d4302901620a6b71a22aa.png", "/images/upload/projects/nha-may-lhh-2/85dddff7290d48b9bd6a4501cf899f0d.png", "/images/upload/projects/nha-may-lhh-2/d423b26557bc4670bfa09f6e2269cbc7.png", "/images/upload/projects/nha-may-lhh-2/2cc1f689fd90420eb8616ee4f0f5075b.png", "/images/upload/projects/nha-may-lhh-2/671698ae846340c8a2c316bcd4ad47c8.png", "/images/upload/projects/nha-may-lhh-2/ab6d8ed5cbf0402fb8dee1e8b617f22f.png", "/images/upload/projects/nha-may-lhh-2/55a0692daa88436e8cbd5f4d658d4820.png", "/images/upload/projects/nha-may-lhh-2/49dff0797dc243af9f4bb064bdfe8b2a.png", "/images/upload/projects/nha-may-lhh-2/2f749622af564b0ca6af41f6ad12b88d.png", "/images/upload/projects/nha-may-lhh-2/9d3b229bd6eb497789a23df0a4b234c1.png", "/images/upload/projects/nha-may-lhh-2/64330a663bd74585ac77ff74ccf34580.png", "/images/upload/projects/nha-may-lhh-2/2f9251abeb6443bb974a5437b00b6314.png", "/images/upload/projects/nha-may-lhh-2/51363ee0799240d5a4e414cfeb4ee720.png", "/images/upload/projects/nha-may-lhh-2/106d968ed70446eab64d1fe527b9b67d.png", "/images/upload/projects/nha-may-lhh-2/fcfc522812ef4f94aa42a1fe33590121.png", "/images/upload/projects/nha-may-lhh-2/c3454cce12524b2190fb3148709de49d.png", "/images/upload/projects/nha-may-lhh-2/4c8b3c650569462db118ee4ee6445b31.png", "/images/upload/projects/nha-may-lhh-2/8ff0aecf687f43aab8edbfd2337897ad.png", "/images/upload/projects/nha-may-lhh-2/810cbfd64fb34124962457e2c98875c4.png", "/images/upload/projects/nha-may-lhh-2/de9a8bc5922841aca7559494228ae1e5.png", "/images/upload/projects/nha-may-lhh-2/ea65bfcb96b44fcfaa09e84db84b36fa.png", "/images/upload/projects/nha-may-lhh-2/d4bd4187df0d4c5c9f6a563fd9cd3755.png", "/images/upload/projects/nha-may-lhh-2/3c676c44350c4e1fa8b9f03473a1a23c.png", "/images/upload/projects/nha-may-lhh-2/13d8e35e68c04c17ae5b44ed9a427d56.png", "/images/upload/projects/nha-may-lhh-2/c9f5d9f296de45e19bab972867c7a906.png", "/images/upload/projects/nha-may-lhh-2/6c5faf3e7f224aa78d83ab1bda49328c.png", "/images/upload/projects/nha-may-lhh-2/e086f39093e24a5dbccc22875a72e6a2.png", "/images/upload/projects/nha-may-lhh-2/adf4804061c848808c8d1ea7508f32c4.png", "/images/upload/projects/nha-may-lhh-2/ce3aac9dc16a4284a5b69859c6355a1f.png", "/images/upload/projects/nha-may-lhh-2/3ce9b2e2b0144e728b18bee16ca124ad.png", "/images/upload/projects/nha-may-lhh-2/9d74de01600b4ed58d015c3a22a0c6d6.png" }), Name = "Nhà Máy Lâm Hiệp Hưng", Client = "Lam Hiệp Hưng", Location = "Tỉnh Bình Dương", Scale = "250.000 m²", Scope = "Thiết kế", Status = "ongoing", Year = "2024", Category = "Nhà máy công nghiệp", Description = "Nhà máy Lâm Hiệp Hưng tại Bình Dương, mở rộng dây chuyền sản xuất.", SortOrder = 12 },
            // ── Completed projects from nicon.vn ──
            new() { Slug = "nha-may-stfood", ImageUrl = "/images/upload/projects/nha-may-stfood/cda9fd6a7aa44389810a3ceaa425dc2c.png", GalleryJson = JsonSerializer.Serialize(new[] { "/images/upload/projects/nha-may-stfood/bee852f31b954eef8672157ec5226501.png", "/images/upload/projects/nha-may-stfood/1196f8fded4749979d123d5cb09deb87.png", "/images/upload/projects/nha-may-stfood/1a950a15315d4d199f1012db6f4f2607.png", "/images/upload/projects/nha-may-stfood/3bcaf8a1bee8431098a75c26cca7e33f.png", "/images/upload/projects/nha-may-stfood/9f9913ca8ad24a7f8ee3a6888ae386fb.png", "/images/upload/projects/nha-may-stfood/cbd4318bc21e45d69a4135998a49446d.png", "/images/upload/projects/nha-may-stfood/b459858ef7d44de3adba585059f457f0.png", "/images/upload/projects/nha-may-stfood/a0db34a70c914ecfbdbd9aafec2fd134.png", "/images/upload/projects/nha-may-stfood/1d88f52fb0cd4430afb339f0de98b5ab.png" }), Name = "Nhà Máy S.T.Food Marketing Việt Nam", Client = "S.T.FOOD MARKETING Vietnam Co. Ltd.", Location = "Đường 24, VSIP II-A, Tân Uyên, Bình Dương", Scale = "20.000 m²", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2022", Category = "Nhà máy công nghiệp", Description = "Nhà máy sản xuất thực phẩm từ chủ đầu tư Thái Lan, thiết kế theo tiêu chuẩn GMP và HACCP.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "20.000 m²" }, new { label = "Tiêu chuẩn", value = "GMP/HACCP" }, new { label = "Thời gian", value = "14 tháng" }, new { label = "Năm", value = "2022" } }), SortOrder = 13 },
            new() { Slug = "medicare-shop", ImageUrl = "/images/upload/projects/medicare-shop/e71a5f2407a345e5b805fc79c22986ab.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/upload/projects/medicare-shop/38943383d50841809e98db1d54625cc6.jpg", "/images/upload/projects/medicare-shop/78bfbeac9f3e428483fd42498dc7a526.jpg", "/images/upload/projects/medicare-shop/3741e942d93d41e98027074e7233e735.jpg", "/images/upload/projects/medicare-shop/f8e6196614594cf68c6c329acee13d14.jpg", "/images/upload/projects/medicare-shop/f90102b9028c4b34b88ef084dd194377.jpg" }), Name = "Medicare Shop", Client = "Medicare Company", Location = "G3, Aeon Mall Bình Dương Canary", Scale = "250 m²", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2020", Category = "Thương mại", Description = "Cửa hàng Medicare tại Aeon Mall Bình Dương Canary.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "250 m²" }, new { label = "Vị trí", value = "Aeon Mall" }, new { label = "Năm", value = "2020" } }), SortOrder = 14 },
            new() { Slug = "nha-may-lhh-completed", ImageUrl = "/images/upload/projects/nha-may-lhh-completed/faf83805e7604efd9a0f06d9cdbeebad.jpeg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/upload/projects/nha-may-lhh-completed/598bf0143f5f49c3941443fbf52777ed.jpg", "/images/upload/projects/nha-may-lhh-completed/e772ffb69c104ca7933193e6e7f801d7.jpg", "/images/upload/projects/nha-may-lhh-completed/fbfe91eb487c4690b333e912084ceaaa.jpg", "/images/upload/projects/nha-may-lhh-completed/54c21b752ef14f52b2ea44dc26332c43.jpg", "/images/upload/projects/nha-may-lhh-completed/6305f991685f4030942d2ab5a28ae3dc.jpg", "/images/upload/projects/nha-may-lhh-completed/4473f9c65c6d48d583f081af41bf857a.jpg" }), Name = "Nhà Máy Lâm Hiệp Hưng (Hoàn thành)", Client = "Lam Hiệp Hưng & Tân Toàn Phát", Location = "Tỉnh Bình Dương", Scale = "18.000 m²", Scope = "Thiết kế", Status = "completed", Year = "2019", Category = "Nhà máy công nghiệp", Description = "Dự án nhà máy Lâm Hiệp Hưng giai đoạn 1 đã hoàn thành.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "18.000 m²" }, new { label = "Năm", value = "2019" } }), SortOrder = 15 },
            new() { Slug = "sctv-office", ImageUrl = "/images/projects/sctv-office/01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/sctv-office/02.jpg" }), Name = "Văn Phòng SCTV", Client = "Đài Truyền hình Cáp Sài Gòn", Location = "Quận 2, TP.HCM", Scale = "4.000 m²", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2020", Category = "Văn phòng", Description = "Thiết kế và thi công văn phòng SCTV tại Quận 2, TP.HCM.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "4.000 m²" }, new { label = "Năm", value = "2020" } }), SortOrder = 16 },
            new() { Slug = "nha-may-hbfuller", ImageUrl = "/images/upload/projects/nha-may-hbfuller/faeb079a35f9449dab62ac3f8b1244a6.jpeg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/upload/projects/nha-may-hbfuller/cc85e652896245b19dc3d1cc9c75ec30.jpg", "/images/upload/projects/nha-may-hbfuller/4d451d0769ea4c949e4aa9d5ae814ea2.jpg", "/images/upload/projects/nha-may-hbfuller/2677dc83cd58400580cc8d49002a1182.jpg", "/images/upload/projects/nha-may-hbfuller/f947e108565d4c13add9a5ce3e2e3349.jpg", "/images/upload/projects/nha-may-hbfuller/03179c25d76a4639899e450a21293b86.jpg", "/images/upload/projects/nha-may-hbfuller/eacea7ff61f84d79b7bb3eac762e7b92.jpg", "/images/upload/projects/nha-may-hbfuller/21790fefb556479982261af269195994.jpg", "/images/upload/projects/nha-may-hbfuller/f65a53ab6b5f4c86b71a1dbcacf8035d.jpg", "/images/upload/projects/nha-may-hbfuller/b41cb0867ea3462db5e9237702c06eb4.jpg", "/images/upload/projects/nha-may-hbfuller/2742b73f494141c19b65eb2dc93b9a2d.jpg", "/images/upload/projects/nha-may-hbfuller/17b00980cb774d6a8291fd0069270c33.jpg" }), Name = "Nhà Máy H.B.Fuller", Client = "H.B.Fuller Co., Ltd.", Location = "Tỉnh Bình Dương", Scale = "", Scope = "MEP", Status = "completed", Year = "2019", Category = "Nhà máy công nghiệp", Description = "Thi công hệ thống MEP cho nhà máy H.B.Fuller tại Bình Dương.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Phạm vi", value = "MEP" }, new { label = "Năm", value = "2019" } }), SortOrder = 17 },
            new() { Slug = "red-bull-expansion", ImageUrl = "/images/upload/projects/red-bull-expansion/946bd19ff0454c4e812aa208551c3555.png", GalleryJson = JsonSerializer.Serialize(new[] { "/images/upload/projects/red-bull-expansion/139ca878202844ea99beb483cff9b083.png", "/images/upload/projects/red-bull-expansion/d83267731ed54b8998df4fa51bfb23d8.png", "/images/upload/projects/red-bull-expansion/14ba21f1a76a4f43b66e74977569e8eb.png", "/images/upload/projects/red-bull-expansion/8e86d56306814c588636ef79d8270537.jpg", "/images/upload/projects/red-bull-expansion/85e8580b46b54117b044847f828e1275.png", "/images/upload/projects/red-bull-expansion/c920ab5fd0a34398b4a521bfed7ca2cc.png", "/images/upload/projects/red-bull-expansion/34b6bca579cd4572b9c3a8c4d7d674a1.png", "/images/upload/projects/red-bull-expansion/f1a4efa704a146eab70bbd8c5c814606.png", "/images/upload/projects/red-bull-expansion/03ad0b5ba89d472c952e89fc9fb1a27f.png", "/images/upload/projects/red-bull-expansion/e2f1a6bcce294e02af7d5ce07dc1875f.png", "/images/upload/projects/red-bull-expansion/665d68bd3d8142648131c3e5dfdfdea5.png" }), Name = "Dự Án Mở Rộng Red Bull", Client = "Red Bull (Việt Nam) Co., Ltd", Location = "Xa lộ Hà Nội, Bình Thắng, Dĩ An, Bình Dương", Scale = "2.000 m²", Scope = "Thiết kế", Status = "completed", Year = "2024", Category = "Nhà máy công nghiệp", Description = "Thiết kế mở rộng nhà máy Red Bull tại Bình Dương, giữ nguyên bản sắc thương hiệu.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "2.000 m²" }, new { label = "Năm", value = "2024" } }), SortOrder = 18 },
            new() { Slug = "nha-may-great-lotus", ImageUrl = "/images/upload/projects/nha-may-great-lotus/2458ac06eb5e4a3885e9512e3312590a.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/upload/projects/nha-may-great-lotus/773b8cd563fc4a9dbc527f6202904b7b.jpg", "/images/upload/projects/nha-may-great-lotus/b62c94651b2f4492aafbc5934dcde2e3.jpg", "/images/upload/projects/nha-may-great-lotus/b5db598165624afdb66500f84c57ed48.jpg", "/images/upload/projects/nha-may-great-lotus/b5112f38ddda446890c4e153c97d904e.jpg", "/images/upload/projects/nha-may-great-lotus/98a22058dd014592863e8325fb420217.jpg", "/images/upload/projects/nha-may-great-lotus/a7a8f15a78c9408a8546dcb7e3a531e8.jpg", "/images/upload/projects/nha-may-great-lotus/d5db7ebab1c34f98a5e9bfc562b2be3c.jpg", "/images/upload/projects/nha-may-great-lotus/fc11f14c1bfa415282dd1524a1e8162e.jpg", "/images/upload/projects/nha-may-great-lotus/13d6d2845caf4de08e2d471c78e2e239.jpg" }), Name = "Nhà Máy Great Lotus Việt Nam", Client = "Great Lotus Manufacturing Vietnam Co. Ltd.", Location = "VSIP II-A, Tân Uyên, Bình Dương", Scale = "31.187 m²", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2019", Category = "Nhà máy công nghiệp", Description = "Thiết kế và thi công nhà máy Great Lotus với quy mô hơn 31.000 m².", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "31.187 m²" }, new { label = "Năm", value = "2019" } }), SortOrder = 19 },
            new() { Slug = "nha-may-advanced-casting", ImageUrl = "/images/upload/projects/nha-may-advanced-casting/eec80ff8243847878455a40733c6f29b.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/upload/projects/nha-may-advanced-casting/2e208248bec047b7ab9e4c126df3e3c3.jpg", "/images/upload/projects/nha-may-advanced-casting/9dcbc8b999e64a99bde311c84f5c067a.jpg", "/images/upload/projects/nha-may-advanced-casting/441eefe239fe4242a9820db0330e2f50.jpg", "/images/upload/projects/nha-may-advanced-casting/e2a81aa908a54e3990e06f7ad54ebd46.jpg", "/images/upload/projects/nha-may-advanced-casting/c9a39a2405fe4ffe9df268e627e8faf9.jpg", "/images/upload/projects/nha-may-advanced-casting/2e313f20500242ec8a7ab388e8647232.jpg", "/images/upload/projects/nha-may-advanced-casting/000f6dbfa8504c358cddc0d893336905.jpg", "/images/upload/projects/nha-may-advanced-casting/2c2ee5c38262483b85ef17cae86f6197.jpg", "/images/upload/projects/nha-may-advanced-casting/c209d11b83254cbbb8de0eb22f0dec45.jpg" }), Name = "Nhà Máy Advanced Casting Asia", Client = "Advanced Casting Asia Co. Ltd.", Location = "VSIP II-A, Tân Uyên, Bình Dương", Scale = "15.000 m²", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2018", Category = "Nhà máy công nghiệp", Description = "Nhà máy sản xuất Advanced Casting Asia tại VSIP II-A.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "15.000 m²" }, new { label = "Năm", value = "2018" } }), SortOrder = 20 },
            new() { Slug = "sctv-studio", ImageUrl = "/images/upload/projects/sctv-studio/57be138f70a945d1ad4dc407e8fd42b6.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/upload/projects/sctv-studio/b77c49d835e0444f8248d7be356b8bd5.jpg", "/images/upload/projects/sctv-studio/6c017df287be4c659b8cdc711a9aabc0.jpg", "/images/upload/projects/sctv-studio/c0e08ce9d2c0438c80b36aff4d1b3d78.jpg", "/images/upload/projects/sctv-studio/115846e571f94fcc8a20f0c8560c53e8.jpg", "/images/upload/projects/sctv-studio/4e35b66ba5f04a30b3e0211837f7bad4.jpg" }), Name = "SCTV Studio & Văn Phòng", Client = "Đài Truyền hình Cáp Sài Gòn", Location = "Quận 2, TP.HCM", Scale = "", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2018", Category = "Studio", Description = "Trường quay truyền hình quy mô lớn nhất Việt Nam tại thời điểm xây dựng.", SortOrder = 21 },
            new() { Slug = "nha-may-bkl", ImageUrl = "/images/upload/projects/nha-may-bkl/b8abb6350bc14bc5b482aa8f98744969.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/upload/projects/nha-may-bkl/158dc00900f24557a0e07f309a61ca8f.jpg", "/images/upload/projects/nha-may-bkl/4953373599ac421f845b6e1f1da168bc.jpg" }), Name = "Nhà Máy BKL", Client = "BKL International Ltd., Co", Location = "KCN Thịnh Phát, Bến Lức, Long An", Scale = "5.000 m²", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2018", Category = "Nhà máy công nghiệp", Description = "Thiết kế và thi công nhà máy BKL tại KCN Thịnh Phát.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "5.000 m²" }, new { label = "Năm", value = "2018" } }), SortOrder = 22 },
            new() { Slug = "nha-may-rebisco", ImageUrl = "/images/upload/projects/nha-may-rebisco/8ca15c435f7e49e0be4faf350d125950.jpeg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/upload/projects/nha-may-rebisco/78dbaf451e7c4f1f808a764c9714143a.jpg", "/images/upload/projects/nha-may-rebisco/31cc9d22f009485e895dd86147ca2bfa.jpg", "/images/upload/projects/nha-may-rebisco/bec1a67d84cb4f6bb7d94a6e43c5b8f2.jpg", "/images/upload/projects/nha-may-rebisco/e8e0710760144548a738cdacc3b52a6b.jpg", "/images/upload/projects/nha-may-rebisco/5c7001b5b0064e7197bbcc7ae5919ed8.jpg" }), Name = "Nhà Máy Rebisco", Client = "Republic Biscuit Corporation", Location = "VSIP II-A, Tân Uyên, Bình Dương", Scale = "20.000 m²", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2017", Category = "Nhà máy công nghiệp", Description = "Nhà máy sản xuất bánh kẹo Rebisco (Philippines) tại VSIP II-A.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "20.000 m²" }, new { label = "Năm", value = "2017" } }), SortOrder = 23 },
            new() { Slug = "nha-may-nestle", ImageUrl = "/images/upload/projects/nha-may-nestle/3046bd2d5d7046cfa883da553f6b1f93.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/upload/projects/nha-may-nestle/be5a7ee88a164f91864dea09d82523e1.jpg", "/images/upload/projects/nha-may-nestle/5f3c76aac4fd442bad1f7b5902a0a210.jpg", "/images/upload/projects/nha-may-nestle/16610db89b1c495eb1a8ae200952e1a2.jpg" }), Name = "Nhà Máy & Văn Phòng Nestlé Bình An", Client = "Nestlé Việt Nam", Location = "KCN Biên Hòa II, Đồng Nai", Scale = "", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2015", Category = "Nhà máy công nghiệp", Description = "Thiết kế và thi công nhà máy và văn phòng Nestlé Bình An.", SortOrder = 24 },
            new() { Slug = "nha-may-ampharco", ImageUrl = "/images/upload/projects/nha-may-ampharco/74a9ac8d86444e81a50b68dc2a9263ad.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/upload/projects/nha-may-ampharco/7454a80f1359442ea206ce0f16994623.jpg" }), Name = "Nhà Máy Ampharco U.S.A", Client = "Ampharco U.S.A", Location = "KCN Nhơn Trạch 3, Đồng Nai", Scale = "", Scope = "Thi công", Status = "completed", Year = "2016", Category = "Nhà máy dược phẩm", Description = "Thi công nhà máy dược phẩm Ampharco U.S.A tại Nhơn Trạch.", SortOrder = 25 },
            new() { Slug = "konimiyaki-restaurant", ImageUrl = "/images/upload/projects/konimiyaki-restaurant/79e3aa0158854a43adcad7e5a4db4774.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/upload/projects/konimiyaki-restaurant/3f75732862fe4437b7230d35d4047d62.jpg", "/images/upload/projects/konimiyaki-restaurant/2915b06fb49645218b35f3037af57aec.jpg", "/images/upload/projects/konimiyaki-restaurant/56f073c59b4c493a8b074fe29da8489b.jpg", "/images/upload/projects/konimiyaki-restaurant/9a08355dd5b04b09bbddbe7415513781.jpg", "/images/upload/projects/konimiyaki-restaurant/8e13069c2d4242d0b4b2ecc434e553bc.jpg" }), Name = "Nhà Hàng Konimiyaki", Client = "Konimiyaki Restaurant", Location = "Quận 1, TP.HCM", Scale = "400 m²", Scope = "Thiết kế", Status = "completed", Year = "2018", Category = "Nhà hàng", Description = "Thiết kế nhà hàng Konimiyaki tại Quận 1, TP.HCM.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "400 m²" }, new { label = "Năm", value = "2018" } }), SortOrder = 26 },
            new() { Slug = "nha-may-scon", ImageUrl = "/images/upload/projects/nha-may-scon/53da8327bbf946be901d1cdf5450875a.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/upload/projects/nha-may-scon/a0d7f1b7bf144ad385e12c0dab2e254a.jpg", "/images/upload/projects/nha-may-scon/0e5f464764d9429787c9cf969c8931ee.jpg", "/images/upload/projects/nha-may-scon/73327ba2f4ae49c4bcf6c31573ad2d15.jpg", "/images/upload/projects/nha-may-scon/e6227ab2237e4ea58a39200c9a1c8ddd.jpg", "/images/upload/projects/nha-may-scon/3fb553cd3fd94689ade69ee2b6f783c5.jpg", "/images/upload/projects/nha-may-scon/01e1fb5312904c749f025418d294b05b.jpg", "/images/upload/projects/nha-may-scon/0ef84e9c925e40e3871049c09baba318.jpg", "/images/upload/projects/nha-may-scon/08e8ac1a21b94c2fa7288b126096a985.jpg" }), Name = "Nhà Máy SCON", Client = "SCON Co. Ltd.", Location = "VSIP II-A, Tân Uyên, Bình Dương", Scale = "8.337 m²", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2018", Category = "Nhà máy công nghiệp", Description = "Thiết kế và thi công nhà máy SCON tại VSIP II-A.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "8.337 m²" }, new { label = "Năm", value = "2018" } }), SortOrder = 27 },
            new() { Slug = "nha-may-clotex", ImageUrl = "/images/upload/projects/nha-may-clotex/e234dc928e014871aa80895f3ee70ec5.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/upload/projects/nha-may-clotex/ac8dacd961654ec5bbef1dc128fe11e6.jpg", "/images/upload/projects/nha-may-clotex/c7c582671a3145f582f65a8ee5b3d3dd.jpg", "/images/upload/projects/nha-may-clotex/50917776c7bb4300849c228a46455178.jpg", "/images/upload/projects/nha-may-clotex/d6a44b656eab4ba3bba3297a4a7a2a14.jpg", "/images/upload/projects/nha-may-clotex/0f715231b47c466daae0fa7120ab10ac.jpg", "/images/upload/projects/nha-may-clotex/36a9190bd10d4742bba30fb45db38da4.jpg", "/images/upload/projects/nha-may-clotex/41fa39768fa24302aa1f9e2783489b47.jpg", "/images/upload/projects/nha-may-clotex/0b374c4cfc304855a056693673a6f01b.jpg", "/images/upload/projects/nha-may-clotex/af1a5427e95245d1a95fb91a584410dc.jpg" }), Name = "Nhà Máy Clotex Labels Việt Nam", Client = "Clotex Labels (VN) Co. Ltd.", Location = "VSIP II-A, Tân Uyên, Bình Dương", Scale = "8.565 m²", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2018", Category = "Nhà máy công nghiệp", Description = "Nhà máy nhãn mác Clotex Labels Vietnam tại VSIP II-A.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "8.565 m²" }, new { label = "Năm", value = "2018" } }), SortOrder = 28 },
            new() { Slug = "nha-may-amiba", ImageUrl = "/images/upload/projects/nha-may-amiba/557d05cd0d7e4cdea22e7aabdc567dc3.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/upload/projects/nha-may-amiba/0cd106e5a02a4fb8bc260c9a7fcf1baa.jpg", "/images/upload/projects/nha-may-amiba/12449444288642fbbeee4010e084ef6d.jpg", "/images/upload/projects/nha-may-amiba/b280d972663540e2970800ea1cd9caa6.jpg", "/images/upload/projects/nha-may-amiba/413e6db3daa747cab14ab61fc9aae8fc.jpg", "/images/upload/projects/nha-may-amiba/ea23536b47f44713b64dbbf0f3d146a8.jpg", "/images/upload/projects/nha-may-amiba/b984f05cec264e73bfdd3183a6af7a16.jpg" }), Name = "Nhà Máy Amiba", Client = "Amiba Vietnam Company Limited", Location = "VSIP II-A, Tân Uyên, Bình Dương", Scale = "20.000 m²", Scope = "Thi công", Status = "completed", Year = "2018", Category = "Nhà máy công nghiệp", Description = "Thi công nhà máy Amiba với diện tích 2 hecta tại VSIP II-A.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "20.000 m²" }, new { label = "Năm", value = "2018" } }), SortOrder = 29 },
            new() { Slug = "nha-may-akati-wood", ImageUrl = "/images/upload/projects/nha-may-akati-wood/8882cb3c35a3444894209cf69080b84e.jpeg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/upload/projects/nha-may-akati-wood/3ad39350da5e4f74b4125b2f89422626.jpg", "/images/upload/projects/nha-may-akati-wood/e14d2034c44b4583bc8de80800fedf78.jpg", "/images/upload/projects/nha-may-akati-wood/7149a359274e40df99f408cba68404d3.jpg", "/images/upload/projects/nha-may-akati-wood/a6a4d8cdecc548658616c98af52e60b5.jpg", "/images/upload/projects/nha-may-akati-wood/6988e8280db543879eaa855628c20404.jpg" }), Name = "Nhà Máy Akati Wood", Client = "Akati Dominant (Malaysia)", Location = "Bình Dương", Scale = "", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2016", Category = "Nhà máy công nghiệp", Description = "Nhà máy gỗ Akati Wood, chi nhánh của Akati Dominant từ Malaysia.", SortOrder = 30 },
            new() { Slug = "nha-may-japan-plus", ImageUrl = "/images/upload/projects/nha-may-japan-plus/a8e0a632ba5e453aaa7c3fc4effec433.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/upload/projects/nha-may-japan-plus/d30e433c45e04357b8e3280cba6a3141.jpg", "/images/upload/projects/nha-may-japan-plus/a78b2f6761d14ff1b1c627b6384196e6.jpg", "/images/upload/projects/nha-may-japan-plus/9e1c7ab7f071468aadcacafcd23b59f7.jpg", "/images/upload/projects/nha-may-japan-plus/ceef12bcd2ef4a518a3b2a28b0ad0f23.jpg" }), Name = "Nhà Máy Japan Plus", Client = "Japan Plus (Nhật Bản)", Location = "KCN Đông Nam Củ Chi", Scale = "", Scope = "Thi công", Status = "completed", Year = "2016", Category = "Nhà máy công nghiệp", Description = "Nhà máy Japan Plus sản xuất hộp PE tại KCN Đông Nam Củ Chi.", SortOrder = 31 },
            new() { Slug = "duoc-pham-trung-uong", ImageUrl = "/images/upload/projects/duoc-pham-trung-uong/327c757975ff4a93b7de4710a7421998.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/upload/projects/duoc-pham-trung-uong/7ed62ca0ffa24b2897a299a49b24e4e6.jpeg", "/images/upload/projects/duoc-pham-trung-uong/d164e0c8eeb94f429c38b06f0b326141.jpeg", "/images/upload/projects/duoc-pham-trung-uong/ea2cab9f0e7c4d39981e46dccd194acb.jpg", "/images/upload/projects/duoc-pham-trung-uong/98cee3bee8d940d68dbe8fa780ef6f20.jpeg", "/images/upload/projects/duoc-pham-trung-uong/5793986fd92743149746bdd8fd9dcd88.jpg", "/images/upload/projects/duoc-pham-trung-uong/e89784e1ab14444ea877c60f901876d6.jpeg", "/images/upload/projects/duoc-pham-trung-uong/367bc75a16004208a1f1e7deaf0dd6ed.jpg", "/images/upload/projects/duoc-pham-trung-uong/dc024f26b386431aa4a91740636e6ea4.jpg", "/images/upload/projects/duoc-pham-trung-uong/a57201cfeae14cffac920c386eaeeed3.jpeg", "/images/upload/projects/duoc-pham-trung-uong/c6fd8d51319a4c2694c3ff546f315acc.jpeg", "/images/upload/projects/duoc-pham-trung-uong/1f6298f1cc3a43a49f0357815f57c695.jpeg", "/images/upload/projects/duoc-pham-trung-uong/cad60168eaab4970a9c42308bfd59a11.jpg", "/images/upload/projects/duoc-pham-trung-uong/5bf9ce3df2be4839b1a9c86aeca08ac4.jpg", "/images/upload/projects/duoc-pham-trung-uong/396af416e82b4936bbf9fd70d471b9a4.jpg" }), Name = "Dược Phẩm Trung Ương TP.HCM", Client = "Công ty TNHH Dược Phẩm Trung Ương 1", Location = "TP.HCM", Scale = "", Scope = "Thiết kế", Status = "completed", Year = "2020", Category = "Nhà máy dược phẩm", Description = "Thiết kế nhà máy dược phẩm Trung Ương tại TP.HCM.", SortOrder = 32 },
            new() { Slug = "kumgang-office", ImageUrl = "/images/upload/projects/kumgang-office/8361a84a0d7a408ead145a5f1a685326.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/upload/projects/kumgang-office/1a0bde82e3c543b791b5a071ee95201a.jpg", "/images/upload/projects/kumgang-office/7c58755b9e9543aeb00ca311a45a8d69.jpg", "/images/upload/projects/kumgang-office/19e9849507364d31b42b94555758c3a3.jpg" }), Name = "Văn Phòng Kumgang", Client = "KUMGANG VINA CO., LTD", Location = "KCN Giang Điền, Trảng Bom, Đồng Nai", Scale = "180 m²", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2018", Category = "Văn phòng", Description = "Thiết kế và thi công văn phòng Kumgang Vina.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "180 m²" }, new { label = "Năm", value = "2018" } }), SortOrder = 33 },
            new() { Slug = "nha-may-vda-hcm", ImageUrl = "/images/upload/projects/nha-may-vda-hcm/c34a263971114a36899c781c8b47bc14.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/upload/projects/nha-may-vda-hcm/86cabd12910a4cf3a8a8c5a146c4b0f4.jpg", "/images/upload/projects/nha-may-vda-hcm/95c9e85322e748e4afe998103046111d.jpg", "/images/upload/projects/nha-may-vda-hcm/b72713fe21aa4f51bb9a8250483e3db7.jpg", "/images/upload/projects/nha-may-vda-hcm/ee006e8a20274c27891a9371f35215d2.jpg", "/images/upload/projects/nha-may-vda-hcm/49b270b3ce2a41eab161466d5dd9caca.jpg", "/images/upload/projects/nha-may-vda-hcm/188e793a2f74417e828a990da644a092.jpg" }), Name = "Nhà Máy VDA-HCM", Client = "VDA-HCM", Location = "KCN Cầu Tràm, Cần Đước, Long An", Scale = "", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2017", Category = "Nhà máy công nghiệp", Description = "Thiết kế và thi công nhà máy VDA-HCM tại KCN Cầu Tràm.", SortOrder = 34 },
            new() { Slug = "thu-thiem-dragon", ImageUrl = "/images/upload/projects/thu-thiem-dragon/42f22b3e33af491588cf066aace6d635.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/upload/projects/thu-thiem-dragon/78a0408fcad24106896051ab67e93d7e.jpg", "/images/upload/projects/thu-thiem-dragon/9a7e45538aca47c0b6bcb7b319ced9c8.jpg", "/images/upload/projects/thu-thiem-dragon/3b984a4ab5274c4b870c5ab2b29616be.jpeg", "/images/upload/projects/thu-thiem-dragon/f0f67a4a65c34633af1ead20849add2a.jpeg", "/images/upload/projects/thu-thiem-dragon/d1572eaf23414d2c949b7dfb173dfe48.jpeg", "/images/upload/projects/thu-thiem-dragon/2c01ac32cf8b40449cf89c9aa6ba568b.jpeg", "/images/upload/projects/thu-thiem-dragon/952f7f0c81d24a3ea5b0fc78ddfbe4cf.jpeg", "/images/upload/projects/thu-thiem-dragon/241963982dd845bc9bc4b7091b152fc0.jpeg" }), Name = "Thu Thiêm Dragon Show Flat", Client = "Thu Thiêm Group", Location = "Quận 2, TP.HCM", Scale = "", Scope = "Thi công", Status = "completed", Year = "2015", Category = "Bất động sản", Description = "Thi công căn hộ mẫu Thu Thiêm Dragon tại Quận 2.", SortOrder = 35 },
            new() { Slug = "nha-may-nam-ha-viet", ImageUrl = "/images/upload/projects/nha-may-nam-ha-viet/29dadb3400d34716bdcda35dd2cac379.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/upload/projects/nha-may-nam-ha-viet/148a04bc6a584e07bd07f228e13d5844.jpg", "/images/upload/projects/nha-may-nam-ha-viet/1fad17b1b5564991a0bc1959c6f19f12.jpg" }), Name = "Nhà Máy Nam Hà Việt", Client = "Nam Hà Việt Co., Ltd.", Location = "KCN Rạch Bắp, Bến Cát, Bình Dương", Scale = "", Scope = "Thiết kế", Status = "completed", Year = "2020", Category = "Nhà máy công nghiệp", Description = "Nhà máy sản xuất que hàn Nam Hà Việt.", SortOrder = 36 },
            new() { Slug = "nha-may-yc-tec", ImageUrl = "/images/upload/projects/nha-may-yc-tec/54c2c9a2c2aa45298f2c0d650f2431ba.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/upload/projects/nha-may-yc-tec/df4aa100a979482dab7ce31da4b12edb.jpg", "/images/upload/projects/nha-may-yc-tec/87882ca07ecc4482a2c5c9a752778100.jpg" }), Name = "Nhà Máy YC TEC", Client = "YC TEC Group", Location = "KCN Sóng Thần II, Dĩ An, Bình Dương", Scale = "", Scope = "Thiết kế", Status = "completed", Year = "2020", Category = "Nhà máy công nghiệp", Description = "Thiết kế nhà máy YC TEC tại KCN Sóng Thần II.", SortOrder = 37 },
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
            new() { Slug = "semivina-nissi-factory", ImageUrl = "/images/upload/projects/semivina-nissi-factory/c8e28129bfe6405bb1b6c3aeacc1f4c7.jpeg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/semivina-nissi-factory/01.jpg", "/images/projects/semivina-nissi-factory/02.jpg", "/images/projects/semivina-nissi-factory/03.jpg", "/images/projects/semivina-nissi-factory/04.jpg", "/images/upload/projects/semivina-nissi-factory/20b0743d627d4ac5b33ba779bafad0d9.jpg", "/images/upload/projects/semivina-nissi-factory/64bb70f35b77487b8ea8b2aeae53a1fd.jpg" }), Name = "Nhà Máy Semivina – Nissi", Client = "Semivina – Nissi (Hàn Quốc)", Location = "VSIP II, Bình Dương", Scale = "", Scope = "Thiết kế và Thi công", Status = "ongoing", Year = "2024", Category = "Nhà máy công nghiệp", Description = "Nhà máy thiết bị chiếu sáng của nhà đầu tư Hàn Quốc tại VSIP II.", SortOrder = 53 },
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
            new() { Slug = "ky-tuc-xa-soul-gear", ImageUrl = "/images/upload/projects/ky-tuc-xa-soul-gear/d433f179102f43789788a089f48136f1.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/ky-tuc-xa-soul-gear/img-01.jpg" }), Name = "Ký Túc Xá Soul Gear", Client = "", Location = "", Scale = "", Scope = "Thiết kế", Status = "completed", Year = "", Category = "Nhà ở", Description = "Khách hàng: Công ty Soul Gear VINA (Hàn Quốc)", SortOrder = 67 },
            new() { Slug = "nha-may-js", ImageUrl = "/images/projects/nha-may-js/img-01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/nha-may-js/img-02.jpg", "/images/projects/nha-may-js/img-03.jpg", "/images/projects/nha-may-js/img-04.jpg" }), Name = "Nhà Máy JS", Client = "", Location = "", Scale = "", Scope = "Thiết kế", Status = "completed", Year = "", Category = "Nhà máy công nghiệp", Description = "Địa điểm: Mỹ Phước 2 Khu công nghiệp, Tỉnh Bình Dương", SortOrder = 68 },
            new() { Slug = "truong-quoc-te-acg", ImageUrl = "/images/projects/truong-quoc-te-acg/img-01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/truong-quoc-te-acg/img-02.jpg", "/images/projects/truong-quoc-te-acg/img-03.jpg", "/images/projects/truong-quoc-te-acg/img-04.jpg" }), Name = "Trường Quốc Tế ACG", Client = "", Location = "", Scale = "", Scope = "Thiết kế", Status = "completed", Year = "", Category = "Giáo dục", Description = "Địa điểm: Quận 2, TP Hồ Chí Minh", SortOrder = 69 },
            new() { Slug = "khach-san-eden", ImageUrl = "/images/projects/khach-san-eden/img-01.jpg", Name = "Khách Sạn Eden", Client = "", Location = "", Scale = "", Scope = "Thiết kế", Status = "completed", Year = "", Category = "Khách sạn", Description = "Địa điểm: 60 Mai Xuân Thưởng, Thành phố Quy Nhơn", SortOrder = 70 },
            new() { Slug = "nha-may-dream-mekong", ImageUrl = "/images/projects/nha-may-dream-mekong/img-01.jpg", Name = "Nhà Máy Dream Mekong", Client = "", Location = "", Scale = "", Scope = "Thiết kế", Status = "completed", Year = "", Category = "Nhà máy công nghiệp", Description = "Khách hàng: Dream Mekong Viet nam (Hàn Quốc)", SortOrder = 71 },
            new() { Slug = "long-khanh-hotel-resort", ImageUrl = "/images/projects/long-khanh-hotel-resort/img-01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/long-khanh-hotel-resort/img-02.jpg" }), Name = "Long Khánh Hotel & Resort", Client = "", Location = "", Scale = "", Scope = "Thiết kế", Status = "completed", Year = "", Category = "Khách sạn", Description = "Khách hàng: Long Khánh Hotel & Resort", SortOrder = 72 },
            new() { Slug = "suzuki-showroom", ImageUrl = "/images/projects/suzuki-showroom/img-01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/suzuki-showroom/img-02.jpg", "/images/projects/suzuki-showroom/img-03.jpg", "/images/projects/suzuki-showroom/img-04.jpg" }), Name = "Suzuki Showroom", Client = "", Location = "", Scale = "", Scope = "Thiết kế", Status = "completed", Year = "", Category = "Thương mại", Description = "Địa điểm: Quận 5, TP Hồ Chí Minh", SortOrder = 73 },
            new() { Slug = "kv-battery", ImageUrl = "/images/projects/kv-battery-2/img-01.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/kv-battery-2/img-02.jpg" }), Name = "K&V Battery", Client = "", Location = "", Scale = "", Scope = "Thiết kế", Status = "completed", Year = "", Category = "Nhà máy công nghiệp", Description = "Khách hàng: K & V Battery Co., LTD (Hàn Quốc)", SortOrder = 74 },

        };

        var newItems = items.Where(p => !existingSlugs.Contains(p.Slug)).ToArray();
        if (newItems.Length > 0)
        {
            db.Projects.AddRange(newItems);
            db.SaveChanges();
        }

        var uploadBySlug = items
            .Where(p => p.ImageUrl != null && p.ImageUrl.StartsWith("/images/upload/"))
            .ToDictionary(p => p.Slug);
        if (uploadBySlug.Count > 0)
        {
            var uploadSlugs = uploadBySlug.Keys.ToList();
            var staleProjects = db.Projects
                .Where(p => uploadSlugs.Contains(p.Slug) &&
                            (p.ImageUrl == null || !p.ImageUrl.StartsWith("/images/upload/")))
                .ToList();
            foreach (var project in staleProjects)
            {
                if (uploadBySlug.TryGetValue(project.Slug, out var seed))
                {
                    project.ImageUrl = seed.ImageUrl;
                    if (project.GalleryJson == null || !project.GalleryJson.Contains("/images/upload/"))
                        project.GalleryJson = seed.GalleryJson;
                    project.UpdatedAt = DateTime.UtcNow;
                }
            }
            if (staleProjects.Count > 0)
                db.SaveChanges();
        }
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
                    if (existing.TryGetValue(key, out var row))
                    {
                        if (row.Value != value)
                        {
                            var tracked = db.EntityTranslations.Find(row.Id);
                            if (tracked != null) { tracked.Value = value; tracked.UpdatedAt = now; }
                        }
                    }
                    else
                    {
                        toAdd.Add(new EntityTranslation
                        {
                            EntityType = EntityTypes.Project,
                            EntityId = entityId,
                            FieldName = field,
                            LanguageCode = lang,
                            Value = value,
                            CreatedAt = now,
                            UpdatedAt = now,
                        });
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
                item.Title           = seed.Title;
                item.ShortTitle      = seed.ShortTitle;
                item.Tagline         = seed.Tagline;
                item.Intro           = seed.Intro;
                item.SectionsJson    = seed.SectionsJson;
                item.HighlightsJson  = seed.HighlightsJson;
                item.IntroBlocksJson = seed.IntroBlocksJson;
                item.SortOrder       = seed.SortOrder;
                item.UpdatedAt       = now;
            }
            else
            {
                db.ServiceItems.Add(new ServiceItem
                {
                    Slug            = seed.Slug,
                    Title           = seed.Title,
                    ShortTitle      = seed.ShortTitle,
                    Tagline         = seed.Tagline,
                    Intro           = seed.Intro,
                    SectionsJson    = seed.SectionsJson,
                    HighlightsJson  = seed.HighlightsJson,
                    IntroBlocksJson = seed.IntroBlocksJson,
                    SortOrder       = seed.SortOrder,
                    CreatedAt       = now,
                    UpdatedAt       = now,
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
            ["Vietnam Golden FDI 2019"]             = "/images/activities/activity-opening.jpg",
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
                item.ImageUrl  = s.ImageUrl;
                item.Title     = s.Title;
                item.Subtitle  = s.Subtitle;
                item.LinkUrl   = s.LinkUrl;
                item.LinkText  = s.LinkText;
                item.IsActive  = s.IsActive;
                item.SortOrder = s.SortOrder;
                item.UpdatedAt = now;
            }
            else
            {
                db.SlideshowItems.Add(new SlideshowItem
                {
                    Slug = s.Slug, ImageUrl = s.ImageUrl, Title = s.Title,
                    Subtitle = s.Subtitle, LinkUrl = s.LinkUrl, LinkText = s.LinkText,
                    IsActive = s.IsActive, SortOrder = s.SortOrder,
                    CreatedAt = now, UpdatedAt = now,
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
                item.Eyebrow    = seed.Eyebrow;
                item.TitleA     = seed.TitleA;
                item.TitleB     = seed.TitleB;
                item.Paragraph1 = seed.Paragraph1;
                item.Paragraph2 = seed.Paragraph2;
                item.ImageUrl   = seed.ImageUrl;
                item.ItemsJson  = seed.ItemsJson;
                item.IsActive   = seed.IsActive;
                item.SortOrder  = seed.SortOrder;
                item.UpdatedAt  = now;
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
