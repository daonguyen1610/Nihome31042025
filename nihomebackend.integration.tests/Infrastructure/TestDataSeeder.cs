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

    /// <summary>
    /// One test user per business role. Keys are the role <c>Code</c> exposed
    /// by <see cref="NihomeBackend.Data.RbacSeedData"/>; values are stable
    /// phone numbers reserved for tests. Kept in sync with
    /// <see cref="NihomeBackend.Data.DbSeeder"/> so dev and tests share the
    /// same credentials.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> BusinessRolePhonesByCode =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SALE"] = "0911000003",
            ["DESIGN"] = "0911000004",
            ["PM"] = "0911000005",
            ["QS"] = "0911000006",
            ["ACCOUNTANT"] = "0911000007",
            ["WAREHOUSE"] = "0911000008",
            ["BGD"] = "0911000009",
        };

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

        SeedBusinessRoleUsers(db, password);

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

    private static void SeedBusinessRoleUsers(AppDbContext db, PasswordService password)
    {
        var rolesByCode = db.Roles
            .Where(r => !r.IsSystem)
            .ToDictionary(r => r.Code, r => r.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var (code, phone) in BusinessRolePhonesByCode)
        {
            if (!rolesByCode.TryGetValue(code, out var roleId)) continue;
            if (db.Users.Any(u => u.PhoneNumber == phone)) continue;

            var user = new ApplicationUser
            {
                PhoneNumber = phone,
                FullName = $"{code} Tester",
                Email = $"{code.ToLowerInvariant()}.test@nihome.test",
                Role = UserRole.USER,
                RoleEntityId = roleId,
                IsActive = true,
            };
            user.PasswordHash = password.Hash(user, DefaultPassword);
            db.Users.Add(user);
        }

        db.SaveChanges();
    }
}
