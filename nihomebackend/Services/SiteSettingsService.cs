using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;

namespace NihomeBackend.Services;

public class SiteSettingsService(AppDbContext db)
{
    public async Task<List<ProjectScheduleEmailReviewItem>> GetScheduleEmailsNeedingReviewAsync(CancellationToken ct) =>
        await db.ProjectScheduleEmails.AsNoTracking()
            .Where(email => email.Status == ProjectScheduleEmail.NeedsReviewStatus)
            .OrderByDescending(email => email.LastAttemptAt)
            .Take(100)
            .Select(email => new ProjectScheduleEmailReviewItem(
                email.Id, email.OperationalProjectId, email.RevisionId,
                email.CmScheduleBaselineId.HasValue ? email.CmScheduleBaseline!.Version : email.Revision.Version, email.Revision.OperationalProject.Code,
                email.AttemptCount, email.LastAttemptAt, null, null, null))
            .ToListAsync(ct);

    public async Task<SiteSettings?> GetAsync()
    {
        return await db.SiteSettings.AsNoTracking().FirstOrDefaultAsync();
    }

    public async Task<(string TimeZoneId, string Currency)> GetDisplayAsync(CancellationToken ct = default)
    {
        var settings = await db.SiteSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        return (
            DisplaySettings.CanonicalTimeZoneId(settings?.DisplayTimeZoneId),
            DisplaySettings.NormalizeCurrencyOrDefault(settings?.CurrencyCode));
    }

    public async Task<(string TimeZoneId, string Currency)> UpdateDisplayAsync(
        string timeZoneId,
        string currency,
        CancellationToken ct = default)
    {
        var settings = await db.SiteSettings.FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("SiteSettings chưa được khởi tạo.");
        settings.DisplayTimeZoneId = DisplaySettings.CanonicalTimeZoneId(timeZoneId);
        settings.CurrencyCode = DisplaySettings.NormalizeCurrency(currency);
        settings.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return (settings.DisplayTimeZoneId, settings.CurrencyCode);
    }

    public async Task<decimal> UpdateCmScheduleThresholdAsync(decimal threshold, CancellationToken ct)
    {
        var settings = await db.SiteSettings.FirstAsync(ct);
        settings.CmScheduleDelayThresholdPercent = threshold;
        settings.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return threshold;
    }

    public async Task<SiteSettings> UpdateContractUploadLimitAsync(int maxFileSizeMb, CancellationToken ct)
    {
        var settings = await db.SiteSettings.FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("SiteSettings chưa được khởi tạo.");
        settings.ContractMaxFileSizeMb = maxFileSizeMb;
        settings.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return settings;
    }

    public async Task<SiteSettings> UpdateEmailTemplatesAsync(
        string? newApplicationSubject,
        string? newApplicationBody,
        string? notificationEmail,
        string? otpEmailSubject = null,
        string? otpEmailBody = null,
        string? quoteEmailSubject = null,
        string? quoteEmailBody = null,
        string? projectScheduleEmailSubject = null,
        string? projectScheduleEmailBody = null)
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
            if (!EmailTemplateFormatter.IsSafeQuoteHtml(quoteBody))
                throw new ArgumentException("Nội dung mẫu email báo giá chỉ hỗ trợ HTML cơ bản, CSS nội tuyến an toàn; không cho phép script, liên kết hoặc thuộc tính sự kiện.");
            settings.QuoteEmailBodyTemplate = quoteBody;
        }
        if (projectScheduleEmailSubject is not null || projectScheduleEmailBody is not null)
        {
            var scheduleTemplate = ProjectScheduleEmailTemplates.Validate(
                projectScheduleEmailSubject ?? ProjectScheduleEmailTemplates.Get(settings).Subject,
                projectScheduleEmailBody ?? ProjectScheduleEmailTemplates.Get(settings).Body);
            settings.ProjectScheduleEmailSubjectTemplate = scheduleTemplate.Subject;
            settings.ProjectScheduleEmailBodyTemplate = scheduleTemplate.Body;
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

public sealed record ProjectScheduleEmailReviewItem(int Id, int ProjectId, int RevisionId,
    int Version, string ProjectCode, int AttemptCount, DateTime? LastAttemptAt,
    string? ToEmail = null, string? Subject = null, string? Body = null);
