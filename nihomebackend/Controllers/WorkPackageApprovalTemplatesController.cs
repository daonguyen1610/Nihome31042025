using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Authorization;
using NihomeBackend.Data;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

public sealed class WorkPackageApprovalTemplateRequest
{
    [Required, StringLength(200)] public string Subject { get; set; } = "";
    [Required, StringLength(8000)] public string Body { get; set; } = "";
}

[ApiController, Authorize, Route("api/site-settings/work-package-approval-template")]
public sealed class WorkPackageApprovalTemplatesController(AppDbContext db) : ControllerBase
{
    [HttpGet, RequirePermission("system.settings", "view")]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        Ok(WorkPackageApprovalEmailTemplates.Get(await db.SiteSettings.AsNoTracking().FirstOrDefaultAsync(ct)));

    [HttpPut, RequirePermission("system.settings", "manage")]
    public async Task<IActionResult> Put(WorkPackageApprovalTemplateRequest request, CancellationToken ct)
    {
        string subject, body;
        try
        {
            subject = WorkPackageApprovalEmailTemplates.Validate(request.Subject, false);
            body = WorkPackageApprovalEmailTemplates.Validate(request.Body, true);
        }
        catch (ArgumentException error) { return BadRequest(new { message = error.Message }); }
        var settings = await db.SiteSettings.FirstOrDefaultAsync(ct);
        if (settings is null) return NotFound();
        settings.WorkPackageApprovalEmailSubjectTemplate = subject;
        settings.WorkPackageApprovalEmailBodyTemplate = body;
        settings.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(WorkPackageApprovalEmailTemplates.Get(settings));
    }
}
