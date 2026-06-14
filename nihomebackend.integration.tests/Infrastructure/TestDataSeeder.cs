using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Services;

namespace NihomeBackend.IntegrationTests.Infrastructure;

/// <summary>
/// Minimal, deterministic seed for integration tests.
/// Avoids the production ContentSeeder/TranslationSeeder for speed.
/// </summary>
public static class TestDataSeeder
{
    public const string SuperAdminPhone = "0335240370";
    public const string AdminPhone = "0911111111";
    public const string CustomerPhone = "0900000001";
    public const string DefaultPassword = "Admin@123";

    public static void Seed(AppDbContext db)
    {
        var password = new PasswordService();
        var now = DateTime.UtcNow;

        if (!db.Users.Any())
        {
            var superAdmin = new ApplicationUser
            {
                PhoneNumber = SuperAdminPhone,
                FullName = "Super Admin",
                Email = "superadmin@nihome.test",
                Role = UserRole.SUPER_ADMIN,
                IsActive = true,
            };
            superAdmin.PasswordHash = password.Hash(superAdmin, DefaultPassword);

            var admin = new ApplicationUser
            {
                PhoneNumber = AdminPhone,
                FullName = "Admin User",
                Email = "admin@nihome.test",
                Role = UserRole.ADMIN,
                IsActive = true,
            };
            admin.PasswordHash = password.Hash(admin, DefaultPassword);

            var customer = new ApplicationUser
            {
                PhoneNumber = CustomerPhone,
                FullName = "Customer User",
                Email = "customer@nihome.test",
                Role = UserRole.USER,
                IsActive = true,
            };
            customer.PasswordHash = password.Hash(customer, DefaultPassword);

            db.Users.AddRange(superAdmin, admin, customer);
            db.SaveChanges();
        }

        // Seed RBAC tables (roles, permissions, role_permissions, user-role
        // backfill) so endpoints depending on them can run.
        RbacSeeder.Seed(db);

        if (!db.SiteSettings.Any())
        {
            db.SiteSettings.Add(new SiteSettings
            {
                SiteName = "Nihome Test",
                SiteDescription = "Integration test instance",
                PrimaryEmail = "tests@nihome.test",
                PrimaryPhone = "0000000000",
                Address = "Test Address",
                EnableOtpForRegistration = false,
                EnableOtpForForgotPassword = false,
                OtpEmailSubjectTemplate = EmailTemplateFormatter.DefaultOtpSubject,
                OtpEmailBodyTemplate = EmailTemplateFormatter.DefaultOtpBody,
                NewApplicationEmailSubjectTemplate = EmailTemplateFormatter.DefaultNewApplicationSubject,
                NewApplicationEmailBodyTemplate = EmailTemplateFormatter.DefaultNewApplicationBody,
                NotificationEmail = "notify@nihome.test",
                CreatedAt = now,
                UpdatedAt = now,
            });
            db.SaveChanges();
        }
    }
}
