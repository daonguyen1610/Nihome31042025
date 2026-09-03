using NihomeBackend.Models;
using NihomeBackend.Services;

namespace NihomeBackend.Data;

public static class DbSeeder
{
    private const string SiteName = "NICON";
    private const string SiteDescription = "Tư vấn thiết kế và thi công trọn gói nhà máy, nhà xưởng và công trình dân dụng.";
    private const string SiteAddress = "92 Đường 56, Phường Bình Trưng, TP. Hồ Chí Minh";
    private const string SiteMapUrl = "https://www.google.com/maps?q=92+%C4%90%C6%B0%E1%BB%9Dng+56%2C+B%C3%ACnh+Tr%C6%B0ng%2C+H%E1%BB%93+Ch%C3%AD+Minh+700000%2C+Vietnam&output=embed";

    /// <summary>
    /// Seed the database with baseline users, RBAC, master-data, content
    /// translations and sample CRM rows. <paramref name="webRootPath"/>
    /// is optional — when provided, seeders that need to drop physical
    /// demo assets (e.g. capability-document PDFs so download links
    /// resolve on a fresh install) know where to write them.
    /// </summary>
    public static void Seed(AppDbContext db, string? webRootPath = null)
    {
        var now = DateTime.UtcNow;

        SeedCanonicalAdminUsers(db);

        if (!db.SiteSettings.Any())
        {
            db.SiteSettings.Add(new SiteSettings
            {
                SiteName = SiteName,
                SiteDescription = SiteDescription,
                PrimaryEmail = "info@nihome.vn",
                SecondaryEmail = "projects@nihome.vn",
                PrimaryPhone = "028 7300 1976",
                SecondaryPhone = "+84 90 000 2006",
                Address = SiteAddress,
                MapEmbedUrl = SiteMapUrl,
                EnableOtpForRegistration = true,
                EnableOtpForForgotPassword = true,
                OtpEmailSubjectTemplate = EmailTemplateFormatter.DefaultOtpSubject,
                OtpEmailBodyTemplate = EmailTemplateFormatter.DefaultOtpBody,
                NewApplicationEmailSubjectTemplate = NihomeBackend.Services.EmailTemplateFormatter.DefaultNewApplicationSubject,
                NewApplicationEmailBodyTemplate = NihomeBackend.Services.EmailTemplateFormatter.DefaultNewApplicationBody,
                NotificationEmail = "nihome@nihome.vn",
                CreatedAt = now,
                UpdatedAt = now
            });
            db.SaveChanges();
        }

        var existingSettings = db.SiteSettings.OrderBy(settings => settings.Id).FirstOrDefault();
        if (existingSettings != null)
        {
            var updated = false;

            if (existingSettings.SiteName == "Nihome")
            {
                existingSettings.SiteName = SiteName;
                updated = true;
            }
            if (existingSettings.SiteDescription == "Căn hộ dịch vụ cao cấp - Không gian sống tiện nghi")
            {
                existingSettings.SiteDescription = SiteDescription;
                updated = true;
            }
            if (existingSettings.PrimaryEmail == "nihome@nihome.vn")
            {
                existingSettings.PrimaryEmail = "info@nihome.vn";
                updated = true;
            }
            if (existingSettings.SecondaryEmail == "booking@nihome.vn")
            {
                existingSettings.SecondaryEmail = "projects@nihome.vn";
                updated = true;
            }
            if (existingSettings.PrimaryPhone == "1900 3311")
            {
                existingSettings.PrimaryPhone = "028 7300 1976";
                updated = true;
            }
            if (existingSettings.SecondaryPhone == "+84 987 654 321")
            {
                existingSettings.SecondaryPhone = "+84 90 000 2006";
                updated = true;
            }
            if (existingSettings.Address == "92 Đường 56, Bình Trưng, Hồ Chí Minh 700000, Vietnam")
            {
                existingSettings.Address = SiteAddress;
                updated = true;
            }
            if (string.IsNullOrWhiteSpace(existingSettings.MapEmbedUrl))
            {
                existingSettings.MapEmbedUrl = SiteMapUrl;
                updated = true;
            }

            if (string.IsNullOrWhiteSpace(existingSettings.OtpEmailSubjectTemplate))
            {
                existingSettings.OtpEmailSubjectTemplate = EmailTemplateFormatter.DefaultOtpSubject;
                updated = true;
            }

            if (string.IsNullOrWhiteSpace(existingSettings.OtpEmailBodyTemplate) ||
                EmailTemplateFormatter.IsLegacyDefaultOtpBody(existingSettings.OtpEmailBodyTemplate))
            {
                existingSettings.OtpEmailBodyTemplate = EmailTemplateFormatter.DefaultOtpBody;
                updated = true;
            }

            if (string.IsNullOrWhiteSpace(existingSettings.NewApplicationEmailSubjectTemplate))
            {
                existingSettings.NewApplicationEmailSubjectTemplate = NihomeBackend.Services.EmailTemplateFormatter.DefaultNewApplicationSubject;
                updated = true;
            }

            if (string.IsNullOrWhiteSpace(existingSettings.NewApplicationEmailBodyTemplate))
            {
                existingSettings.NewApplicationEmailBodyTemplate = NihomeBackend.Services.EmailTemplateFormatter.DefaultNewApplicationBody;
                updated = true;
            }

            if (string.IsNullOrWhiteSpace(existingSettings.NotificationEmail))
            {
                existingSettings.NotificationEmail = existingSettings.PrimaryEmail ?? "nihome@nihome.vn";
                updated = true;
            }

            if (updated)
            {
                existingSettings.UpdatedAt = now;
                db.SaveChanges();
            }
        }

        ContentSeeder.Seed(db);
        TranslationSeeder.Seed(db);
        RbacSeeder.Seed(db);
        MasterDataSeeder.Seed(db);
        WorkflowConfigSeeder.Seed(db);
        NotificationTemplateSeeder.Seed(db);
        SeedBusinessRoleUsers(db);
        SampleCrmDataSeeder.Seed(db, webRootPath);
    }

