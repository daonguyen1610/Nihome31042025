using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Authorization;
using NihomeBackend.Data;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

public sealed class QuoteApprovalTemplateRequest
{
    [Required, StringLength(200)] public string Subject { get; set; } = "";
    [Required, StringLength(8000)] public string Body { get; set; } = "";
}

[ApiController, Authorize, Route("api/site-settings/quote-approval-template")]
public sealed class QuoteApprovalTemplatesController(AppDbContext db) : ControllerBase
{
    [HttpGet, RequirePermission("system.settings", "view")]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        Ok(QuoteApprovalEmailTemplates.Get(await db.SiteSettings.AsNoTracking().FirstOrDefaultAsync(ct)));

    [HttpPut, RequirePermission("system.settings", "manage")]
    public async Task<IActionResult> Put(QuoteApprovalTemplateRequest request, CancellationToken ct)
    {
        string subject, body;
        try
        {
            subject = QuoteApprovalEmailTemplates.Validate(request.Subject, false);
            body = QuoteApprovalEmailTemplates.Validate(request.Body, true);
        }
        catch (ArgumentException error) { return BadRequest(new { message = error.Message }); }
        var settings = await db.SiteSettings.FirstOrDefaultAsync(ct);
        if (settings is null) return NotFound();
        settings.QuoteApprovalEmailSubjectTemplate = subject;
        settings.QuoteApprovalEmailBodyTemplate = body;
        settings.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(QuoteApprovalEmailTemplates.Get(settings));
    }
}
