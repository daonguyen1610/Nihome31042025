using NihomeBackend.Models;

namespace NihomeBackend.Data;

/// <summary>
/// Populates a small, deterministic set of sample rows for the CRM features
/// (Leads + Customers + primary contact + one activity each) so a freshly
/// migrated database has data to demo bulk-select, filters and detail
/// dialogs without hand-crafting curl calls. Idempotent: every insert is
/// guarded so re-running is a no-op.
/// </summary>
public static class SampleCrmDataSeeder
{
    private const string SampleTag = "[SAMPLE]";

    public static void Seed(AppDbContext db)
    {
        var now = DateTime.UtcNow;

        // Owner defaults to the SALE test user seeded by DbSeeder, so the
        // sample data plays well with the RBAC ownership scoping tested in
        // integration + e2e specs. If SALE is missing (e.g. a partial seed),
        // fall back to the first admin.
        var owner = db.Users.FirstOrDefault(u => u.PhoneNumber == "0911000003")
                    ?? db.Users.FirstOrDefault(u => u.Role == UserRole.SUPER_ADMIN)
                    ?? db.Users.FirstOrDefault();
        if (owner is null) return;

        SeedLeads(db, owner, now);
        SeedCustomers(db, owner, now);
    }

    private static void SeedLeads(AppDbContext db, ApplicationUser owner, DateTime now)
    {
        var samples = new[]
        {
            new Lead
            {
                Name = $"{SampleTag} Nguyễn Minh Anh",
                Phone = "0900000101",
                Email = "minh.anh.sample@example.com",
                SourceCode = "marketing",
                Status = LeadStatus.New,
                Note = "Lead mẫu từ chiến dịch Facebook Ads.",
            },
            new Lead
            {
                Name = $"{SampleTag} Trần Thị Bích",
                CompanyName = "Công ty TNHH Bích Anh",
                Phone = "0900000102",
                Email = "bich.anh.sample@example.com",
                SourceCode = "referral",
                Status = LeadStatus.Contacted,
                Note = "Lead mẫu do khách hàng cũ giới thiệu.",
            },
            new Lead
            {
                Name = $"{SampleTag} Phạm Quốc Cường",
                Phone = "0900000103",
                SourceCode = "website",
                Status = LeadStatus.Interested,
                Note = "Đã liên hệ qua form website, quan tâm gói cao cấp.",
            },
            new Lead
            {
                Name = $"{SampleTag} Lê Hồng Duyên",
                Email = "duyen.sample@example.com",
                SourceCode = "event",
                Status = LeadStatus.New,
                Note = "Ghi nhận tại sự kiện triển lãm nội thất tháng 10.",
            },
            new Lead
            {
                Name = $"{SampleTag} Đỗ Thanh Ê",
                Phone = "0900000105",
                SourceCode = "cold-call",
                Status = LeadStatus.NotInterested,
                Note = "Chưa có nhu cầu, hẹn liên hệ lại quý sau.",
            },
        };

        foreach (var s in samples)
        {
            var exists = db.Leads.Any(l => l.Name == s.Name);
            if (exists) continue;

            s.OwnerUserId = owner.Id;
            s.CreatedByUserId = owner.Id;
            s.UpdatedByUserId = owner.Id;
            s.CreatedAt = now;
            s.UpdatedAt = now;

            db.Leads.Add(s);
            db.SaveChanges();

            db.LeadActivities.Add(new LeadActivity
            {
                LeadId = s.Id,
                Type = LeadActivityType.Note,
                Content = "Ghi chú mẫu khởi tạo cho lead demo.",
                CreatedByUserId = owner.Id,
                CreatedAt = now,
            });
            db.SaveChanges();
        }
    }