    private static readonly (string Phone, string FullName, string Email, UserRole Role)[] _canonicalAdminUsers =
    [
        ("0335240370", "Super Admin", "superadmin@nihome.vn", UserRole.SUPER_ADMIN),
        ("0911111111", "Lê Thảo Vy", "ops.admin@nihome.vn", UserRole.ADMIN),
        ("0922222222", "Nguyễn Quốc Bảo", "leasing.admin@nihome.vn", UserRole.ADMIN),
    ];

    private static void SeedCanonicalAdminUsers(AppDbContext db)
    {
        var existingPhones = db.Users.Select(user => user.PhoneNumber).ToHashSet(StringComparer.Ordinal);
        var existingEmails = db.Users.Select(user => user.Email).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var passwordService = new PasswordService();

        foreach (var (phone, fullName, preferredEmail, role) in _canonicalAdminUsers)
        {
            if (existingPhones.Contains(phone)) continue;

            var email = ResolveSeedEmail(preferredEmail, phone, existingEmails);

            var user = new ApplicationUser
            {
                PhoneNumber = phone,
                FullName = fullName,
                Email = email,
                Role = role,
                IsActive = true,
            };
            user.PasswordHash = passwordService.Hash(user, "Admin@123");
            db.Users.Add(user);
            existingPhones.Add(phone);
            existingEmails.Add(email);
        }

        db.SaveChanges();
    }

