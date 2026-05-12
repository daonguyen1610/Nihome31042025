using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        var settings = await svc.GetAsync() ?? new SiteSettings();
        return Ok(ToOtpSettingsResponse(settings));
    }

    /// <summary>Update OTP verification settings.</summary>
    [HttpPut("otp-settings")]
    [Authorize(Roles = "SUPER_ADMIN,ADMIN")]
    public async Task<ActionResult<OtpSettingsResponse>> UpdateOtpSettings([FromBody] UpdateOtpSettingsRequest req)
    {
        try
        {
            var settings = await svc.UpdateOtpSettingsAsync(
                req.EnableOtpForRegistration,
                req.EnableOtpForForgotPassword);

            return Ok(ToOtpSettingsResponse(settings));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Get email templates and notification email.</summary>
    [HttpGet("email-templates")]
    [Authorize(Roles = "SUPER_ADMIN,ADMIN")]
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
    [Authorize(Roles = "SUPER_ADMIN,ADMIN")]
    public async Task<IActionResult> UpdateEmailTemplates([FromBody] UpdateEmailTemplatesRequest req)
    {
        try
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
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private static OtpSettingsResponse ToOtpSettingsResponse(SiteSettings settings) => new()
    {
        EnableOtpForRegistration = settings.EnableOtpForRegistration,
        EnableOtpForForgotPassword = settings.EnableOtpForForgotPassword
    };
}
