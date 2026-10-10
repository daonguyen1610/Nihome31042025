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
        string? otpEmailBody = null,
        string? quoteEmailSubject = null,
        string? quoteEmailBody = null)
    {
        var settings = await db.SiteSettings.FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("SiteSettings chưa được khởi tạo.");

        settings.NewApplicationEmailSubjectTemplate = newApplicationSubject?.Trim();
        settings.NewApplicationEmailBodyTemplate = newApplicationBody?.Trim();
        settings.NotificationEmail = notificationEmail?.Trim();
        settings.OtpEmailSubjectTemplate = otpEmailSubject?.Trim();
        settings.OtpEmailBodyTemplate = otpEmailBody?.Trim();
        if (quoteEmailSubject is not null)
            settings.QuoteEmailSubjectTemplate = ValidateQuoteTemplate(quoteEmailSubject, 200, "Tiêu đề mẫu email báo giá");
        if (quoteEmailBody is not null)
        {
            var quoteBody = ValidateQuoteTemplate(quoteEmailBody, 8000, "Nội dung mẫu email báo giá");
            if (!quoteBody.Contains("{{quoteLines}}", StringComparison.OrdinalIgnoreCase) ||
                !quoteBody.Contains("{{grandTotal}}", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Nội dung mẫu email báo giá phải có {{quoteLines}} và {{grandTotal}} để khách thấy hạng mục và tổng giá trị.");
            settings.QuoteEmailBodyTemplate = quoteBody;
        }
        settings.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return settings;
    }

    private static string ValidateQuoteTemplate(string value, int maxLength, string field)
    {
        var trimmed = value.Trim();
        if (trimmed.Length is 0 || trimmed.Length > maxLength)
            throw new ArgumentException($"{field} phải có từ 1 đến {maxLength} ký tự.");
        if (EmailTemplateFormatter.HasInvalidQuoteTokens(trimmed))
            throw new ArgumentException($"{field} chứa biến không được hỗ trợ hoặc sai cú pháp. Dùng các biến có sẵn trong màn hình Mẫu email.");
        return trimmed;
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
