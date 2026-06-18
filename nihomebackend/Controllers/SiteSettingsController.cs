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
            settings.NewApplicationEmailBodyTemplate,
            settings.NotificationEmail,
            settings.OtpEmailSubjectTemplate,
            settings.OtpEmailBodyTemplate,
        });
    }

    /// <summary>Update email templates and notification email.</summary>
    [HttpPut("email-templates")]
    [Authorize]
    [RequirePermission("system.settings", "manage")]
    public async Task<IActionResult> UpdateEmailTemplates([FromBody] UpdateEmailTemplatesRequest req)
    {
        var settings = await svc.UpdateEmailTemplatesAsync(
            req.NewApplicationEmailSubjectTemplate,
            req.NewApplicationEmailBodyTemplate,
            req.NotificationEmail,
            req.OtpEmailSubjectTemplate,
            req.OtpEmailBodyTemplate);

        return Ok(new
        {
            settings.NewApplicationEmailSubjectTemplate,
            settings.NewApplicationEmailBodyTemplate,
            settings.NotificationEmail,
            settings.OtpEmailSubjectTemplate,
            settings.OtpEmailBodyTemplate,
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
