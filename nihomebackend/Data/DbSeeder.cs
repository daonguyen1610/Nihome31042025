using NihomeBackend.Models;
using NihomeBackend.Services;

namespace NihomeBackend.Data;

public static class DbSeeder
{
    public static void Seed(AppDbContext db)
    {
        var now = DateTime.UtcNow;
        var otpEmailBodyTemplate = @"<div style='margin:0;padding:0;background:#f3f6fb;font-family:Segoe UI,Arial,sans-serif;color:#1f2937;'>
    <table role='presentation' width='100%' cellspacing='0' cellpadding='0' style='width:100%;max-width:100%;margin:0;background:#ffffff;border:1px solid #e5e7eb;border-radius:0;overflow:hidden;'>
        <tr>
            <td style='padding:14px 18px;background:linear-gradient(135deg,#0f172a,#7c3aed);color:#fff;'>
                <div style='font-size:18px;font-weight:700;'>{{siteName}}</div>
                <div style='font-size:12px;opacity:.9;margin-top:4px;'>MÃ XÁC THỰC OTP</div>
            </td>
        </tr>
        <tr>
            <td style='padding:16px 18px;'>
                <p style='margin:0 0 12px;'>Xin chào,</p>
                <p style='margin:0 0 8px;'>Mã OTP của bạn là:</p>
                <div style='margin:12px 0 16px;padding:14px 18px;background:#f8fafc;border:1px dashed #cbd5e1;border-radius:10px;font-size:30px;letter-spacing:6px;font-weight:700;text-align:center;color:#111827;'>{{otpCode}}</div>
                <p style='margin:0 0 12px;'>Mã có hiệu lực trong <strong>{{otpExpireMinutes}} phút</strong>.</p>
                <p style='margin:0;color:#b91c1c;'>Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email.</p>
            </td>
        </tr>
        <tr>
            <td style='padding:10px 18px;background:#f8fafc;border-top:1px solid #e5e7eb;font-size:11px;color:#6b7280;'>
                © {{siteName}}. Bảo mật thông tin là ưu tiên hàng đầu của chúng tôi.
            </td>
        </tr>
    </table>
</div>";

        if (!db.Users.Any(u => u.Role == UserRole.SUPER_ADMIN))
        {
            var superAdmin = new ApplicationUser
            {
                PhoneNumber = "84335240370",
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
                    PhoneNumber = "84911111111",
                    FullName = "Lê Thảo Vy",
                    Email = "ops.admin@nihome.vn",
                    Role = UserRole.ADMIN,
                    IsActive = true
                },
                new ApplicationUser
                {
                    PhoneNumber = "84922222222",
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
                Address = "123 Đường Nguyễn Huệ, Quận 1, TP. Hồ Chí Minh, Việt Nam",
                EnableOtpForRegistration = true,
                EnableOtpForForgotPassword = true,
                OtpEmailSubjectTemplate = "[{{siteName}}] Mã OTP xác thực của bạn",
                OtpEmailBodyTemplate = otpEmailBodyTemplate,
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
                existingSettings.OtpEmailSubjectTemplate = "[{{siteName}}] Mã OTP xác thực của bạn";
                updated = true;
            }

            if (string.IsNullOrWhiteSpace(existingSettings.OtpEmailBodyTemplate))
            {
                existingSettings.OtpEmailBodyTemplate = otpEmailBodyTemplate;
                updated = true;
            }

            if (updated)
            {
                existingSettings.UpdatedAt = now;
                db.SaveChanges();
            }
        }
    }
}
