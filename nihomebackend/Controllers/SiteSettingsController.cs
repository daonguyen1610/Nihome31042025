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
