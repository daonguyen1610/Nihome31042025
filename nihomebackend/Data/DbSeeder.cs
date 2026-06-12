using NihomeBackend.Models;
using NihomeBackend.Services;

namespace NihomeBackend.Data;

public static class DbSeeder
{
    public static void Seed(AppDbContext db)
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
    }
}
