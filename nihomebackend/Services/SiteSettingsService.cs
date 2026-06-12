using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;

namespace NihomeBackend.Services;

public class SiteSettingsService(AppDbContext db)
{
    public async Task<SiteSettings?> GetAsync()
    {
        return await db.SiteSettings.AsNoTracking().FirstOrDefaultAsync();
    }

    public async Task<SiteSettings> UpdateEmailTemplatesAsync(
        string? newApplicationSubject,
        string? newApplicationBody,
        string? notificationEmail,
        string? otpEmailSubject = null,
        string? otpEmailBody = null)
    {
        var settings = await db.SiteSettings.FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("SiteSettings chưa được khởi tạo.");

        settings.NewApplicationEmailSubjectTemplate = newApplicationSubject?.Trim();
        settings.NewApplicationEmailBodyTemplate = newApplicationBody?.Trim();
        settings.NotificationEmail = notificationEmail?.Trim();
        settings.OtpEmailSubjectTemplate = otpEmailSubject?.Trim();
        settings.OtpEmailBodyTemplate = otpEmailBody?.Trim();
        settings.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return settings;
    }

    public async Task<SiteSettings> UpdateOtpSettingsAsync(
        bool enableOtpForRegistration,
        bool enableOtpForForgotPassword)
    {
        var settings = await db.SiteSettings.FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("SiteSettings chưa được khởi tạo.");

        settings.EnableOtpForRegistration = enableOtpForRegistration;
        settings.EnableOtpForForgotPassword = enableOtpForForgotPassword;
        settings.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return settings;
    }

    public async Task<SiteSettings> UpdateMapEmbedAsync(string? mapEmbedUrl)
    {
        var settings = await db.SiteSettings.FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("SiteSettings chưa được khởi tạo.");

        var trimmed = mapEmbedUrl?.Trim();
        if (!string.IsNullOrEmpty(trimmed) && trimmed.Length > 1000)
        {
            throw new InvalidOperationException("MapEmbedUrl không được vượt quá 1000 ký tự.");
        }

        settings.MapEmbedUrl = string.IsNullOrEmpty(trimmed) ? null : trimmed;
        settings.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return settings;
    }
}
