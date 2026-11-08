using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Authorization;
using NihomeBackend.Data;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

[ApiController, Authorize, Route("api/site-settings/cm-schedule/email-template")]
public sealed class CmScheduleEmailTemplatesController(AppDbContext db) : ControllerBase
{
    [HttpGet, RequirePermission("system.settings", "view")]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        Ok(CmScheduleMailService.Template(await db.SiteSettings.AsNoTracking().FirstOrDefaultAsync(ct)));
    [HttpPut, RequirePermission("system.settings", "manage")]
    public async Task<IActionResult> Put(WorkPackageApprovalTemplateRequest request, CancellationToken ct)
    {
        ProjectScheduleEmailTemplate template;
        try { template = ProjectScheduleEmailTemplates.Validate(request.Subject, request.Body); }
        catch (ArgumentException error) { return BadRequest(new { code = "CM_SCHEDULE_emailTemplateInvalid", message = error.Message }); }
        var settings = await db.SiteSettings.FirstOrDefaultAsync(ct);
        if (settings is null) return NotFound();
        settings.CmScheduleEmailSubjectTemplate = template.Subject;
        settings.CmScheduleEmailBodyTemplate = template.Body;
        settings.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(template);
    }
    [HttpPost("preview"), RequirePermission("system.settings", "manage")]
    public IActionResult Preview(WorkPackageApprovalTemplateRequest request)
    {
        try { return Ok(ProjectScheduleEmailTemplates.Preview(ProjectScheduleEmailTemplates.Validate(request.Subject, request.Body))); }
        catch (ArgumentException error) { return BadRequest(new { code = "CM_SCHEDULE_emailTemplateInvalid", message = error.Message }); }
    }
}