    // Phone numbers used here are stable, predictable test credentials so the
    // RBAC test matrix in docs/users-rbac.md and the playwright/integration
    // tests can always log in as any role.
    private static readonly (string RoleCode, string Phone, string FullName, string Email, string? LegacyName, string? LegacyEmail)[] _businessRoleUsers =
    [
        ("SALE",            "0911000003", "Nguyễn Minh Anh",  "minh.anh.sale@nihome.vn",       "Sale Tester",          "sale.test@nihome.vn"),
        ("SALES_MANAGER",   "0911000010", "Trần Quốc Huy",    "quoc.huy.sales@nihome.vn",      "Sales Manager Tester", "sales.manager.test@nihome.vn"),
        ("DESIGN",          "0911000004", "Lê Hoàng Nam",     "hoang.nam.design@nihome.vn",    "Design Tester",        "design.test@nihome.vn"),
        ("DESIGN_LEAD",     "0911000011", "Phạm Thu Hà",      "thu.ha.design@nihome.vn",       null,                   null),
        ("ARCHITECT",       "0911000012", "Vũ Đức Long",      "duc.long.architect@nihome.vn",  null,                   null),
        ("MEP_ENGINEER",    "0911000013", "Đỗ Thành Công",    "thanh.cong.mep@nihome.vn",      null,                   null),
        ("STRUCT_ENGINEER", "0911000014", "Bùi Quang Khải",   "quang.khai.struct@nihome.vn",   null,                   null),
        ("PM",              "0911000005", "Ngô Tuấn Kiệt",    "tuan.kiet.pm@nihome.vn",        "PM Tester",            "pm.test@nihome.vn"),
        ("LEGAL_OFFICER",   "0911000015", "Đặng Ngọc Mai",    "ngoc.mai.legal@nihome.vn",      null,                   null),
        ("QS",              "0911000006", "Hoàng Gia Bảo",    "gia.bao.qs@nihome.vn",          "QS Tester",            "qs.test@nihome.vn"),
        ("ACCOUNTANT",      "0911000007", "Nguyễn Thùy Linh", "thuy.linh.accounting@nihome.vn", "Accountant Tester",    "accountant.test@nihome.vn"),
        ("WAREHOUSE",       "0911000008", "Trịnh Văn Sơn",    "van.son.warehouse@nihome.vn",   "Warehouse Tester",     "warehouse.test@nihome.vn"),
        ("BGD",             "0911000009", "Phan Anh Dũng",    "anh.dung.bgd@nihome.vn",        "BGD Tester",           "bgd.test@nihome.vn"),
    ];

    private static void SeedBusinessRoleUsers(AppDbContext db)
    {
        var passwordService = new PasswordService();
        var rolesByCode = db.Roles
            .Where(r => !r.IsSystem)
            .ToDictionary(r => r.Code, r => r.Id, StringComparer.OrdinalIgnoreCase);
        var existingEmails = db.Users.Select(user => user.Email).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (code, phone, fullName, preferredEmail, legacyName, legacyEmail) in _businessRoleUsers)
        {
            if (!rolesByCode.TryGetValue(code, out var roleId)) continue;

            var existingUser = db.Users.FirstOrDefault(u => u.PhoneNumber == phone);
            if (existingUser is not null)
            {
                var hasLegacyIdentity = (legacyName is not null && existingUser.FullName == legacyName)
                    || (legacyEmail is not null && existingUser.Email == legacyEmail);
                if (!existingUser.RoleEntityId.HasValue || hasLegacyIdentity)
                {
                    existingUser.RoleEntityId = roleId;
                }
                if (legacyName is not null && existingUser.FullName == legacyName)
                {
                    existingUser.FullName = fullName;
                }
                if (legacyEmail is not null && existingUser.Email == legacyEmail)
                {
                    existingUser.Email = ResolveSeedEmail(preferredEmail, phone, existingEmails);
                }
                continue;
            }

            var email = ResolveSeedEmail(preferredEmail, phone, existingEmails);
            var user = new ApplicationUser
            {
                PhoneNumber = phone,
                FullName = fullName,
                Email = email,
                // Business roles live outside the legacy 3-value enum; the
                // canonical role link is RoleEntityId, and PermissionService
                // reads from there first.
                Role = UserRole.USER,
                RoleEntityId = roleId,
                IsActive = true,
            };
            user.PasswordHash = passwordService.Hash(user, "Admin@123");
            db.Users.Add(user);
        }

        db.SaveChanges();
    }

    private static string ResolveSeedEmail(string preferredEmail, string phone,
        HashSet<string> existingEmails)
    {
        var email = preferredEmail;
        if (existingEmails.Contains(email))
        {
            email = $"seed.{phone}@nihome.vn";
            var suffix = 1;
            while (existingEmails.Contains(email))
            {
                email = $"seed.{phone}.{suffix}@nihome.vn";
                suffix++;
            }
        }

        existingEmails.Add(email);
        return email;
    }
}
