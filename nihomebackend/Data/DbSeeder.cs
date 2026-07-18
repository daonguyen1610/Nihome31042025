using NihomeBackend.Models;
using NihomeBackend.Services;

namespace NihomeBackend.Data;

public static class DbSeeder
{
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

        if (!db.Users.Any(u => u.Role == UserRole.SUPER_ADMIN))
        {
            var superAdmin = new ApplicationUser
            {
                PhoneNumber = "0335240370",
                FullName = "Super Admin",
                Email = "superadmin@nihome.vn",
                Role = UserRole.SUPER_ADMIN,
                IsActive = true
            };

            var passwordService = new PasswordService();
            superAdmin.PasswordHash = passwordService.Hash(superAdmin, "Admin@123");

            db.Users.Add(superAdmin);
            db.SaveChanges();
        }

        if (!db.Users.Any(u => u.Role == UserRole.ADMIN))
        {
            var passwordService = new PasswordService();
            var adminUsers = new[]
            {
                new ApplicationUser
                {
                    PhoneNumber = "0911111111",
                    FullName = "Lê Thảo Vy",
                    Email = "ops.admin@nihome.vn",
                    Role = UserRole.ADMIN,
                    IsActive = true
                },
                new ApplicationUser
                {
                    PhoneNumber = "0922222222",
                    FullName = "Nguyễn Quốc Bảo",
                    Email = "leasing.admin@nihome.vn",
                    Role = UserRole.ADMIN,
                    IsActive = true
                }
            };

            foreach (var admin in adminUsers)
            {
                admin.PasswordHash = passwordService.Hash(admin, "Admin@123");
            }

            db.Users.AddRange(adminUsers);
            db.SaveChanges();
        }

        if (!db.SiteSettings.Any())
        {
            db.SiteSettings.Add(new SiteSettings
            {
                SiteName = "Nihome",
                SiteDescription = "Căn hộ dịch vụ cao cấp - Không gian sống tiện nghi",
                PrimaryEmail = "nihome@nihome.vn",
                SecondaryEmail = "booking@nihome.vn",
                PrimaryPhone = "1900 3311",
                SecondaryPhone = "+84 987 654 321",
                Address = "92 Đường 56, Bình Trưng, Hồ Chí Minh 700000, Vietnam",
                MapEmbedUrl = "https://www.google.com/maps?q=92+%C4%90%C6%B0%E1%BB%9Dng+56%2C+B%C3%ACnh+Tr%C6%B0ng%2C+H%E1%BB%93+Ch%C3%AD+Minh+700000%2C+Vietnam&output=embed",
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

        var existingSettings = db.SiteSettings.FirstOrDefault();
        if (existingSettings != null)
        {
            var updated = false;

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

            if (string.IsNullOrWhiteSpace(existingSettings.MapEmbedUrl))
            {
                existingSettings.MapEmbedUrl = "https://www.google.com/maps?q=92+%C4%90%C6%B0%E1%BB%9Dng+56%2C+B%C3%ACnh+Tr%C6%B0ng%2C+H%E1%BB%93+Ch%C3%AD+Minh+700000%2C+Vietnam&output=embed";
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

    // Phone numbers used here are stable, predictable test credentials so the
    // RBAC test matrix in docs/users-rbac.md and the playwright/integration
    // tests can always log in as any role.
    private static readonly (string RoleCode, string Phone, string FullName, string Email)[] _businessRoleUsers =
    [
        ("SALE",           "0911000003", "Sale Tester",           "sale.test@nihome.vn"),
        ("SALES_MANAGER",  "0911000010", "Sales Manager Tester",  "sales.manager.test@nihome.vn"),
        ("DESIGN",         "0911000004", "Design Tester",         "design.test@nihome.vn"),
        ("PM",             "0911000005", "PM Tester",             "pm.test@nihome.vn"),
        ("QS",             "0911000006", "QS Tester",             "qs.test@nihome.vn"),
        ("ACCOUNTANT",     "0911000007", "Accountant Tester",     "accountant.test@nihome.vn"),
        ("WAREHOUSE",      "0911000008", "Warehouse Tester",      "warehouse.test@nihome.vn"),
        ("BGD",            "0911000009", "BGD Tester",            "bgd.test@nihome.vn"),
    ];

    private static void SeedBusinessRoleUsers(AppDbContext db)
    {
        var passwordService = new PasswordService();
        var rolesByCode = db.Roles
            .Where(r => !r.IsSystem)
            .ToDictionary(r => r.Code, r => r.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var (code, phone, fullName, email) in _businessRoleUsers)
        {
            if (!rolesByCode.TryGetValue(code, out var roleId)) continue;
            if (db.Users.Any(u => u.PhoneNumber == phone)) continue;

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
}
