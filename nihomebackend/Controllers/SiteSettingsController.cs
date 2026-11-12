using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

[ApiController]
[Route("api/site-settings")]
public class SiteSettingsController(SiteSettingsService svc) : ControllerBase
{
    [HttpGet("cm-schedule"), Authorize, RequirePermission("system.settings", "view")]
    public async Task<IActionResult> CmSchedulePolicy() => Ok(new { thresholdPercent = (await svc.GetAsync())?.CmScheduleDelayThresholdPercent });
    [HttpPut("cm-schedule"), Authorize, RequirePermission("system.settings", "manage")]
    public async Task<IActionResult> UpdateCmSchedulePolicy([FromBody] CmScheduleThresholdRequest request, CancellationToken ct)
    {
        if (request.ThresholdPercent != decimal.Round(request.ThresholdPercent!.Value, 4))
            return BadRequest(new { code = "CM_SCHEDULE_thresholdInvalid", field = "thresholdPercent", message = "Ngưỡng chậm phải từ 0 đến 100, tối đa 4 chữ số thập phân. Ví dụ: 5 hoặc 5,5." });
        return Ok(new { thresholdPercent = await svc.UpdateCmScheduleThresholdAsync(request.ThresholdPercent!.Value, ct) });
    }
    public sealed class CmScheduleThresholdRequest
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.Range(typeof(decimal), "0", "100")]
        public decimal? ThresholdPercent { get; set; }
    }

    [HttpGet("contract-uploads")]
    [Authorize]
    [RequirePermission("system.settings", "view")]
    public async Task<IActionResult> GetContractUploadLimit()
    {
        var settings = await svc.GetAsync();
        return Ok(new { maxFileSizeMb = settings?.ContractMaxFileSizeMb ?? SiteSettings.DefaultContractMaxFileSizeMb, maxAllowedFileSizeMb = ProjectDocumentStorageService.ContractMaxFileSizeMb });
    }

    [HttpPut("contract-uploads")]
    [Authorize]
    [RequirePermission("system.settings", "manage")]
    public async Task<IActionResult> UpdateContractUploadLimit([FromBody] UpdateContractUploadLimitRequest req, CancellationToken ct)
    {
        if (req.MaxFileSizeMb < 1 || req.MaxFileSizeMb > ProjectDocumentStorageService.ContractMaxFileSizeMb)
            return BadRequest(new { code = "CONTRACT_UPLOAD_LIMIT_INVALID", field = "maxFileSizeMb", maxAllowedFileSizeMb = ProjectDocumentStorageService.ContractMaxFileSizeMb, message = $"Giới hạn tệp hợp đồng phải từ 1 đến {ProjectDocumentStorageService.ContractMaxFileSizeMb} MB. Ví dụ: 100 MB." });
        var settings = await svc.UpdateContractUploadLimitAsync(req.MaxFileSizeMb, ct);
        return Ok(new { maxFileSizeMb = settings.ContractMaxFileSizeMb, maxAllowedFileSizeMb = ProjectDocumentStorageService.ContractMaxFileSizeMb });
    }

    /// <summary>Get OTP verification settings.</summary>
    [HttpGet("otp-settings")]
    [AllowAnonymous]
    public async Task<ActionResult<OtpSettingsResponse>> GetOtpSettings()
    {
        var settings = await svc.GetAsync();
        if (settings == null)
        {
            // When settings are not initialized, return default OTP settings (feature enabled by default).
            return Ok(new OtpSettingsResponse
            {
                EnableOtpForRegistration = true,
                EnableOtpForForgotPassword = true
            });
        }

        return Ok(ToOtpSettingsResponse(settings));
    }

    /// <summary>Update OTP verification settings.</summary>
    [HttpPut("otp-settings")]
    [Authorize]
    [RequirePermission("system.settings", "manage")]
    public async Task<ActionResult<OtpSettingsResponse>> UpdateOtpSettings([FromBody] UpdateOtpSettingsRequest req)
    {
        var settings = await svc.UpdateOtpSettingsAsync(
            req.EnableOtpForRegistration,
            req.EnableOtpForForgotPassword);

        return Ok(ToOtpSettingsResponse(settings));
    }

    /// <summary>Get email templates and notification email.</summary>
    [HttpGet("email-templates")]
    [Authorize]
    [RequirePermission("system.settings", "view")]
    public async Task<IActionResult> GetEmailTemplates()
    {
        var settings = await svc.GetAsync();
        if (settings == null) return NotFound();

        return Ok(new
        {
            settings.NewApplicationEmailSubjectTemplate,
            NewApplicationEmailBodyTemplate = EmailTemplateBaseline.Apply(
                settings.NewApplicationEmailBodyTemplate ?? EmailTemplateFormatter.DefaultNewApplicationBody,
                "ĐƠN ỨNG TUYỂN MỚI", "Email tự động · vui lòng không trả lời"),
            settings.NotificationEmail,
            settings.OtpEmailSubjectTemplate,
            OtpEmailBodyTemplate = EmailTemplateBaseline.Apply(
                settings.OtpEmailBodyTemplate ?? EmailTemplateFormatter.DefaultOtpBody,
                "XÁC THỰC TÀI KHOẢN", "Email tự động · vui lòng không trả lời"),
            QuoteEmailSubjectTemplate = settings.QuoteEmailSubjectTemplate ?? EmailTemplateFormatter.DefaultQuoteSubject,
            QuoteEmailBodyTemplate = EmailTemplateBaseline.Apply(
                settings.QuoteEmailBodyTemplate ?? EmailTemplateFormatter.DefaultQuoteBody,
                "BÁO GIÁ DỰ ÁN · {{quoteCode}}", "NICON · Đối tác phát triển của Quý khách"),
            ProjectScheduleEmailSubjectTemplate = ProjectScheduleEmailTemplates.Get(settings).Subject,
            ProjectScheduleEmailBodyTemplate = ProjectScheduleEmailTemplates.Get(settings).Body,
        });
    }

    /// <summary>List schedule emails that exhausted retries for an administrator's manual review.</summary>
    [HttpGet("email-templates/project-schedule/needs-review")]
    [Authorize]
    [RequirePermission("system.settings", "view")]
    public async Task<ActionResult<List<ProjectScheduleEmailReviewItem>>> GetProjectScheduleEmailsNeedingReview(CancellationToken ct) =>
        Ok(await svc.GetScheduleEmailsNeedingReviewAsync(ct));

    /// <summary>Preview an unsaved project schedule template using the production renderer and sample data.</summary>
    [HttpPost("email-templates/project-schedule/preview")]
    [Authorize]
    [RequirePermission("system.settings", "manage")]
    public IActionResult PreviewProjectScheduleEmail([FromBody] PreviewProjectScheduleEmailRequest req)
    {
        try
        {
            var template = ProjectScheduleEmailTemplates.Validate(req.Subject, req.Body);
            return Ok(ProjectScheduleEmailTemplates.Preview(template with
            {
                Body = EmailTemplateBaseline.Apply(template.Body,
                    "TIẾN ĐỘ TỔNG THỂ · {{eventName}}", "NICON · Thông báo tiến độ dự án")
            }));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Update email templates and notification email.</summary>
    [HttpPut("email-templates")]
    [Authorize]
    [RequirePermission("system.settings", "manage")]
    public async Task<IActionResult> UpdateEmailTemplates([FromBody] UpdateEmailTemplatesRequest req)
    {
        SiteSettings settings;
        try
        {
            settings = await svc.UpdateEmailTemplatesAsync(
                req.NewApplicationEmailSubjectTemplate,
                req.NewApplicationEmailBodyTemplate,
                req.NotificationEmail,
                req.OtpEmailSubjectTemplate,
                req.OtpEmailBodyTemplate,
                req.QuoteEmailSubjectTemplate,
                req.QuoteEmailBodyTemplate,
                req.ProjectScheduleEmailSubjectTemplate,
                req.ProjectScheduleEmailBodyTemplate);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        return Ok(new
        {
            settings.NewApplicationEmailSubjectTemplate,
            NewApplicationEmailBodyTemplate = EmailTemplateBaseline.Apply(
                settings.NewApplicationEmailBodyTemplate ?? EmailTemplateFormatter.DefaultNewApplicationBody,
                "ĐƠN ỨNG TUYỂN MỚI", "Email tự động · vui lòng không trả lời"),
            settings.NotificationEmail,
            settings.OtpEmailSubjectTemplate,
            OtpEmailBodyTemplate = EmailTemplateBaseline.Apply(
                settings.OtpEmailBodyTemplate ?? EmailTemplateFormatter.DefaultOtpBody,
                "XÁC THỰC TÀI KHOẢN", "Email tự động · vui lòng không trả lời"),
            settings.QuoteEmailSubjectTemplate,
            QuoteEmailBodyTemplate = EmailTemplateBaseline.Apply(
                settings.QuoteEmailBodyTemplate ?? EmailTemplateFormatter.DefaultQuoteBody,
                "BÁO GIÁ DỰ ÁN · {{quoteCode}}", "NICON · Đối tác phát triển của Quý khách"),
            ProjectScheduleEmailSubjectTemplate = ProjectScheduleEmailTemplates.Get(settings).Subject,
            ProjectScheduleEmailBodyTemplate = ProjectScheduleEmailTemplates.Get(settings).Body,
        });
    }

    private static OtpSettingsResponse ToOtpSettingsResponse(SiteSettings settings) => new()
    {
        EnableOtpForRegistration = settings.EnableOtpForRegistration,
        EnableOtpForForgotPassword = settings.EnableOtpForForgotPassword
    };

    /// <summary>
    /// Public display settings used by every client so dates, times and money
    /// values follow one configured time zone and currency.
    /// </summary>
    [HttpGet("display")]
    [AllowAnonymous]
    public async Task<IActionResult> GetDisplaySettings(CancellationToken ct)
    {
        var settings = await svc.GetDisplayAsync(ct);
        return Ok(new
        {
            timeZoneId = settings.TimeZoneId,
            currency = settings.Currency,
            usdVndRate = settings.UsdVndRate,
        });
    }

    /// <summary>Available time zones for the admin display settings screen.</summary>
    [HttpGet("time-zones")]
    [Authorize]
    [RequirePermission("system.settings", "view")]
    public IActionResult GetTimeZones() => Ok(DisplaySettings.ListTimeZones());

    /// <summary>Update the system-wide display time zone and currency.</summary>
    [HttpPut("display")]
    [Authorize]
    [RequirePermission("system.settings", "manage")]
    public async Task<IActionResult> UpdateDisplaySettings(
        [FromBody] UpdateDisplaySettingsRequest req,
        CancellationToken ct)
    {
        if (!DisplaySettings.TryResolveTimeZone(req.TimeZoneId, out _))
            return BadRequest(new
            {
                code = "DISPLAY_TIMEZONE_INVALID",
                field = "timeZoneId",
                message = "Múi giờ không hợp lệ. Chọn một múi giờ trong danh sách hệ thống.",
            });
        if (!DisplaySettings.IsSupportedCurrency(req.Currency))
            return BadRequest(new
            {
                code = "DISPLAY_CURRENCY_INVALID",
                field = "currency",
                message = "Tiền tệ chỉ hỗ trợ VND hoặc USD.",
            });
        var currentRate = (await svc.GetDisplayAsync(ct)).UsdVndRate;
        var rate = req.UsdVndRate ?? currentRate;
        if (rate <= 0 || rate > 1_000_000_000m || decimal.Round(rate, 4) != rate)
            return BadRequest(new
            {
                code = "DISPLAY_RATE_INVALID",
                field = "usdVndRate",
                message = "Tỷ giá phải lớn hơn 0 và tối đa 4 chữ số thập phân. Ví dụ: 25000.",
            });

        var settings = await svc.UpdateDisplayAsync(req.TimeZoneId!, req.Currency!, rate, ct);
        return Ok(new
        {
            timeZoneId = settings.TimeZoneId,
            currency = settings.Currency,
            usdVndRate = settings.UsdVndRate,
        });
    }

    /// <summary>Get the embedded Google Map URL used on the public Contact page.</summary>
    [HttpGet("map-embed")]
    [AllowAnonymous]
    public async Task<IActionResult> GetMapEmbed()
    {
        var settings = await svc.GetAsync();
        return Ok(new { mapEmbedUrl = settings?.MapEmbedUrl });
    }

    /// <summary>Update the embedded Google Map URL.</summary>
    [HttpPut("map-embed")]
    [Authorize]
    [RequirePermission("system.settings", "manage")]
    public async Task<IActionResult> UpdateMapEmbed([FromBody] UpdateMapEmbedRequest req)
    {
        var settings = await svc.UpdateMapEmbedAsync(req.MapEmbedUrl);
        return Ok(new { mapEmbedUrl = settings.MapEmbedUrl });
    }
}

public sealed record UpdateContractUploadLimitRequest(int MaxFileSizeMb);

public sealed record UpdateDisplaySettingsRequest(string? TimeZoneId, string? Currency, decimal? UsdVndRate);
