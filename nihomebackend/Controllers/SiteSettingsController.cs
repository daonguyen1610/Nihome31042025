using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

[ApiController]
[Route("api/site-settings")]
public class SiteSettingsController(SiteSettingsService svc) : ControllerBase
{
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
}