    private static void SeedCustomers(AppDbContext db, ApplicationUser owner, DateTime now)
    {
        var samples = new List<(Customer Customer, CustomerContact Contact)>
        {
            (
                new Customer
                {
                    Type = CustomerType.Individual,
                    Name = $"{SampleTag} Nguyễn Văn An",
                    SourceCode = "marketing",
                    RelationshipStatus = CustomerRelationshipStatus.Prospect,
                    Note = "Khách hàng cá nhân mẫu — kênh Marketing.",
                },
                new CustomerContact
                {
                    FullName = "Nguyễn Văn An",
                    Phone = "0900000201",
                    Email = "an.sample@example.com",
                    IsPrimary = true,
                }
            ),
            (
                new Customer
                {
                    Type = CustomerType.Individual,
                    Name = $"{SampleTag} Trần Thị Bảo",
                    SourceCode = "referral",
                    RelationshipStatus = CustomerRelationshipStatus.InProgress,
                    Note = "Khách hàng cá nhân đang trong quá trình tư vấn.",
                },
                new CustomerContact
                {
                    FullName = "Trần Thị Bảo",
                    Phone = "0900000202",
                    IsPrimary = true,
                }
            ),
            (
                new Customer
                {
                    Type = CustomerType.Company,
                    Name = $"{SampleTag} Công ty CP Xây dựng Alpha",
                    TaxId = "0100000001",
                    Address = "Số 1, Đường Alpha, Q.1, TP.HCM",
                    RepresentativeName = "Ông Nguyễn Văn Alpha",
                    SourceCode = "website",
                    RelationshipStatus = CustomerRelationshipStatus.Prospect,
                    Note = "Khách hàng doanh nghiệp mẫu — mảng B2B.",
                },
                new CustomerContact
                {
                    FullName = "Nguyễn Thị Anh",
                    Position = "Trưởng phòng Mua hàng",
                    Phone = "0900000301",
                    Email = "purchase.alpha.sample@example.com",
                    IsPrimary = true,
                }
            ),
            (
                new Customer
                {
                    Type = CustomerType.Company,
                    Name = $"{SampleTag} Công ty TNHH Beta Interior",
                    TaxId = "0100000002",
                    Address = "Số 2, Đường Beta, Q.3, TP.HCM",
                    RepresentativeName = "Bà Lê Thị Beta",
                    SourceCode = "referral",
                    RelationshipStatus = CustomerRelationshipStatus.Signed,
                    Note = "Khách hàng đã ký hợp đồng — dùng làm demo cho tab hợp đồng.",
                },
                new CustomerContact
                {
                    FullName = "Phạm Quốc Bảo",
                    Position = "Giám đốc dự án",
                    Phone = "0900000302",
                    Email = "pm.beta.sample@example.com",
                    IsPrimary = true,
                }
            ),
            (
                new Customer
                {
                    Type = CustomerType.Company,
                    Name = $"{SampleTag} Công ty CP Gamma Home",
                    TaxId = "0100000003",
                    Address = "Số 3, Đường Gamma, Q.7, TP.HCM",
                    RepresentativeName = "Ông Trần Văn Gamma",
                    SourceCode = "event",
                    RelationshipStatus = CustomerRelationshipStatus.InProgress,
                    Note = "Khách hàng gặp tại sự kiện triển lãm.",
                },
                new CustomerContact
                {
                    FullName = "Lê Hồng Cường",
                    Position = "Kế toán trưởng",
                    Phone = "0900000303",
                    IsPrimary = true,
                }
            ),
        };

        foreach (var (customer, contact) in samples)
        {
            if (db.Customers.Any(c => c.Name == customer.Name)) continue;

            customer.OwnerUserId = owner.Id;
            customer.CreatedByUserId = owner.Id;
            customer.UpdatedByUserId = owner.Id;
            customer.CreatedAt = now;
            customer.UpdatedAt = now;

            db.Customers.Add(customer);
            db.SaveChanges();

            contact.CustomerId = customer.Id;
            contact.CreatedAt = now;
            contact.UpdatedAt = now;
            db.CustomerContacts.Add(contact);

            db.CustomerActivities.Add(new CustomerActivity
            {
                CustomerId = customer.Id,
                Type = CustomerActivityType.Note,
                OccurredAt = now,
                Content = "Ghi chú mẫu khởi tạo cho khách hàng demo.",
                CreatedByUserId = owner.Id,
                CreatedAt = now,
            });

            db.SaveChanges();
        }
    }
}
