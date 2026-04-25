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
        SeedEntityTranslations(db);
    }

    // ─── Activities (Vietnamese default) ────────────────────────────

    private static void SeedActivities(AppDbContext db)
    {
        if (db.Activities.Any()) return;

        var items = new Activity[]
        {
            new()
            {
                Slug = "dich-vu-xay-dung-tron-goi-nicon", Date = "15.12.2025",
                ImageUrl = "/images/activities/activity-handover.jpg",
                Category = "Dịch vụ", Author = "NICON Editorial",
                Title = "Dịch vụ xây dựng trọn gói NICON — Giải pháp tối ưu cho công trình nhà xưởng, nhà máy và dự án dân dụng",
                Excerpt = "Xây dựng trọn gói là hình thức chủ đầu tư giao toàn bộ quá trình xây nhà cho một đơn vị chuyên nghiệp. Từ khảo sát, lập dự toán, thiết kế, xin phép đến chuẩn bị vật tư, thi công hoàn thiện và bàn giao nhà.",
                ContentJson = JsonSerializer.Serialize(new[] {
                    "Trong bối cảnh thị trường xây dựng ngày càng cạnh tranh, các chủ đầu tư mong muốn rút ngắn thời gian triển khai, kiểm soát chi phí và đảm bảo chất lượng đồng bộ. Dịch vụ xây dựng trọn gói (Design & Build) ra đời nhằm giải quyết toàn diện những yêu cầu đó.",
                    "Tại NICON, chúng tôi đảm nhiệm toàn bộ chuỗi giá trị: từ tư vấn ý tưởng, thiết kế kiến trúc – kết cấu – MEP, xin phép xây dựng, lựa chọn vật tư, đến thi công và bàn giao. Một đầu mối duy nhất chịu trách nhiệm toàn bộ dự án giúp chủ đầu tư tiết kiệm thời gian quản lý và giảm thiểu rủi ro phát sinh.",
                    "Quy trình quản lý dự án theo chuẩn ISO 9001:2015 đảm bảo mọi giai đoạn đều được kiểm soát chất lượng nghiêm ngặt. Đội ngũ kỹ sư trên 20 năm kinh nghiệm trong lĩnh vực nhà máy, nhà xưởng công nghiệp và công trình dân dụng cam kết mang đến giải pháp tối ưu nhất cho mỗi loại hình công trình.",
                    "Với hơn 150 dự án đã hoàn thành tại các khu công nghiệp lớn như VSIP, Hựu Thạnh, Giang Điền, Long An..., NICON tự tin là đối tác tin cậy cho hành trình phát triển của doanh nghiệp bạn.",
                }),
                SortOrder = 0,
            },
            new()
            {
                Slug = "khoi-cong-bma-tay-ninh", Date = "08.11.2025",
                ImageUrl = "/images/activities/activity-ceremony.jpg",
                Category = "Sự kiện", Author = "Phòng Truyền Thông",
                Title = "Khởi công Dự án Nhà máy Bảo Minh Ân Việt Nam tại KCN Hựu Thạnh, Tây Ninh",
                Excerpt = "Ngày 19/10/2025, NICON tự hào chính thức khởi công Dự án Nhà máy Bảo Minh Ân Việt Nam tại KCN Hựu Thạnh, xã Đức Hòa, tỉnh Tây Ninh.",
                ContentJson = JsonSerializer.Serialize(new[] {
                    "Sáng ngày 19/10/2025, lễ khởi công Dự án Nhà máy Bảo Minh Ân Việt Nam đã diễn ra trang trọng tại Khu công nghiệp Hựu Thạnh, xã Đức Hòa, tỉnh Tây Ninh. Dự án đánh dấu bước phát triển mới trong hợp tác chiến lược giữa NICON và tập đoàn Bảo Minh Ân.",
                    "Buổi lễ có sự tham dự của đại diện lãnh đạo địa phương, ban giám đốc chủ đầu tư cùng đội ngũ kỹ sư, công nhân của NICON. Phát biểu tại sự kiện, đại diện chủ đầu tư bày tỏ tin tưởng vào năng lực thi công và quản lý dự án của NICON.",
                    "Nhà máy được thiết kế trên diện tích 15.000 m² với hệ thống dây chuyền sản xuất hiện đại, tuân thủ tiêu chuẩn quốc tế về môi trường và an toàn lao động. Dự kiến hoàn thành và đưa vào vận hành vào quý IV/2026.",
                }),
                SortOrder = 1,
            },
            new()
            {
                Slug = "grand-opening-trimas", Date = "08.11.2025",
                ImageUrl = "/images/activities/activity-opening.jpg",
                Category = "Khánh thành", Author = "Phòng Truyền Thông",
                Title = "Grand Opening — Nhà máy TriMas",
                Excerpt = "Ngày 13/10/2025 vừa qua, Nhà máy TriMas tại KCN VSIP IIA mở rộng, Phường Vĩnh Tân, TP. Hồ Chí Minh chính thức khánh thành trong không khí hân hoan và đầy phấn khởi.",
                ContentJson = JsonSerializer.Serialize(new[] {
                    "Ngày 13/10/2025, lễ khánh thành Nhà máy TriMas đã diễn ra trong không khí trang trọng và hân hoan tại KCN VSIP IIA mở rộng, Phường Vĩnh Tân, TP. Hồ Chí Minh.",
                    "Công trình do NICON đảm nhận vai trò Tổng thầu Thiết kế – Thi công với diện tích 10.000 m², hoàn thành trong 11 tháng đúng tiến độ cam kết. Đây là dấu mốc quan trọng trong quan hệ hợp tác lâu dài giữa NICON và tập đoàn TriMas.",
                    "Nhà máy được trang bị các hệ thống tiên tiến nhất về sản xuất, MEP và xử lý môi trường, đáp ứng tiêu chuẩn LEED Silver.",
                }),
                SortOrder = 2,
            },
            new()
            {
                Slug = "nicon-trien-lam-pccc-2024", Date = "17.08.2024",
                ImageUrl = "/images/activities/activity-ceremony.jpg",
                Category = "Triển lãm", Author = "Phòng Marketing",
                Title = "NICON tại Triển Lãm Quốc Tế Về Kỹ Thuật, Thiết Bị An Toàn, Bảo Vệ, Phòng Cháy Chữa Cháy Lần Thứ 17",
                Excerpt = "NICON tham gia triển lãm quốc tế nhằm cập nhật những công nghệ mới nhất trong lĩnh vực an toàn và phòng cháy chữa cháy.",
                ContentJson = JsonSerializer.Serialize(new[] {
                    "NICON vinh dự góp mặt tại Triển lãm Quốc tế lần thứ 17 về kỹ thuật, thiết bị an toàn, bảo vệ và phòng cháy chữa cháy diễn ra tại Trung tâm Hội chợ Triển lãm Sài Gòn (SECC).",
                    "Đây là cơ hội để NICON tiếp cận những công nghệ PCCC mới nhất, nâng cao năng lực thiết kế và thi công hệ thống an toàn cho các nhà máy, nhà xưởng — một trong những tiêu chí then chốt khi triển khai dự án công nghiệp.",
                    "Đoàn kỹ sư NICON đã có nhiều buổi trao đổi chuyên môn với các nhà cung cấp giải pháp hàng đầu thế giới đến từ Nhật Bản, Đức và Hoa Kỳ.",
                }),
                SortOrder = 3,
            },
            new()
            {
                Slug = "sinh-vien-vgu-tham-quan-stfm", Date = "05.08.2022",
                ImageUrl = "/images/activities/activity-opening.jpg",
                Category = "Sự kiện", Author = "Phòng Hành Chính",
                Title = "Sinh viên trường Đại Học Quốc Tế Việt Đức tham quan công trình STFM",
                Excerpt = "Ngày 04/08/2022, Công ty NICON đã có buổi làm việc, đón tiếp các bạn sinh viên năm 3 ngành Kiến trúc Trường Đại học Quốc tế Việt Đức (VGU).",
                ContentJson = JsonSerializer.Serialize(new[] {
                    "Ngày 04/08/2022, NICON vinh hạnh đón tiếp đoàn sinh viên năm 3 ngành Kiến trúc Trường Đại học Quốc tế Việt Đức (VGU) tham quan công trình S.T.Food Marketing tại KCN VSIP II-A.",
                    "Buổi tham quan giúp các bạn sinh viên có cái nhìn thực tế về quy trình thi công nhà máy công nghiệp, từ kết cấu thép, hệ MEP đến hoàn thiện. Đội ngũ kỹ sư NICON đã trực tiếp giới thiệu, giải đáp các thắc mắc chuyên môn.",
                    "Hoạt động thuộc chương trình hợp tác giữa NICON và VGU nhằm góp phần đào tạo thế hệ kiến trúc sư – kỹ sư tương lai.",
                }),
                SortOrder = 4,
            },
            new()
            {
                Slug = "khoi-cong-stfm-2021", Date = "12.06.2021",
                ImageUrl = "/images/activities/activity-handover.jpg",
                Category = "Khởi công", Author = "Phòng Truyền Thông",
                Title = "Lễ khởi công dự án Nhà máy S.T.Food Marketing Việt Nam",
                Excerpt = "Sáng ngày 09/06/2021 Công ty NICON với cương vị là đơn vị Tổng thầu thiết kế - thi công đã tổ chức buổi Lễ Khởi Công Dự án Nhà máy S.T.FOOD MARKETING của Chủ đầu tư Thái Lan tại KCN VSIP II-A.",
                ContentJson = JsonSerializer.Serialize(new[] {
                    "Sáng ngày 09/06/2021, NICON với vai trò Tổng thầu thiết kế – thi công đã long trọng tổ chức Lễ Khởi Công Dự án Nhà máy S.T.FOOD MARKETING tại KCN VSIP II-A, Bình Dương.",
                    "Dự án có quy mô đầu tư lớn từ chủ đầu tư Thái Lan, đánh dấu sự tin tưởng của các tập đoàn quốc tế đối với năng lực Tổng thầu của NICON.",
                    "Công trình áp dụng các tiêu chuẩn an toàn thực phẩm GMP và HACCP, với thời gian thi công dự kiến 14 tháng.",
                }),
                SortOrder = 5,
            },
            // ── Additional activities from nicon.vn ──
            new()
            {
                Slug = "scholarships-awarding-2021", Date = "23.11.2021",
                ImageUrl = "/images/activities/activity-ceremony.jpg",
                Category = "Cộng đồng", Author = "Phòng Hành Chính",
                Title = "Trao học bổng khuyến học",
                Excerpt = "Cuối năm 2021, Công ty NICON vinh dự trao 35 suất học bổng khuyến học cho học sinh trường THPT Phù Mỹ số 1, Bình Định.",
                ContentJson = JsonSerializer.Serialize(new[] {
                    "Cuối năm 2021, Công ty NICON vinh dự trao tặng 35 suất Học Bổng Khuyến Học cho các em học sinh Trường THPT Phù Mỹ Số 1, huyện Phù Mỹ, tỉnh Bình Định — những em có hoàn cảnh gia đình khó khăn nhưng đạt thành tích học tập xuất sắc.",
                    "NICON xin chúc các em thêm quyết tâm, nỗ lực đạt nhiều thành tích hơn nữa, sau này trở thành công dân tốt đóng góp cho xã hội, gia đình và bản thân.",
                }),
                SortOrder = 6,
            },
            new()
            {
                Slug = "grand-opening-hbfuller", Date = "19.07.2019",
                ImageUrl = "/images/activities/activity-opening.jpg",
                Category = "Khánh thành", Author = "Phòng Truyền Thông",
                Title = "Lễ khánh thành Nhà máy H.B.Fuller Việt Nam",
                Excerpt = "Lễ khánh thành nhà máy H.B.Fuller Việt Nam tại KCN VSIP II-A, Tân Uyên, Bình Dương.",
                ContentJson = JsonSerializer.Serialize(new[] {
                    "Nhà máy H.B.Fuller Việt Nam tại đường 27, KCN VSIP II-A, Tân Uyên, Bình Dương đã chính thức khánh thành.",
                    "Dự án do NICON đảm nhận phần thi công MEP, đáp ứng các tiêu chuẩn quốc tế về an toàn và chất lượng.",
                }),
                SortOrder = 7,
            },
            new()
            {
                Slug = "great-lotus-steel-structure", Date = "20.05.2019",
                ImageUrl = "/images/activities/activity-handover.jpg",
                Category = "Dự án", Author = "Phòng Truyền Thông",
                Title = "NICON hoàn thành kết cấu thép Nhà máy Great Lotus Việt Nam",
                Excerpt = "Ngày 10/05/2019, NICON đã hoàn thành hạng mục kết cấu thép cho Nhà máy Great Lotus Việt Nam.",
                ContentJson = JsonSerializer.Serialize(new[] {
                    "Ngày 10/05/2019, NICON hoàn thành hạng mục kết cấu thép cho Nhà máy Great Lotus Việt Nam tại VSIP II-A, Bình Dương.",
                    "Đây là một trong những dự án quy mô lớn với diện tích hơn 31.000 m², NICON đảm nhận vai trò Tổng thầu Thiết kế – Thi công.",
                }),
                SortOrder = 8,
            },
            new()
            {
                Slug = "nicon-top10-vietnam-brand-2018", Date = "24.01.2019",
                ImageUrl = "/images/activities/activity-ceremony.jpg",
                Category = "Giải thưởng", Author = "Phòng Marketing",
                Title = "Báo Nhật: NICON lọt Top 10 Vietnam Leading Brands 2018",
                Excerpt = "NICON được vinh danh trong Top 10 Thương hiệu Hàng đầu Việt Nam 2018, được đưa tin bởi báo chí Nhật Bản.",
                ContentJson = JsonSerializer.Serialize(new[] {
                    "NICON vinh dự lọt vào Top 10 Vietnam Leading Brands 2018, một giải thưởng uy tín được đưa tin rộng rãi bởi các tờ báo Nhật Bản.",
                    "Giải thưởng này khẳng định vị thế và uy tín của NICON trong lĩnh vực xây dựng công nghiệp tại Việt Nam, đặc biệt trong mắt các nhà đầu tư quốc tế.",
                }),
                SortOrder = 9,
            },
            new()
            {
                Slug = "nicon-mori-strategic-cooperation", Date = "07.07.2018",
                ImageUrl = "/images/activities/activity-ceremony.jpg",
                Category = "Sự kiện", Author = "Phòng Truyền Thông",
                Title = "NICON – Mori: Hợp tác chiến lược",
                Excerpt = "Tập đoàn Mori Construction từ Nhật Bản đã mở rộng hợp tác quốc tế với NICON.",
                ContentJson = JsonSerializer.Serialize(new[] {
                    "Tập đoàn Mori Construction từ Nhật Bản đã chính thức mở rộng hợp tác quốc tế với Công ty NICON.",
                    "Sự hợp tác này mang đến cho NICON tiêu chuẩn kỹ thuật và văn hóa làm việc chuẩn Nhật, nâng cao năng lực cạnh tranh trong lĩnh vực xây dựng công nghiệp.",
                }),
                SortOrder = 10,
            },
            new()
            {
                Slug = "training-improvement-2018", Date = "06.09.2018",
                ImageUrl = "/images/activities/activity-handover.jpg",
                Category = "Đào tạo", Author = "Phòng Hành Chính",
                Title = "Đào tạo nâng cao kỹ năng làm việc và đổi mới sáng tạo",
                Excerpt = "Đào tạo, giáo dục là hoạt động thường xuyên để nâng cao kỹ năng quản lý chất lượng, tiến độ và an toàn thi công.",
                ContentJson = JsonSerializer.Serialize(new[] {
                    "Đào tạo và giáo dục là hoạt động thường xuyên tại NICON nhằm nâng cao kỹ năng quản lý chất lượng, kiểm soát tiến độ và đảm bảo an toàn tại công trường.",
                    "Các khóa đào tạo được tổ chức định kỳ, mời chuyên gia trong và ngoài nước để cập nhật kiến thức và phương pháp mới nhất.",
                }),
                SortOrder = 11,
            },
            new()
            {
                Slug = "nicon-annual-trip-2018", Date = "26.08.2018",
                ImageUrl = "/images/activities/activity-opening.jpg",
                Category = "Văn hóa", Author = "Phòng Hành Chính",
                Title = "Du lịch và Team Building NICON hàng năm",
                Excerpt = "Chuyến du lịch hàng năm của NICON giúp các thành viên có khoảng thời gian vui vẻ, trải nghiệm tuyệt vời sau một năm làm việc.",
                ContentJson = JsonSerializer.Serialize(new[] {
                    "Chuyến du lịch hàng năm của Công ty NICON nhằm giúp tất cả thành viên có khoảng thời gian tốt đẹp, trải nghiệm tuyệt vời sau một năm làm việc vất vả.",
                    "Đây cũng là dịp để gắn kết tình cảm giữa các thành viên trong đại gia đình NICON.",
                }),
                SortOrder = 12,
            },
            new()
            {
                Slug = "nha-xuong-vda-hcm", Date = "07.07.2018",
                ImageUrl = "/images/activities/activity-handover.jpg",
                Category = "Khởi công", Author = "Phòng Truyền Thông",
                Title = "Xây dựng nhà máy mới của Công ty VDA-HCM",
                Excerpt = "Ngày 23/12/2016, NICON và VDA-HCM khởi công nhà máy mới tại KCN Cầu Tràm, Cần Đước, Long An.",
                ContentJson = JsonSerializer.Serialize(new[] {
                    "Ngày 23/12/2016, NICON và VDA-HCM đã khởi công nhà máy mới tại Khu công nghiệp Cầu Tràm, huyện Cần Đước, tỉnh Long An.",
                    "Dự án đánh dấu sự mở rộng hoạt động của VDA-HCM và sự tin tưởng vào năng lực thi công của NICON.",
                }),
                SortOrder = 13,
            },
            new()
            {
                Slug = "nha-may-amiba-db", Date = "07.07.2018",
                ImageUrl = "/images/activities/activity-ceremony.jpg",
                Category = "Khởi công", Author = "Phòng Truyền Thông",
                Title = "Dự án Nhà máy AMIBA: Thiết kế & Thi công",
                Excerpt = "Ngày 29/12/2017, NICON và AMIBA tổ chức lễ khởi công nhà máy mới với diện tích 2 hecta tại KCN VSIP II-A, Bình Dương.",
                ContentJson = JsonSerializer.Serialize(new[] {
                    "Ngày 29/12/2017, NICON và AMIBA đã long trọng tổ chức lễ khởi công nhà máy mới với diện tích 2 hecta tại KCN VSIP II-A, Tân Uyên, Bình Dương.",
                    "Dự án được thực hiện theo hình thức Design & Build, NICON chịu trách nhiệm toàn bộ từ thiết kế đến thi công và bàn giao.",
                }),
                SortOrder = 14,
            },
            new()
            {
                Slug = "ky-ket-nicon-mori", Date = "07.07.2018",
                ImageUrl = "/images/activities/activity-ceremony.jpg",
                Category = "Sự kiện", Author = "Phòng Truyền Thông",
                Title = "Ký kết hợp đồng đối tác chiến lược NICON & Mori",
                Excerpt = "Ngày 19/06/2016, ông Võ Trí Nguyên (CEO NICON) và ông Yoshihiro Mori (CEO Mori Industrial Group) ký kết hợp đồng đối tác chiến lược.",
                ContentJson = JsonSerializer.Serialize(new[] {
                    "Ngày 19/06/2016, tại Văn phòng NICON, ông Võ Trí Nguyên (CEO NICON) và ông Yoshihiro Mori (CEO Mori Industrial Group) đã ký kết hợp đồng đối tác chiến lược.",
                    "Với thỏa thuận này, Mori Industrial Group trở thành đối tác chiến lược của NICON, mang đến tiêu chuẩn kỹ thuật Nhật Bản cho các dự án xây dựng công nghiệp.",
                }),
                SortOrder = 15,
            },
        };

        db.Activities.AddRange(items);
        db.SaveChanges();
    }

    // ─── News (Vietnamese default) ──────────────────────────────────

    private static void SeedNews(AppDbContext db)
    {
        if (db.NewsArticles.Any()) return;

        var items = new NewsArticle[]
        {
            new() { Slug = "modern-fire-protection-system", Date = "13.01.2025", ImageUrl = "/images/news/news-fire-protection.jpeg", Category = "Kỹ thuật", Title = "Hệ thống phòng cháy hiện đại cho các nhà máy có nguy cơ cao — Giải pháp tối ưu từ NICON", Excerpt = "Nhà máy ngành hoá chất, dệt may thường đối mặt với rủi ro cháy nổ cao. Trang bị hệ thống PCCC hiện đại là yếu tố then chốt bảo vệ tài sản và đảm bảo vận hành liên tục.", ContentJson = JsonSerializer.Serialize(new[] { "Nhà máy trong các ngành công nghiệp hóa chất, dệt may, may mặc thường đối mặt với nguy cơ cháy nổ cao do sự hiện diện của vật liệu dễ cháy, thiết bị sinh nhiệt lớn và môi trường sản xuất phức tạp.", "NICON cung cấp giải pháp PCCC trọn gói: thiết kế hệ thống sprinkler, hệ báo cháy địa chỉ, bình CO2/foam, vách ngăn chống cháy theo tiêu chuẩn TCVN 3890:2009 và NFPA quốc tế.", "Đội ngũ kỹ sư phòng cháy của NICON có chứng chỉ hành nghề, đảm bảo hồ sơ thẩm duyệt nhanh gọn và bàn giao nghiệm thu thuận lợi." }), SortOrder = 0 },
            new() { Slug = "build-concept-for-design-project", Date = "25.12.2024", ImageUrl = "/images/news/news-build-concept.jpeg", Category = "Thiết kế", Title = "Xây dựng concept cho dự án thiết kế", Excerpt = "Tạo concept cho một dự án thiết kế nhà ở là bước đầu tiên nhưng quan trọng, giúp định hình ý tưởng và đảm bảo sự hài hoà giữa thẩm mỹ, công năng và nhu cầu thực tế.", ContentJson = JsonSerializer.Serialize(new[] { "Concept thiết kế là nền tảng định hướng cho toàn bộ quá trình thi công. Một concept tốt phản ánh cá tính và câu chuyện riêng của ngôi nhà.", "NICON xây dựng concept dựa trên ba trụ cột: nghiên cứu khách hàng – tham chiếu xu hướng – thử nghiệm vật liệu. Mỗi dự án đều có moodboard riêng được trình bày 3D trước khi triển khai chi tiết.", "Thông qua quy trình này, chúng tôi đảm bảo khách hàng nhìn thấy rõ kết quả cuối cùng trước khi xây dựng, tránh phát sinh chỉnh sửa tốn kém." }), SortOrder = 1 },
            new() { Slug = "gmp-standards-for-factories", Date = "16.12.2024", ImageUrl = "/images/news/news-gmp-standards.jpeg", Category = "Tiêu chuẩn", Title = "Tìm hiểu chuẩn GMP trong nhà máy thực phẩm, dược phẩm và mỹ phẩm", Excerpt = "Trong các ngành được kiểm soát chặt chẽ như thực phẩm, dược phẩm và mỹ phẩm, việc tuân thủ GMP là điều kiện tiên quyết để đảm bảo chất lượng và an toàn sản phẩm.", ContentJson = JsonSerializer.Serialize(new[] { "GMP (Good Manufacturing Practices) là hệ tiêu chuẩn bắt buộc cho ngành thực phẩm – dược – mỹ phẩm, kiểm soát toàn diện từ thiết kế nhà xưởng đến quy trình sản xuất.", "NICON đã thiết kế và thi công nhiều dự án đạt GMP-WHO, GMP-EU như nhà máy dược, mỹ phẩm. Các yêu cầu cốt lõi gồm: phân khu sạch theo cấp ISO 5/7/8, hệ HVAC độc lập, vật liệu không phát bụi và quy trình một chiều.", "Đội ngũ kỹ sư NICON tư vấn miễn phí giai đoạn lập dự án, giúp chủ đầu tư tránh sai sót thiết kế dẫn đến không đạt khi thẩm định." }), SortOrder = 2 },
            new() { Slug = "nicon-consulting-factory-projects", Date = "15.12.2024", ImageUrl = "/images/news/news-consulting-factory.jpeg", Category = "Dịch vụ", Title = "NICON tư vấn và thiết kế dự án nhà máy, nhà xưởng", Excerpt = "Với nhiều năm kinh nghiệm tư vấn và thiết kế, NICON cung cấp dịch vụ trọn gói từ tư vấn thiết kế đến thi công, luôn đem lại giải pháp tối ưu cho dự án của nhà đầu tư.", ContentJson = JsonSerializer.Serialize(new[] { "Là nhà thầu chuyên nghiệp với hơn 18 năm kinh nghiệm, NICON đã đồng hành cùng hơn 80 chủ đầu tư trong và ngoài nước trong việc tư vấn và thiết kế nhà máy, nhà xưởng công nghiệp.", "Dịch vụ tư vấn của NICON bao gồm: lựa chọn địa điểm, lập dự án đầu tư, thiết kế cơ sở, thiết kế kỹ thuật và bản vẽ thi công.", "Chúng tôi cam kết bàn giao hồ sơ đúng tiến độ và hỗ trợ chủ đầu tư trong suốt quá trình xin phép xây dựng." }), SortOrder = 3 },
            new() { Slug = "nicon-factory-price-list-2024", Date = "10.11.2024", ImageUrl = "/images/news/news-price-list-2024.jpeg", Category = "Báo giá", Title = "Bảng giá xây dựng nhà máy NICON 2024", Excerpt = "NICON cập nhật bảng giá xây dựng nhà máy nhà xưởng năm 2024 — minh bạch theo từng loại kết cấu và quy mô diện tích.", ContentJson = JsonSerializer.Serialize(new[] { "Bảng giá năm 2024 của NICON áp dụng cho các loại nhà xưởng kết cấu thép tiền chế và bê tông cốt thép, với các mức từ tiêu chuẩn đến cao cấp.", "Mức giá tham khảo: nhà xưởng thép tiền chế từ 2.700.000 đ/m², nhà xưởng có lửng văn phòng từ 3.500.000 đ/m², kho lạnh từ 5.000.000 đ/m² (chưa VAT).", "Để có báo giá chính xác, vui lòng liên hệ phòng kinh doanh NICON với thông tin chi tiết về địa điểm, diện tích và yêu cầu kỹ thuật cụ thể." }), SortOrder = 4 },
            new() { Slug = "nihome-redefining-service-apartment", Date = "07.11.2024", ImageUrl = "/images/news/news-nihome-apartment.png", Category = "Đối tác", Title = "NIHOME — \"Định nghĩa lại\" mô hình căn hộ dịch vụ", Excerpt = "Nicon và Nihome đã \"định nghĩa lại\" khái niệm căn hộ dịch vụ thành một không gian sống mang đến trải nghiệm thư giãn, tận hưởng cho khách hàng.", ContentJson = JsonSerializer.Serialize(new[] { "NIHOME là thương hiệu căn hộ dịch vụ cao cấp được phát triển bởi NICON, hướng đến đối tượng chuyên gia nước ngoài làm việc dài hạn tại Việt Nam.", "Khác biệt của NIHOME là sự kết hợp giữa thiết kế nội thất tối giản kiểu Nhật, dịch vụ khách sạn 4 sao và vị trí trung tâm các đô thị lớn.", "Hiện NIHOME đã có mặt tại Thủ Đức, Bình Dương và đang mở rộng sang Hà Nội trong giai đoạn 2025-2026." }), SortOrder = 5 },
            // ── Additional news from nicon.vn (page 2) ──
            new() { Slug = "nihome-trends-2024", Date = "16.10.2024", ImageUrl = "/images/news/news-build-concept.jpeg", Category = "Xu hướng", Title = "NIHOME Trends — Xu hướng thiết kế nội thất 2024", Excerpt = "Khám phá những xu hướng thiết kế nội thất nổi bật năm 2024 mà NIHOME đang áp dụng trong các dự án căn hộ dịch vụ.", ContentJson = JsonSerializer.Serialize(new[] { "Năm 2024 chứng kiến sự trỗi dậy của phong cách Japandi — sự giao thoa giữa tối giản Nhật Bản và ấm áp Scandinavian.", "NIHOME áp dụng xu hướng này với vật liệu tự nhiên, tông màu trung tính và ánh sáng tự nhiên tối đa trong mỗi căn hộ." }), SortOrder = 6 },
            new() { Slug = "5-nha-may-kien-truc-doc-dao", Date = "10.10.2024", ImageUrl = "/images/news/news-consulting-factory.jpeg", Category = "Kiến trúc", Title = "5 Nhà máy có kiến trúc độc đáo nhất thế giới", Excerpt = "Điểm danh 5 nhà máy công nghiệp có kiến trúc ấn tượng, đột phá, truyền cảm hứng cho ngành xây dựng công nghiệp.", ContentJson = JsonSerializer.Serialize(new[] { "Kiến trúc nhà máy không chỉ đáp ứng công năng sản xuất mà còn thể hiện bản sắc thương hiệu.", "Bài viết giới thiệu 5 nhà máy tiêu biểu toàn cầu với thiết kế sáng tạo, bền vững và thân thiện môi trường." }), SortOrder = 7 },
            new() { Slug = "quy-trinh-xay-dung-nha-may", Date = "07.10.2024", ImageUrl = "/images/news/news-gmp-standards.jpeg", Category = "Quy trình", Title = "Quy trình xây dựng nhà máy công nghiệp từ A-Z", Excerpt = "Tổng hợp quy trình xây dựng nhà máy công nghiệp chi tiết từ giai đoạn lập dự án đến bàn giao vận hành.", ContentJson = JsonSerializer.Serialize(new[] { "Xây dựng nhà máy công nghiệp là quá trình phức tạp, đòi hỏi sự phối hợp chặt chẽ giữa nhiều bên.", "NICON chia quy trình thành 6 giai đoạn chính: Lập dự án → Thiết kế → Xin phép → Thi công → Nghiệm thu → Bàn giao vận hành." }), SortOrder = 8 },
            new() { Slug = "yeu-to-tham-my-thiet-ke-cong-nghiep", Date = "03.10.2024", ImageUrl = "/images/news/news-build-concept.jpeg", Category = "Thiết kế", Title = "Yếu tố thẩm mỹ trong thiết kế công nghiệp", Excerpt = "Thẩm mỹ không chỉ dành cho công trình dân dụng — nhà máy cũng cần được thiết kế đẹp để nâng cao giá trị thương hiệu.", ContentJson = JsonSerializer.Serialize(new[] { "Xu hướng hiện đại yêu cầu nhà máy không chỉ đáp ứng công năng mà còn phải đẹp, thể hiện bản sắc doanh nghiệp.", "NICON tích hợp yếu tố thẩm mỹ vào thiết kế nhà máy: mặt đứng hiện đại, cảnh quan xanh, khu văn phòng tiêu chuẩn 5 sao." }), SortOrder = 9 },
            new() { Slug = "healing-trong-kien-truc", Date = "25.09.2024", ImageUrl = "/images/news/news-nihome-apartment.png", Category = "Kiến trúc", Title = "Healing trong kiến trúc — Không gian chữa lành", Excerpt = "Xu hướng thiết kế không gian chữa lành (healing architecture) đang được áp dụng rộng rãi trong kiến trúc hiện đại.", ContentJson = JsonSerializer.Serialize(new[] { "Healing architecture tập trung vào việc tạo ra không gian sống và làm việc mang lại cảm giác thư thái, giảm stress.", "NIHOME áp dụng nguyên tắc này: cây xanh nội thất, ánh sáng tự nhiên, vật liệu gỗ ấm áp và không gian mở." }), SortOrder = 10 },
            new() { Slug = "tieu-chuan-pccc-2024", Date = "24.09.2024", ImageUrl = "/images/news/news-fire-protection.jpeg", Category = "Tiêu chuẩn", Title = "Tiêu chuẩn PCCC mới nhất 2024 cho nhà máy công nghiệp", Excerpt = "Cập nhật các quy định và tiêu chuẩn phòng cháy chữa cháy mới nhất năm 2024 áp dụng cho nhà máy, nhà xưởng.", ContentJson = JsonSerializer.Serialize(new[] { "Năm 2024, Bộ Công an ban hành nhiều quy định mới về PCCC cho công trình công nghiệp.", "NICON cập nhật và tuân thủ đầy đủ các tiêu chuẩn TCVN 3890:2023, QCVN 06:2022/BXD trong mọi dự án." }), SortOrder = 11 },
            // ── Additional news from nicon.vn (page 3) ──
            new() { Slug = "xu-huong-nha-dep-2025", Date = "23.09.2024", ImageUrl = "/images/news/news-build-concept.jpeg", Category = "Xu hướng", Title = "Xu hướng nhà đẹp 2025 — Tối giản và bền vững", Excerpt = "Những xu hướng thiết kế nhà ở nổi bật dự kiến sẽ thống trị năm 2025: tối giản, bền vững và thông minh.", ContentJson = JsonSerializer.Serialize(new[] { "Năm 2025 dự kiến tiếp tục xu hướng tối giản kết hợp công nghệ smart home.", "Vật liệu tái chế, năng lượng mặt trời và hệ thống quản lý năng lượng thông minh sẽ trở thành tiêu chuẩn." }), SortOrder = 12 },
            new() { Slug = "tong-thau-nha-may-thuc-pham", Date = "19.09.2024", ImageUrl = "/images/news/news-gmp-standards.jpeg", Category = "Dịch vụ", Title = "Tổng thầu nhà máy thực phẩm — Tiêu chuẩn GMP/HACCP", Excerpt = "NICON chia sẻ kinh nghiệm trong vai trò tổng thầu thiết kế và thi công nhà máy thực phẩm đạt chuẩn quốc tế.", ContentJson = JsonSerializer.Serialize(new[] { "Nhà máy thực phẩm yêu cầu tuân thủ nghiêm ngặt tiêu chuẩn GMP và HACCP từ giai đoạn thiết kế.", "NICON đã hoàn thành nhiều dự án nhà máy thực phẩm cho khách hàng Nhật Bản, Thái Lan và Philippines." }), SortOrder = 13 },
            new() { Slug = "stfood-va-thiet-ke-cong-nghiep", Date = "18.09.2024", ImageUrl = "/images/news/news-consulting-factory.jpeg", Category = "Dự án", Title = "S.T.Food & Thiết kế công nghiệp — Case Study", Excerpt = "Phân tích chi tiết dự án nhà máy S.T.Food Marketing Việt Nam — từ concept đến bàn giao.", ContentJson = JsonSerializer.Serialize(new[] { "Dự án S.T.Food là ví dụ điển hình cho mô hình tổng thầu D&B mà NICON thực hiện.", "Từ thiết kế đến bàn giao trong 14 tháng, nhà máy đạt đầy đủ tiêu chuẩn GMP/HACCP cho sản xuất thực phẩm." }), SortOrder = 14 },
            new() { Slug = "thiet-ke-kien-truc-tinh-than-thuong-hieu", Date = "12.09.2024", ImageUrl = "/images/news/news-build-concept.jpeg", Category = "Thiết kế", Title = "Thiết kế kiến trúc thể hiện tinh thần thương hiệu", Excerpt = "Kiến trúc nhà máy không chỉ là nơi sản xuất — đó còn là bộ mặt thương hiệu của doanh nghiệp.", ContentJson = JsonSerializer.Serialize(new[] { "Nhiều doanh nghiệp quốc tế yêu cầu nhà máy tại Việt Nam phải thể hiện đúng bản sắc thương hiệu toàn cầu.", "NICON có kinh nghiệm thiết kế nhà máy cho Nestlé, Red Bull, Rebisco... đều tuân thủ Brand Guidelines nghiêm ngặt." }), SortOrder = 15 },
            new() { Slug = "xu-huong-khong-gian-mo", Date = "12.09.2024", ImageUrl = "/images/news/news-nihome-apartment.png", Category = "Xu hướng", Title = "Xu hướng không gian mở trong thiết kế văn phòng và nhà máy", Excerpt = "Không gian mở (open space) đang trở thành xu hướng chủ đạo trong thiết kế văn phòng và nhà máy hiện đại.", ContentJson = JsonSerializer.Serialize(new[] { "Không gian mở giúp tăng tương tác, linh hoạt bố trí và tiết kiệm diện tích.", "NICON áp dụng xu hướng này trong thiết kế văn phòng B37 và các dự án nhà máy có khu văn phòng tích hợp." }), SortOrder = 16 },
            new() { Slug = "hanh-trinh-thanh-cong-nha-may-thuc-pham", Date = "12.09.2024", ImageUrl = "/images/news/news-gmp-standards.jpeg", Category = "Dự án", Title = "Hành trình thành công của một nhà máy thực phẩm", Excerpt = "Chia sẻ hành trình từ ý tưởng đến vận hành thành công của một nhà máy thực phẩm tại Việt Nam.", ContentJson = JsonSerializer.Serialize(new[] { "Xây dựng nhà máy thực phẩm đạt chuẩn quốc tế là hành trình dài đòi hỏi sự kiên trì và chuyên môn cao.", "NICON đồng hành cùng chủ đầu tư từ giai đoạn khảo sát địa điểm, thiết kế, thi công đến khi nhà máy vận hành ổn định." }), SortOrder = 17 },
            // ── Additional news from nicon.vn (page 4) ──
            new() { Slug = "ban-giao-nha-may-apm", Date = "26.08.2024", ImageUrl = "/images/news/news-consulting-factory.jpeg", Category = "Dự án", Title = "Bàn giao nhà kho APM — Dự án D&B hoàn thành", Excerpt = "NICON hoàn thành bàn giao nhà kho APM cho Auto Components Việt Nam tại KCN VSIP Bình Dương.", ContentJson = JsonSerializer.Serialize(new[] { "Dự án nhà kho APM đã được bàn giao đúng tiến độ cam kết.", "Đây là dự án thiết kế nhà kho logistics hiện đại với dock loading và hệ thống kệ chứa hàng tự động." }), SortOrder = 18 },
            new() { Slug = "hiep-hoi-thep-bai-viet", Date = "09.08.2018", ImageUrl = "/images/news/news-fire-protection.jpeg", Category = "Ngành", Title = "NICON và ngành kết cấu thép Việt Nam", Excerpt = "NICON chia sẻ về vai trò của kết cấu thép trong xây dựng công nghiệp hiện đại tại Việt Nam.", ContentJson = JsonSerializer.Serialize(new[] { "Kết cấu thép là giải pháp tối ưu cho nhà máy công nghiệp nhờ tốc độ thi công nhanh, nhịp lớn và chi phí hợp lý.", "NICON là một trong những nhà thầu hàng đầu Việt Nam trong lĩnh vực thiết kế và thi công nhà xưởng kết cấu thép." }), SortOrder = 19 },
        };

        db.NewsArticles.AddRange(items);
        db.SaveChanges();
    }

    // ─── Projects ───────────────────────────────────────────────────

    private static void SeedProjects(AppDbContext db)
    {
        if (db.Projects.Any()) return;

        var items = new Project[]
        {
            new() { Slug = "nha-may-bma", ImageUrl = "/images/projects/project-bma.jpg", GalleryJson = JsonSerializer.Serialize(new[] { "/images/projects/project-bma.jpg", "/images/projects/project-office.jpg", "/images/projects/project-nbdc.jpg" }), Name = "Nhà Máy BMA", Client = "Bảo Minh Ân Việt Nam", Location = "KCN Hựu Thạnh, Tây Ninh", Scale = "15.000 m²", Scope = "Thiết kế và Thi công", Status = "ongoing", Year = "2024", Category = "Nhà máy công nghiệp", Description = "Dự án Nhà Máy BMA là tổ hợp sản xuất hiện đại với quy mô 15.000 m², được thiết kế theo tiêu chuẩn công nghiệp quốc tế.", ChallengesJson = JsonSerializer.Serialize(new[] { "Yêu cầu tiến độ chặt chẽ trong vòng 10 tháng từ khởi công đến vận hành.", "Giải pháp kết cấu nhà xưởng nhịp lớn không cột giữa cho dây chuyền sản xuất.", "Tối ưu hệ thống thông gió và chiếu sáng tự nhiên để tiết kiệm năng lượng." }), SolutionsJson = JsonSerializer.Serialize(new[] { "Áp dụng kết cấu thép tiền chế nhịp 30m với mái lấy sáng polycarbonate.", "Thi công song song nhiều hạng mục, quản lý tiến độ bằng phần mềm BIM 4D.", "Hệ thống M&E đồng bộ, dự phòng công suất cho mở rộng tương lai 30%." }), HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "15.000 m²" }, new { label = "Thời gian", value = "10 tháng" }, new { label = "Nhịp kết cấu", value = "30 m" }, new { label = "Tiêu chuẩn", value = "ISO 9001" } }), SortOrder = 0 },
            new() { Slug = "nha-xuong-nbdc", ImageUrl = "/images/projects/project-nbdc.jpg", Name = "Nhà Xưởng NBDC", Client = "Công ty TNHH NBDC VN", Location = "KCN Giang Điền, Đồng Nai", Scale = "8.500 m²", Scope = "Thiết kế", Status = "ongoing", Year = "2024", Category = "Nhà xưởng sản xuất", Description = "NICON cung cấp dịch vụ thiết kế kiến trúc và kết cấu trọn gói cho nhà xưởng sản xuất NBDC tại KCN Giang Điền.", ChallengesJson = JsonSerializer.Serialize(new[] { "Bố cục dây chuyền sản xuất phức tạp với nhiều khu vực chức năng.", "Yêu cầu tích hợp khu văn phòng điều hành và sản xuất trong cùng một khối." }), SolutionsJson = JsonSerializer.Serialize(new[] { "Phân khu rõ ràng với luồng di chuyển một chiều, giảm chéo nhau.", "Thiết kế khu văn phòng 2 tầng tích hợp với view nhìn xuống xưởng." }), HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "8.500 m²" }, new { label = "Khu chức năng", value = "5" }, new { label = "Nhân sự dự kiến", value = "200" }, new { label = "Năm", value = "2024" } }), SortOrder = 1 },
            new() { Slug = "nha-may-lhh", ImageUrl = "/images/projects/project-lhh.jpg", Name = "Nhà Máy Lâm Hiệp Hưng – Tân Toàn Phát", Client = "Lam Hiệp Hưng & Tân Toàn Phát", Location = "Tỉnh Bình Dương", Scale = "250.000 m²", Scope = "Thiết kế", Status = "ongoing", Year = "2023", Category = "Tổ hợp công nghiệp", Description = "Một trong những dự án quy mô lớn nhất NICON đã thực hiện: tổ hợp nhà máy 250.000 m².", ChallengesJson = JsonSerializer.Serialize(new[] { "Quy hoạch tổng mặt bằng quy mô siêu lớn với nhiều khối công trình.", "Đồng bộ hạ tầng kỹ thuật trên diện tích lớn." }), SolutionsJson = JsonSerializer.Serialize(new[] { "Quy hoạch theo mô-đun, dễ dàng mở rộng và thay đổi công năng.", "Hệ thống đường nội bộ thiết kế cho xe container 40 feet." }), HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Tổng diện tích", value = "250.000 m²" }, new { label = "Khối công trình", value = "12" }, new { label = "Đường nội bộ", value = "5,2 km" }, new { label = "Năm", value = "2023" } }), SortOrder = 2 },
            new() { Slug = "ttdtt-thu-duc", ImageUrl = "/images/projects/project-sports.jpg", Name = "Trung Tâm Thể Dục Thể Thao Thủ Đức", Client = "Thủ Thiêm Group", Location = "Thủ Đức, TP.HCM", Scale = "12.000 m²", Scope = "Thiết kế", Status = "ongoing", Year = "2024", Category = "Công trình công cộng", Description = "Trung tâm thể dục thể thao đa năng phục vụ cộng đồng tại Thủ Đức.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "12.000 m²" }, new { label = "Nhịp mái", value = "45 m" }, new { label = "Sức chứa", value = "2.000 chỗ" }, new { label = "Năm", value = "2024" } }), SortOrder = 3 },
            new() { Slug = "noi-that-b37", ImageUrl = "/images/projects/project-office.jpg", Name = "Văn Phòng B37", Client = "Nihome", Location = "Thủ Đức, TP.HCM", Scale = "1.200 m²", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2024", Category = "Nội thất văn phòng", Description = "Thiết kế nội thất văn phòng hiện đại với phong cách tối giản, không gian mở.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "1.200 m²" }, new { label = "Sức chứa", value = "80 người" }, new { label = "Phòng họp", value = "6" }, new { label = "Năm", value = "2024" } }), SortOrder = 4 },
            new() { Slug = "nha-may-trimas", ImageUrl = "/images/projects/project-bma.jpg", Name = "Nhà Máy Trimas Việt Nam", Client = "Rieke Packaging Vietnam Co., Ltd", Location = "VSIP IIA, TP.HCM", Scale = "10.000 m²", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2022", Category = "Nhà máy công nghiệp", Description = "Dự án trọn gói thiết kế và thi công nhà máy sản xuất bao bì cho Trimas tại VSIP IIA.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "10.000 m²" }, new { label = "Tiêu chuẩn", value = "ISO Class 8" }, new { label = "Thời gian", value = "9 tháng" }, new { label = "Năm", value = "2022" } }), SortOrder = 5 },
            new() { Slug = "nha-kho-apm", ImageUrl = "/images/projects/project-lhh.jpg", Name = "Nhà Kho APM", Client = "Auto Components Việt Nam", Location = "KCN Việt Nam – Singapore, Bình Hòa, Thuận An, Bình Dương", Scale = "6.500 m²", Scope = "Thiết kế", Status = "completed", Year = "2022", Category = "Nhà kho logistics", Description = "Thiết kế nhà kho logistics cho Auto Components Việt Nam.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "6.500 m²" }, new { label = "Chiều cao", value = "12 m" }, new { label = "Dock loading", value = "6" }, new { label = "Năm", value = "2022" } }), SortOrder = 6 },
            new() { Slug = "nha-may-jojo", ImageUrl = "/images/projects/project-nbdc.jpg", Name = "Nhà Máy JOJO", Client = "Phạm – Asset", Location = "KCN Hựu Thạnh, Long An", Scale = "7.800 m²", Scope = "Thiết kế", Status = "ongoing", Year = "2024", Category = "Nhà máy công nghiệp", Description = "Thiết kế nhà máy sản xuất JOJO với yêu cầu cao về vệ sinh an toàn thực phẩm.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "7.800 m²" }, new { label = "Tiêu chuẩn", value = "HACCP" }, new { label = "Khu sạch", value = "3" }, new { label = "Năm", value = "2024" } }), SortOrder = 7 },
            new() { Slug = "khach-san-d22", ImageUrl = "/images/projects/project-sports.jpg", Name = "Khách sạn D22", Client = "Nihome", Location = "Thủ Đức, TP.HCM", Scale = "4.500 m²", Scope = "Thiết kế và Thi công", Status = "ongoing", Year = "2024", Category = "Khách sạn", Description = "Khách sạn 4 sao với 80 phòng nghỉ, nhà hàng tầng trệt và khu spa.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "4.500 m²" }, new { label = "Số phòng", value = "80" }, new { label = "Tầng cao", value = "12" }, new { label = "Năm", value = "2024" } }), SortOrder = 8 },
            // ── Ongoing projects from nicon.vn ──
            new() { Slug = "nbdc-canteen", ImageUrl = "/images/projects/project-nbdc.jpg", Name = "NBDC Canteen", Client = "Công ty TNHH NBDC VN", Location = "KCN Giang Điền, Đồng Nai", Scale = "", Scope = "Thiết kế", Status = "ongoing", Year = "2024", Category = "Nhà xưởng sản xuất", Description = "Thiết kế nhà ăn cho khu công nghiệp NBDC tại KCN Giang Điền.", SortOrder = 9 },
            new() { Slug = "nbdc-office", ImageUrl = "/images/projects/project-nbdc.jpg", Name = "NBDC Office", Client = "Công ty TNHH NBDC VN", Location = "KCN Giang Điền, Đồng Nai", Scale = "", Scope = "Thiết kế", Status = "ongoing", Year = "2024", Category = "Văn phòng", Description = "Thiết kế văn phòng điều hành cho NBDC tại KCN Giang Điền.", SortOrder = 10 },
            new() { Slug = "nha-may-ttp", ImageUrl = "/images/projects/project-lhh.jpg", Name = "Nhà Máy Tân Toàn Phát", Client = "Lam Hiệp Hưng & Tân Toàn Phát", Location = "Tỉnh Bình Dương", Scale = "250.000 m²", Scope = "Thiết kế", Status = "ongoing", Year = "2024", Category = "Nhà máy công nghiệp", Description = "Nhà máy thuộc tổ hợp Lâm Hiệp Hưng – Tân Toàn Phát tại Bình Dương.", SortOrder = 11 },
            new() { Slug = "nha-may-lhh-2", ImageUrl = "/images/projects/project-lhh.jpg", Name = "Nhà Máy Lâm Hiệp Hưng", Client = "Lam Hiệp Hưng", Location = "Tỉnh Bình Dương", Scale = "250.000 m²", Scope = "Thiết kế", Status = "ongoing", Year = "2024", Category = "Nhà máy công nghiệp", Description = "Nhà máy Lâm Hiệp Hưng tại Bình Dương, mở rộng dây chuyền sản xuất.", SortOrder = 12 },
            // ── Completed projects from nicon.vn ──
            new() { Slug = "nha-may-stfood", ImageUrl = "/images/projects/project-bma.jpg", Name = "Nhà Máy S.T.Food Marketing Việt Nam", Client = "S.T.FOOD MARKETING Vietnam Co. Ltd.", Location = "Đường 24, VSIP II-A, Tân Uyên, Bình Dương", Scale = "20.000 m²", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2022", Category = "Nhà máy công nghiệp", Description = "Nhà máy sản xuất thực phẩm từ chủ đầu tư Thái Lan, thiết kế theo tiêu chuẩn GMP và HACCP.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "20.000 m²" }, new { label = "Tiêu chuẩn", value = "GMP/HACCP" }, new { label = "Thời gian", value = "14 tháng" }, new { label = "Năm", value = "2022" } }), SortOrder = 13 },
            new() { Slug = "medicare-shop", ImageUrl = "/images/projects/project-office.jpg", Name = "Medicare Shop", Client = "Medicare Company", Location = "G3, Aeon Mall Bình Dương Canary", Scale = "250 m²", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2020", Category = "Thương mại", Description = "Cửa hàng Medicare tại Aeon Mall Bình Dương Canary.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "250 m²" }, new { label = "Vị trí", value = "Aeon Mall" }, new { label = "Năm", value = "2020" } }), SortOrder = 14 },
            new() { Slug = "nha-may-lhh-completed", ImageUrl = "/images/projects/project-lhh.jpg", Name = "Nhà Máy Lâm Hiệp Hưng (Hoàn thành)", Client = "Lam Hiệp Hưng & Tân Toàn Phát", Location = "Tỉnh Bình Dương", Scale = "18.000 m²", Scope = "Thiết kế", Status = "completed", Year = "2019", Category = "Nhà máy công nghiệp", Description = "Dự án nhà máy Lâm Hiệp Hưng giai đoạn 1 đã hoàn thành.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "18.000 m²" }, new { label = "Năm", value = "2019" } }), SortOrder = 15 },
            new() { Slug = "sctv-office", ImageUrl = "/images/projects/project-office.jpg", Name = "Văn Phòng SCTV", Client = "Đài Truyền hình Cáp Sài Gòn", Location = "Quận 2, TP.HCM", Scale = "4.000 m²", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2020", Category = "Văn phòng", Description = "Thiết kế và thi công văn phòng SCTV tại Quận 2, TP.HCM.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "4.000 m²" }, new { label = "Năm", value = "2020" } }), SortOrder = 16 },
            new() { Slug = "nha-may-hbfuller", ImageUrl = "/images/projects/project-bma.jpg", Name = "Nhà Máy H.B.Fuller", Client = "H.B.Fuller Co., Ltd.", Location = "Tỉnh Bình Dương", Scale = "", Scope = "MEP", Status = "completed", Year = "2019", Category = "Nhà máy công nghiệp", Description = "Thi công hệ thống MEP cho nhà máy H.B.Fuller tại Bình Dương.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Phạm vi", value = "MEP" }, new { label = "Năm", value = "2019" } }), SortOrder = 17 },
            new() { Slug = "red-bull-expansion", ImageUrl = "/images/projects/project-bma.jpg", Name = "Dự Án Mở Rộng Red Bull", Client = "Red Bull (Việt Nam) Co., Ltd", Location = "Xa lộ Hà Nội, Bình Thắng, Dĩ An, Bình Dương", Scale = "2.000 m²", Scope = "Thiết kế", Status = "completed", Year = "2024", Category = "Nhà máy công nghiệp", Description = "Thiết kế mở rộng nhà máy Red Bull tại Bình Dương, giữ nguyên bản sắc thương hiệu.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "2.000 m²" }, new { label = "Năm", value = "2024" } }), SortOrder = 18 },
            new() { Slug = "nha-may-great-lotus", ImageUrl = "/images/projects/project-bma.jpg", Name = "Nhà Máy Great Lotus Việt Nam", Client = "Great Lotus Manufacturing Vietnam Co. Ltd.", Location = "VSIP II-A, Tân Uyên, Bình Dương", Scale = "31.187 m²", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2019", Category = "Nhà máy công nghiệp", Description = "Thiết kế và thi công nhà máy Great Lotus với quy mô hơn 31.000 m².", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "31.187 m²" }, new { label = "Năm", value = "2019" } }), SortOrder = 19 },
            new() { Slug = "nha-may-advanced-casting", ImageUrl = "/images/projects/project-nbdc.jpg", Name = "Nhà Máy Advanced Casting Asia", Client = "Advanced Casting Asia Co. Ltd.", Location = "VSIP II-A, Tân Uyên, Bình Dương", Scale = "15.000 m²", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2018", Category = "Nhà máy công nghiệp", Description = "Nhà máy sản xuất Advanced Casting Asia tại VSIP II-A.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "15.000 m²" }, new { label = "Năm", value = "2018" } }), SortOrder = 20 },
            new() { Slug = "sctv-studio", ImageUrl = "/images/projects/project-office.jpg", Name = "SCTV Studio & Văn Phòng", Client = "Đài Truyền hình Cáp Sài Gòn", Location = "Quận 2, TP.HCM", Scale = "", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2018", Category = "Studio", Description = "Trường quay truyền hình quy mô lớn nhất Việt Nam tại thời điểm xây dựng.", SortOrder = 21 },
            new() { Slug = "nha-may-bkl", ImageUrl = "/images/projects/project-nbdc.jpg", Name = "Nhà Máy BKL", Client = "BKL International Ltd., Co", Location = "KCN Thịnh Phát, Bến Lức, Long An", Scale = "5.000 m²", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2018", Category = "Nhà máy công nghiệp", Description = "Thiết kế và thi công nhà máy BKL tại KCN Thịnh Phát.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "5.000 m²" }, new { label = "Năm", value = "2018" } }), SortOrder = 22 },
            new() { Slug = "nha-may-rebisco", ImageUrl = "/images/projects/project-bma.jpg", Name = "Nhà Máy Rebisco", Client = "Republic Biscuit Corporation", Location = "VSIP II-A, Tân Uyên, Bình Dương", Scale = "20.000 m²", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2017", Category = "Nhà máy công nghiệp", Description = "Nhà máy sản xuất bánh kẹo Rebisco (Philippines) tại VSIP II-A.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "20.000 m²" }, new { label = "Năm", value = "2017" } }), SortOrder = 23 },
            new() { Slug = "nha-may-nestle", ImageUrl = "/images/projects/project-bma.jpg", Name = "Nhà Máy & Văn Phòng Nestlé Bình An", Client = "Nestlé Việt Nam", Location = "KCN Biên Hòa II, Đồng Nai", Scale = "", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2015", Category = "Nhà máy công nghiệp", Description = "Thiết kế và thi công nhà máy và văn phòng Nestlé Bình An.", SortOrder = 24 },
            new() { Slug = "nha-may-ampharco", ImageUrl = "/images/projects/project-nbdc.jpg", Name = "Nhà Máy Ampharco U.S.A", Client = "Ampharco U.S.A", Location = "KCN Nhơn Trạch 3, Đồng Nai", Scale = "", Scope = "Thi công", Status = "completed", Year = "2016", Category = "Nhà máy dược phẩm", Description = "Thi công nhà máy dược phẩm Ampharco U.S.A tại Nhơn Trạch.", SortOrder = 25 },
            new() { Slug = "konimiyaki-restaurant", ImageUrl = "/images/projects/project-office.jpg", Name = "Nhà Hàng Konimiyaki", Client = "Konimiyaki Restaurant", Location = "Quận 1, TP.HCM", Scale = "400 m²", Scope = "Thiết kế", Status = "completed", Year = "2018", Category = "Nhà hàng", Description = "Thiết kế nhà hàng Konimiyaki tại Quận 1, TP.HCM.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "400 m²" }, new { label = "Năm", value = "2018" } }), SortOrder = 26 },
            new() { Slug = "nha-may-scon", ImageUrl = "/images/projects/project-nbdc.jpg", Name = "Nhà Máy SCON", Client = "SCON Co. Ltd.", Location = "VSIP II-A, Tân Uyên, Bình Dương", Scale = "8.337 m²", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2018", Category = "Nhà máy công nghiệp", Description = "Thiết kế và thi công nhà máy SCON tại VSIP II-A.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "8.337 m²" }, new { label = "Năm", value = "2018" } }), SortOrder = 27 },
            new() { Slug = "nha-may-clotex", ImageUrl = "/images/projects/project-nbdc.jpg", Name = "Nhà Máy Clotex Labels Việt Nam", Client = "Clotex Labels (VN) Co. Ltd.", Location = "VSIP II-A, Tân Uyên, Bình Dương", Scale = "8.565 m²", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2018", Category = "Nhà máy công nghiệp", Description = "Nhà máy nhãn mác Clotex Labels Vietnam tại VSIP II-A.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "8.565 m²" }, new { label = "Năm", value = "2018" } }), SortOrder = 28 },
            new() { Slug = "nha-may-amiba", ImageUrl = "/images/projects/project-bma.jpg", Name = "Nhà Máy Amiba", Client = "Amiba Vietnam Company Limited", Location = "VSIP II-A, Tân Uyên, Bình Dương", Scale = "20.000 m²", Scope = "Thi công", Status = "completed", Year = "2018", Category = "Nhà máy công nghiệp", Description = "Thi công nhà máy Amiba với diện tích 2 hecta tại VSIP II-A.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "20.000 m²" }, new { label = "Năm", value = "2018" } }), SortOrder = 29 },
            new() { Slug = "nha-may-akati-wood", ImageUrl = "/images/projects/project-lhh.jpg", Name = "Nhà Máy Akati Wood", Client = "Akati Dominant (Malaysia)", Location = "Bình Dương", Scale = "", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2016", Category = "Nhà máy công nghiệp", Description = "Nhà máy gỗ Akati Wood, chi nhánh của Akati Dominant từ Malaysia.", SortOrder = 30 },
            new() { Slug = "nha-may-japan-plus", ImageUrl = "/images/projects/project-nbdc.jpg", Name = "Nhà Máy Japan Plus", Client = "Japan Plus (Nhật Bản)", Location = "KCN Đông Nam Củ Chi", Scale = "", Scope = "Thi công", Status = "completed", Year = "2016", Category = "Nhà máy công nghiệp", Description = "Nhà máy Japan Plus sản xuất hộp PE tại KCN Đông Nam Củ Chi.", SortOrder = 31 },
            new() { Slug = "duoc-pham-trung-uong", ImageUrl = "/images/projects/project-office.jpg", Name = "Dược Phẩm Trung Ương TP.HCM", Client = "Công ty TNHH Dược Phẩm Trung Ương 1", Location = "TP.HCM", Scale = "", Scope = "Thiết kế", Status = "completed", Year = "2020", Category = "Nhà máy dược phẩm", Description = "Thiết kế nhà máy dược phẩm Trung Ương tại TP.HCM.", SortOrder = 32 },
            new() { Slug = "kumgang-office", ImageUrl = "/images/projects/project-office.jpg", Name = "Văn Phòng Kumgang", Client = "KUMGANG VINA CO., LTD", Location = "KCN Giang Điền, Trảng Bom, Đồng Nai", Scale = "180 m²", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2018", Category = "Văn phòng", Description = "Thiết kế và thi công văn phòng Kumgang Vina.", HighlightsJson = JsonSerializer.Serialize(new[] { new { label = "Diện tích", value = "180 m²" }, new { label = "Năm", value = "2018" } }), SortOrder = 33 },
            new() { Slug = "nha-may-vda-hcm", ImageUrl = "/images/projects/project-nbdc.jpg", Name = "Nhà Máy VDA-HCM", Client = "VDA-HCM", Location = "KCN Cầu Tràm, Cần Đước, Long An", Scale = "", Scope = "Thiết kế và Thi công", Status = "completed", Year = "2017", Category = "Nhà máy công nghiệp", Description = "Thiết kế và thi công nhà máy VDA-HCM tại KCN Cầu Tràm.", SortOrder = 34 },
            new() { Slug = "thu-thiem-dragon", ImageUrl = "/images/projects/project-sports.jpg", Name = "Thu Thiêm Dragon Show Flat", Client = "Thu Thiêm Group", Location = "Quận 2, TP.HCM", Scale = "", Scope = "Thi công", Status = "completed", Year = "2015", Category = "Bất động sản", Description = "Thi công căn hộ mẫu Thu Thiêm Dragon tại Quận 2.", SortOrder = 35 },
            new() { Slug = "nha-may-nam-ha-viet", ImageUrl = "/images/projects/project-nbdc.jpg", Name = "Nhà Máy Nam Hà Việt", Client = "Nam Hà Việt Co., Ltd.", Location = "KCN Rạch Bắp, Bến Cát, Bình Dương", Scale = "", Scope = "Thiết kế", Status = "completed", Year = "2020", Category = "Nhà máy công nghiệp", Description = "Nhà máy sản xuất que hàn Nam Hà Việt.", SortOrder = 36 },
            new() { Slug = "nha-may-yc-tec", ImageUrl = "/images/projects/project-nbdc.jpg", Name = "Nhà Máy YC TEC", Client = "YC TEC Group", Location = "KCN Sóng Thần II, Dĩ An, Bình Dương", Scale = "", Scope = "Thiết kế", Status = "completed", Year = "2020", Category = "Nhà máy công nghiệp", Description = "Thiết kế nhà máy YC TEC tại KCN Sóng Thần II.", SortOrder = 37 },
        };

        db.Projects.AddRange(items);
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
        if (db.ClientLogos.Any()) return;

        var logos = new List<ClientLogo>();
        var i = 0;

        string[][] clients = [
            ["CLOTEX", "/images/logos/clients/clotex.png"],
            ["SCON", "/images/logos/clients/scon.jpeg"],
            ["SMITH MULLER", "/images/logos/clients/smith-muller.jpeg"],
            ["LAM HIEP HUNG", "/images/logos/clients/lam-hiep-hung.jpeg"],
            ["NESTLE", "/images/logos/clients/nestle.jpeg"],
            ["REBISCO", "/images/logos/clients/rebisco.jpeg"],
            ["S.T.FOOD MARKETING", "/images/logos/clients/stfood-marketing.png"],
            ["PHAM-ASSET", "/images/logos/clients/pham-asset.png"],
            ["WATTENS", "/images/logos/clients/wattens.jpeg"],
            ["GREAT LOTUS", "/images/logos/clients/great-lotus.jpeg"],
            ["ADVANCED CASTING ASIA", "/images/logos/clients/advanced-casting-asia.jpeg"],
            ["AMPHACO", "/images/logos/clients/amphaco.jpeg"],
            ["EVERGREEN", "/images/logos/clients/evergreen.jpeg"],
            ["APM SPRINGS", "/images/logos/clients/apm-springs.jpeg"],
            ["RED BULL", "/images/logos/clients/red-bull.png"],
            ["SALADSTOP", "/images/logos/clients/saladstop.jpeg"],
            ["BMT GROUP", "/images/logos/clients/bmt-group.jpeg"],
            ["LAVIE", "/images/logos/clients/lavie.jpeg"],
            ["AKATI WOOD", "/images/logos/clients/akati-wood.png"],
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

        string[][] suppliers = [
            ["Seamasterpaint", "/images/logos/suppliers/seamasterpaint.jpeg"],
            ["MPE-Inc", "/images/logos/suppliers/mpe-inc.jpeg"],
            ["Chi Thanh Steel", "/images/logos/suppliers/chi-thanh-steel.jpeg"],
            ["Nippon", "/images/logos/suppliers/nippon.jpeg"],
            ["Vicem Cement", "/images/logos/suppliers/vicem-cement.png"],
            ["Fico Cement", "/images/logos/suppliers/fico-cement.png"],
            ["Dong Tam Group", "/images/logos/suppliers/dong-tam-group.png"],
            ["Hoa Phat Steel", "/images/logos/suppliers/hoa-phat-steel.jpeg"],
            ["Dulux", "/images/logos/suppliers/dulux.jpeg"],
            ["Sika", "/images/logos/suppliers/sika.jpeg"],
            ["Shell", "/images/logos/suppliers/shell.jpeg"],
            ["Cadivi", "/images/logos/suppliers/cadivi.png"],
            ["VN Steel", "/images/logos/suppliers/vn-steel.png"],
            ["EVN", "/images/logos/suppliers/evn.png"],
            ["Schneider", "/images/logos/suppliers/schneider.png"],
            ["Thinh Phat", "/images/logos/suppliers/thinh-phat.jpeg"],
            ["LS-Vina", "/images/logos/suppliers/ls-vina.jpeg"],
            ["Posco VN", "/images/logos/suppliers/posco-vn.png"],
            ["WhiteHorse Ceramic", "/images/logos/suppliers/whitehorse-ceramic.png"],
            ["Minh Viet Son", "/images/logos/suppliers/minh-viet-son.jpeg"],
            ["QSB Steel", "/images/logos/suppliers/qsb-steel.jpeg"],
            ["Song Hop Luc", "/images/logos/suppliers/song-hop-luc.jpeg"],
            ["Duhal Led", "/images/logos/suppliers/duhal-led.jpeg"],
            ["Eurowindow", "/images/logos/suppliers/eurowindow.jpeg"],
            ["SINO", "/images/logos/suppliers/sino.jpeg"],
            ["Zamil Steel", "/images/logos/suppliers/zamil-steel.jpeg"],
            ["BlueScope", "/images/logos/suppliers/bluescope.jpeg"],
            ["Caesar", "/images/logos/suppliers/caesar.jpeg"],
            ["TungShin", "/images/logos/suppliers/tungshin.jpeg"],
            ["Tai Truong Thanh", "/images/logos/suppliers/tai-truong-thanh.jpeg"],
            ["Holcim", "/images/logos/suppliers/holcim.jpeg"],
            ["Viglacera", "/images/logos/suppliers/viglacera.jpeg"],
            ["American Standard", "/images/logos/suppliers/american-standard.jpeg"],
            ["Vina Kyoei", "/images/logos/suppliers/vina-kyoei.jpeg"],
            ["Binh Minh", "/images/logos/suppliers/binh-minh.jpeg"],
            ["Taicera", "/images/logos/suppliers/taicera.jpeg"],
        ];
        i = 0;
        foreach (var s in suppliers)
            logos.Add(new ClientLogo { Name = s[0], ImageUrl = s[1], Kind = LogoKind.Supplier, SortOrder = i++ });

        db.ClientLogos.AddRange(logos);
        db.SaveChanges();
    }

    // ─── Processes ──────────────────────────────────────────────────

    private static void SeedProcesses(AppDbContext db)
    {
        if (db.ProcessDocuments.Any()) return;

        var items = new List<ProcessDocument>();

        void AddGroup(string key, (string? code, string title)[] entries)
        {
            for (var j = 0; j < entries.Length; j++)
                items.Add(new ProcessDocument { GroupKey = key, Code = entries[j].code, Title = entries[j].title, SortOrder = j });
        }

        AddGroup("general", [(null, "Quy trình kiểm soát tài liệu"), (null, "Quy trình đánh giá nội bộ"), (null, "Quy trình cải tiến"), (null, "Quy trình đánh giá rủi ro - cơ hội"), (null, "Quy trình xác định bối cảnh")]);
        AddGroup("ptcskh", [(null, "Quy trình phát triển và chăm sóc khách hàng"), (null, "Quy trình giải quyết khiếu nại của khách hàng"), (null, "Quy trình đo lường sự thỏa mãn của khách hàng")]);
        AddGroup("dt", [(null, "Quy trình đấu thầu"), ("DT-M02", "Phân chia công việc đấu thầu"), ("DT-M03", "Yêu cầu báo giá"), ("DT-M04", "Bảng phân chia công việc đấu thầu"), ("DT-M05", "Yêu cầu báo giá nhà cung cấp"), ("DT-M07", "Hợp đồng"), ("QLTC-QT01", "Quy trình tổng thể đấu thầu")]);
        AddGroup("tk", [("TK-M01", "Phiếu thu thập thông tin"), ("TK-PL1", "Hồ sơ thiết kế sơ bộ"), ("TK-PL2", "Thuyết minh thiết kế sơ bộ"), ("TK-BM02", "Biên bản nghiệm thu hồ sơ"), ("BM-BLĐ-QT01-08", "Biên bản bàn giao hồ sơ"), ("KT-M01", "Kiểm tra thiết kế kỹ thuật"), ("KT-BM02", "Biên bản nghiệm thu hồ sơ TKKT"), ("TK-M03", "Bàn giao hồ sơ thi công")]);
        AddGroup("tc", [("QLTC-QT02-01", "Thi công - Nghiệm thu - Bàn giao"), (null, "QT - Chuẩn bị"), (null, "QT - Duyệt mẫu vật tư và đề xuất vật tư"), (null, "QT - Duyệt và kiểm soát bản vẽ shopdrawings"), (null, "QT - Duyệt và kiểm soát tiến độ"), (null, "Danh mục hồ sơ nghiệm thu công việc"), (null, "QT - NT.Công việc"), (null, "QT - NT.Giai đoạn"), (null, "QT - NT.Bàn giao"), (null, "QT - Quản lý - điều động - bảo trì thiết bị"), (null, "Phụ lục các nguyên tắc an toàn thi công"), (null, "QT - ATLĐ_VSMT"), ("TC-M28", "Biên bản nghiệm thu nội bộ"), (null, "QT - Thầu phụ"), (null, "QT - Quản lý kho công trường"), (null, "QT - Phát sinh"), (null, "QT - Xử lý tình huống khẩn cấp"), (null, "QT - Xử lý kỷ luật")]);
        AddGroup("ttqtct", [("01.TQT-QT", "Quy trình thanh toán, quyết toán"), ("MH-M04", "Đề nghị thanh toán"), ("TC-M14", "Yêu cầu thanh toán bằng tháng"), ("TQT-M01", "Bảng tổng hợp"), ("TQT-M02", "Đề nghị thanh toán"), ("TQT-M03", "Phiếu chi"), ("TQT-M04", "Quyết toán khách hàng"), ("TQT-M06", "BB Thanh lý hợp đồng"), ("TQT-M07", "Đề nghị tạm ứng"), ("TQT-M08", "Thông báo thanh toán"), ("TQT-M09", "Công văn thanh toán")]);
        AddGroup("qlns", [(null, "Quy trình hoạch định và tuyển dụng nhân sự"), (null, "Quy trình tuyển dụng - đào tạo"), (null, "Quy trình thử việc"), (null, "Quy trình xin nghỉ việc - nghỉ phép"), ("QLNS-M01", "Phiếu yêu cầu tuyển dụng"), ("QLNS-M02", "Phiếu đăng ký dự tuyển"), ("QLNS-M03", "Hợp đồng thử việc"), ("QLNS-M04", "Bảng đánh giá thử việc"), ("QLNS-M05", "Hợp đồng lao động"), ("QLNS-M06", "Quyết định bổ nhiệm"), ("QLNS-M07", "Quyết định khen thưởng / kỷ luật"), ("QLNS-M08", "Đơn xin nghỉ phép"), ("QLNS-M09", "Đơn xin thôi việc")]);
        AddGroup("mhdgncu", [(null, "Quy trình mua hàng, đánh giá nhà cung ứng, thầu phụ"), ("MH-M02", "Yêu cầu báo giá"), ("MH-M03", "Đơn đặt hàng"), ("MH-M04", "Đề nghị thanh toán"), ("MH-M05", "Phiếu yêu cầu đánh giá NCC"), ("MH-M06", "Danh sách NCC ban đầu"), ("MH-M07", "Phiếu đánh giá NCC"), ("MH-M08", "Danh sách NCC được duyệt"), ("TC-DM-M04", "Phiếu yêu cầu vật tư")]);

        db.ProcessDocuments.AddRange(items);
        db.SaveChanges();
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

    // ─── Entity Translations (en/zh/ja for Activities & News) ───────

    private static void SeedEntityTranslations(AppDbContext db)
    {
        if (db.EntityTranslations.Any()) return;

        var translations = new List<EntityTranslation>();
        var now = DateTime.UtcNow;

        void Add(string entityType, int entityId, string field, string lang, string value)
        {
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

        // --- Activity 1: dich-vu-xay-dung-tron-goi-nicon ---
        Add(EntityTypes.Activity, 1, "Title", "en", "NICON turnkey construction — Optimal solution for workshops, factories and civil projects");
        Add(EntityTypes.Activity, 1, "Excerpt", "en", "Turnkey construction is when investors entrust the entire build process to one professional partner — from survey, estimate, design and permitting to materials, construction and handover.");
        Add(EntityTypes.Activity, 1, "Content", "en", JsonSerializer.Serialize(new[] { "In an increasingly competitive construction market, investors want shorter delivery, controlled cost and consistent quality. Design & Build (turnkey) was born to address exactly that.", "At NICON, we handle the entire value chain: concept consulting, architecture/structure/MEP design, permitting, material selection, construction and handover. A single point of responsibility saves management time and reduces risk.", "ISO 9001:2015 project management ensures rigorous quality control at every stage. Our engineers, with over 20 years in industrial and civil works, deliver the optimal solution for every project type.", "With 150+ projects delivered in major IPs like VSIP, Huu Thanh, Giang Dien and Long An, NICON is confident to be a trusted partner for your business growth." }));
        Add(EntityTypes.Activity, 1, "Title", "zh", "NICON 全包施工 — 厂房、工厂与民用项目的最佳解决方案");
        Add(EntityTypes.Activity, 1, "Excerpt", "zh", "全包施工是指投资方将整个建造过程交给一家专业单位 — 从勘察、预算、设计、许可到材料、施工与交付。");
        Add(EntityTypes.Activity, 1, "Content", "zh", JsonSerializer.Serialize(new[] { "在日益激烈的建筑市场中,投资方希望缩短交付时间、控制成本并保持质量一致。设计施工一体化(全包)正是为此而生。", "在 NICON,我们承担整个价值链:概念咨询、建筑/结构/MEP 设计、许可、材料选择、施工与交付。单一责任主体节省管理时间、降低风险。", "ISO 9001:2015 项目管理确保各阶段严格质量控制。20年以上经验的工程师团队为每类项目提供最佳方案。", "在VSIP、Huu Thanh、Giang Dien、Long An等主要工业园已完成150+项目,NICON 是您企业发展的可信合作伙伴。" }));
        Add(EntityTypes.Activity, 1, "Title", "ja", "NICONの一括施工 — 倉庫・工場・民間プロジェクトの最適ソリューション");
        Add(EntityTypes.Activity, 1, "Excerpt", "ja", "一括施工は投資家が建設プロセス全体をプロフェッショナル1社に委ねる方式です — 調査・見積・設計・許可から資材・施工・引渡しまで。");
        Add(EntityTypes.Activity, 1, "Content", "ja", JsonSerializer.Serialize(new[] { "競争が激化する建設市場において、投資家は納期短縮・コスト管理・品質の一貫性を求めます。Design & Build(一括)はそれに応える方式です。", "NICONは構想・建築/構造/MEP設計・許可・資材選定・施工・引渡しまで全工程を担います。窓口一本化で管理時間とリスクを削減します。", "ISO 9001:2015のプロジェクト管理により各段階で厳格な品質管理を実施。20年超の経験を持つエンジニアが最適解をご提供します。", "VSIP・Huu Thanh・Giang Dien・Long Anなど主要工業団地で150件超の実績があり、NICONは事業成長の信頼できるパートナーです。" }));

        // --- Activity 2: khoi-cong-bma-tay-ninh ---
        Add(EntityTypes.Activity, 2, "Title", "en", "Groundbreaking of Bao Minh An Vietnam factory at Huu Thanh IP, Tay Ninh");
        Add(EntityTypes.Activity, 2, "Excerpt", "en", "On October 19, 2025, NICON proudly broke ground for the Bao Minh An Vietnam factory at Huu Thanh Industrial Park, Duc Hoa, Tay Ninh.");
        Add(EntityTypes.Activity, 2, "Content", "en", JsonSerializer.Serialize(new[] { "On the morning of October 19, 2025, the groundbreaking ceremony took place at Huu Thanh IP, marking a new milestone in the strategic partnership between NICON and the Bao Minh An Group.", "The ceremony was attended by local authorities, the investor's board and NICON's engineering and construction team. The investor expressed confidence in NICON's construction and project management capabilities.", "The 15,000 m² factory features modern production lines and complies with international environmental and safety standards. Completion and commissioning are scheduled for Q4 2026." }));
        Add(EntityTypes.Activity, 2, "Title", "zh", "Bao Minh An 越南工厂在西宁省 Huu Thanh 工业园奠基");
        Add(EntityTypes.Activity, 2, "Excerpt", "zh", "2025年10月19日,NICON 自豪地正式启动 Bao Minh An 越南工厂项目,位于西宁省德和县 Huu Thanh 工业园。");
        Add(EntityTypes.Activity, 2, "Content", "zh", JsonSerializer.Serialize(new[] { "2025年10月19日上午,Bao Minh An 越南工厂奠基仪式在 Huu Thanh 工业园隆重举行,标志着 NICON 与 Bao Minh An 集团战略合作的新里程碑。", "出席仪式的有地方领导、投资方董事会及 NICON 的工程与施工团队。投资方代表对 NICON 的施工与项目管理能力表示信任。", "工厂占地15,000㎡,配备现代化生产线,符合国际环境与安全标准,预计将于2026年第四季度建成投产。" }));
        Add(EntityTypes.Activity, 2, "Title", "ja", "タイニン省フウタイン工業団地でBao Minh Anベトナム工場が起工");
        Add(EntityTypes.Activity, 2, "Excerpt", "ja", "2025年10月19日、NICONはタイニン省ドゥックホア県フウタイン工業団地でBao Minh Anベトナム工場プロジェクトの起工式を執り行いました。");
        Add(EntityTypes.Activity, 2, "Content", "ja", JsonSerializer.Serialize(new[] { "2025年10月19日午前、フウタイン工業団地で起工式が荘厳に行われ、NICONとBao Minh Anグループの戦略的パートナーシップにおける新たな節目となりました。", "式典には地方行政、投資家経営陣、NICONのエンジニア・施工チームが出席。投資家はNICONの施工と案件管理能力に信頼を表明しました。", "工場面積は15,000㎡、最新の生産ラインを備え、国際的な環境・労働安全基準に準拠。2026年第4四半期の完成・稼働を予定しています。" }));

        // --- Activity 3: grand-opening-trimas ---
        Add(EntityTypes.Activity, 3, "Title", "en", "Grand Opening — TriMas Factory");
        Add(EntityTypes.Activity, 3, "Excerpt", "en", "On October 13, 2025, the TriMas factory at VSIP IIA Expansion, Vinh Tan Ward, HCMC was officially inaugurated in a joyful atmosphere.");
        Add(EntityTypes.Activity, 3, "Content", "en", JsonSerializer.Serialize(new[] { "On October 13, 2025, the inauguration ceremony took place at VSIP IIA Expansion, Vinh Tan Ward, HCMC.", "Built by NICON as Design–Build general contractor across 10,000 m², the project was completed in 11 months on schedule — a key milestone in the long-term partnership between NICON and TriMas.", "The factory is equipped with cutting-edge production, MEP and environmental treatment systems meeting LEED Silver standards." }));
        Add(EntityTypes.Activity, 3, "Title", "zh", "盛大落成 — TriMas 工厂");
        Add(EntityTypes.Activity, 3, "Content", "zh", JsonSerializer.Serialize(new[] { "2025年10月13日,TriMas 工厂落成典礼在 VSIP IIA 扩展区隆重举行。", "项目由 NICON 担任设计施工总承包,占地10,000㎡,11个月按期完工,是 NICON 与 TriMas 集团长期合作的重要里程碑。", "工厂配备最先进的生产、MEP 与环境处理系统,达到 LEED Silver 标准。" }));
        Add(EntityTypes.Activity, 3, "Title", "ja", "竣工 — TriMas工場");
        Add(EntityTypes.Activity, 3, "Content", "ja", JsonSerializer.Serialize(new[] { "2025年10月13日、VSIP IIA拡張地区で竣工式が荘厳に行われました。", "NICONが設計・施工一括の総合請負業者として10,000㎡を担当し、11ヶ月で予定通り完成。NICONとTriMasの長期的な連携における重要な節目となりました。", "工場は最先端の生産・MEP・環境処理システムを備え、LEED Silver基準を満たしています。" }));

        // --- Activity 4: nicon-trien-lam-pccc-2024 ---
        Add(EntityTypes.Activity, 4, "Title", "en", "NICON at the 17th International Exhibition on Safety, Security & Fire Protection");
        Add(EntityTypes.Activity, 4, "Excerpt", "en", "NICON joined the international exhibition to update the latest technologies in safety and fire protection.");
        Add(EntityTypes.Activity, 4, "Content", "en", JsonSerializer.Serialize(new[] { "NICON proudly attended the 17th International Exhibition on safety, security and fire protection at SECC, Saigon.", "This was an opportunity to access the latest fire protection technology and enhance NICON's design and installation capabilities for factory safety systems — a critical criterion in industrial projects.", "Our engineering team had multiple technical exchanges with leading global suppliers from Japan, Germany and the US." }));

        // --- Activity 5: sinh-vien-vgu-tham-quan-stfm ---
        Add(EntityTypes.Activity, 5, "Title", "en", "VGU Architecture students visit the STFM construction site");
        Add(EntityTypes.Activity, 5, "Excerpt", "en", "On August 4, 2022, NICON hosted third-year Architecture students from Vietnamese-German University (VGU).");
        Add(EntityTypes.Activity, 5, "Content", "en", JsonSerializer.Serialize(new[] { "On August 4, 2022, NICON welcomed third-year Architecture students from VGU to tour the S.T.Food Marketing project at VSIP II-A IP.", "The visit gave students a real-world view of industrial factory construction — from steel structure and MEP to finishing. NICON engineers introduced and answered technical questions in person.", "This activity is part of the NICON–VGU partnership to nurture the next generation of architects and engineers." }));

        // --- Activity 6: khoi-cong-stfm-2021 ---
        Add(EntityTypes.Activity, 6, "Title", "en", "Groundbreaking of S.T.Food Marketing Vietnam factory");
        Add(EntityTypes.Activity, 6, "Excerpt", "en", "On June 9, 2021, as Design–Build general contractor, NICON held the groundbreaking ceremony for the S.T.FOOD MARKETING factory of a Thai investor at VSIP II-A IP.");
        Add(EntityTypes.Activity, 6, "Content", "en", JsonSerializer.Serialize(new[] { "On the morning of June 9, 2021, NICON solemnly held the groundbreaking ceremony at VSIP II-A IP, Binh Duong as Design–Build general contractor.", "The large-scale project from a Thai investor signals international confidence in NICON's general contracting capabilities.", "The project applies GMP and HACCP food safety standards, with an estimated 14-month construction period." }));

        // --- News 1: modern-fire-protection-system ---
        Add(EntityTypes.News, 1, "Title", "en", "Modern fire protection systems for high-risk factories — NICON's optimal solution");
        Add(EntityTypes.News, 1, "Excerpt", "en", "Chemical and textile factories often face elevated fire risk. Equipping a modern fire protection system is essential to safeguard assets and ensure continuous operation.");
        Add(EntityTypes.News, 1, "Content", "en", JsonSerializer.Serialize(new[] { "Factories in chemical, textile and apparel industries face high fire risks due to flammable materials, heat-generating equipment and complex production environments.", "NICON delivers turnkey fire protection: sprinkler design, addressable fire alarms, CO2/foam suppression and fire-rated partitions per TCVN 3890:2009 and international NFPA standards.", "Our certified fire safety engineers ensure smooth approval submissions and acceptance handover." }));

        // --- News 2: build-concept-for-design-project ---
        Add(EntityTypes.News, 2, "Title", "en", "Building the concept for a design project");
        Add(EntityTypes.News, 2, "Excerpt", "en", "Creating the concept for a residential project is the first and crucial step that shapes ideas and harmonizes aesthetics, function and real needs.");
        Add(EntityTypes.News, 2, "Content", "en", JsonSerializer.Serialize(new[] { "Design concept is the foundation guiding the entire construction process. A great concept reflects the personality and story of the home.", "NICON builds concepts on three pillars: client research, trend reference and material experimentation. Every project gets a 3D moodboard before detailed development.", "This process ensures clients see the final outcome before construction, avoiding costly revisions." }));

        // --- Activity 7: scholarships-awarding-2021 ---
        Add(EntityTypes.Activity, 7, "Title", "en", "Scholarships awarded to outstanding students");
        Add(EntityTypes.Activity, 7, "Excerpt", "en", "In late 2021, NICON proudly awarded 35 scholarships to students of Phu My High School No. 1, Binh Dinh.");

        // --- Activity 8: grand-opening-hbfuller ---
        Add(EntityTypes.Activity, 8, "Title", "en", "Grand Opening of H.B.Fuller Vietnam Factory");
        Add(EntityTypes.Activity, 8, "Excerpt", "en", "The H.B.Fuller Vietnam factory at VSIP II-A IP, Tan Uyen, Binh Duong was officially inaugurated.");

        // --- Activity 9: great-lotus-steel-structure ---
        Add(EntityTypes.Activity, 9, "Title", "en", "NICON completes steel structure for Great Lotus Vietnam Factory");
        Add(EntityTypes.Activity, 9, "Excerpt", "en", "On May 10, 2019, NICON completed the steel structure for the 31,000 m² Great Lotus Vietnam Factory.");

        // --- Activity 10: nicon-top10-vietnam-brand-2018 ---
        Add(EntityTypes.Activity, 10, "Title", "en", "Japanese media: NICON in Top 10 Vietnam Leading Brands 2018");
        Add(EntityTypes.Activity, 10, "Excerpt", "en", "NICON was honored as a Top 10 Vietnam Leading Brand 2018, featured by Japanese media.");

        // --- Activity 11: nicon-mori-strategic-cooperation ---
        Add(EntityTypes.Activity, 11, "Title", "en", "NICON – Mori: Strategic Cooperation");
        Add(EntityTypes.Activity, 11, "Excerpt", "en", "Mori Construction from Japan expanded international cooperation with NICON.");

        // --- Activity 12: training-improvement-2018 ---
        Add(EntityTypes.Activity, 12, "Title", "en", "Training for improvement — Quality, safety and innovation");
        Add(EntityTypes.Activity, 12, "Excerpt", "en", "Training and education are regular activities at NICON to enhance quality management, schedule control and site safety.");

        // --- Activity 13: nicon-annual-trip-2018 ---
        Add(EntityTypes.Activity, 13, "Title", "en", "NICON Annual Trip & Team Building");
        Add(EntityTypes.Activity, 13, "Excerpt", "en", "The annual trip gives all NICON members a wonderful time and great experiences after a year of hard work.");

        // --- Activity 14: nha-xuong-vda-hcm ---
        Add(EntityTypes.Activity, 14, "Title", "en", "Groundbreaking of VDA-HCM new factory");
        Add(EntityTypes.Activity, 14, "Excerpt", "en", "On December 23, 2016, NICON and VDA-HCM broke ground for the new factory at Cau Tram IP, Long An.");

        // --- Activity 15: nha-may-amiba-db ---
        Add(EntityTypes.Activity, 15, "Title", "en", "AMIBA Factory Project — Design & Build");
        Add(EntityTypes.Activity, 15, "Excerpt", "en", "On December 29, 2017, NICON and AMIBA held the groundbreaking for a 2-hectare factory at VSIP II-A, Binh Duong.");

        // --- Activity 16: ky-ket-nicon-mori ---
        Add(EntityTypes.Activity, 16, "Title", "en", "NICON & Mori strategic partnership signing");
        Add(EntityTypes.Activity, 16, "Excerpt", "en", "On June 19, 2016, CEO Vo Tri Nguyen (NICON) and CEO Yoshihiro Mori (Mori Industrial Group) signed a strategic partnership agreement.");

        // --- News 7-20: English translations for new news articles ---
        Add(EntityTypes.News, 7, "Title", "en", "NIHOME Trends — Interior Design Trends 2024");
        Add(EntityTypes.News, 7, "Excerpt", "en", "Discover the top interior design trends of 2024 that NIHOME is applying in service apartment projects.");

        Add(EntityTypes.News, 8, "Title", "en", "5 Factories with the Most Unique Architecture in the World");
        Add(EntityTypes.News, 8, "Excerpt", "en", "Counting down 5 industrial factories with impressive, breakthrough architecture that inspires industrial construction.");

        Add(EntityTypes.News, 9, "Title", "en", "Industrial Factory Construction Process from A to Z");
        Add(EntityTypes.News, 9, "Excerpt", "en", "A comprehensive guide to the industrial factory construction process from project initiation to operational handover.");

        Add(EntityTypes.News, 10, "Title", "en", "Aesthetic Factors in Industrial Design");
        Add(EntityTypes.News, 10, "Excerpt", "en", "Aesthetics are not just for civil works — factories also need beautiful design to enhance brand value.");

        Add(EntityTypes.News, 11, "Title", "en", "Healing in Architecture — Healing Spaces");
        Add(EntityTypes.News, 11, "Excerpt", "en", "The healing architecture trend is being widely applied in modern architecture design.");

        Add(EntityTypes.News, 12, "Title", "en", "Latest Fire Safety Standards 2024 for Industrial Factories");
        Add(EntityTypes.News, 12, "Excerpt", "en", "Updated fire prevention and fighting regulations and standards for 2024 applicable to factories and workshops.");

        Add(EntityTypes.News, 13, "Title", "en", "Beautiful Home Trends 2025 — Minimalist and Sustainable");
        Add(EntityTypes.News, 13, "Excerpt", "en", "The standout residential design trends expected to dominate 2025: minimalist, sustainable and smart.");

        Add(EntityTypes.News, 14, "Title", "en", "General Contractor for Food Factories — GMP/HACCP Standards");
        Add(EntityTypes.News, 14, "Excerpt", "en", "NICON shares experience as general contractor for food factory design and construction meeting international standards.");

        Add(EntityTypes.News, 15, "Title", "en", "S.T.Food & Industrial Design — Case Study");
        Add(EntityTypes.News, 15, "Excerpt", "en", "Detailed analysis of the S.T.Food Marketing Vietnam factory project — from concept to handover.");

        Add(EntityTypes.News, 16, "Title", "en", "Architectural Design Expressing Brand Spirit");
        Add(EntityTypes.News, 16, "Excerpt", "en", "Factory architecture is not just a place of production — it is the face of the corporate brand.");

        Add(EntityTypes.News, 17, "Title", "en", "Open Space Trend in Office and Factory Design");
        Add(EntityTypes.News, 17, "Excerpt", "en", "Open space is becoming the dominant trend in modern office and factory design.");

        Add(EntityTypes.News, 18, "Title", "en", "The Success Journey of a Food Factory");
        Add(EntityTypes.News, 18, "Excerpt", "en", "Sharing the journey from idea to successful operation of a food factory in Vietnam.");

        Add(EntityTypes.News, 19, "Title", "en", "APM Warehouse Handover — D&B Project Completed");
        Add(EntityTypes.News, 19, "Excerpt", "en", "NICON completed the handover of APM warehouse to Auto Components Vietnam at VSIP IP, Binh Duong.");

        Add(EntityTypes.News, 20, "Title", "en", "NICON and Vietnam's Steel Structure Industry");
        Add(EntityTypes.News, 20, "Excerpt", "en", "NICON shares insights on the role of steel structures in modern industrial construction in Vietnam.");

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

        db.EntityTranslations.AddRange(translations);
        db.SaveChanges();
    }
}
