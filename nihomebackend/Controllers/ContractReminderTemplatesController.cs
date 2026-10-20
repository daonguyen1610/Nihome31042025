using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Authorization;
using NihomeBackend.Data;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

[ApiController, Authorize, Route("api/site-settings/contract-reminder-templates")]
public sealed class ContractReminderTemplatesController(AppDbContext db) : ControllerBase
{
    [HttpGet, RequirePermission("system.settings", "view")]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        Ok(ContractReminderTemplates.Get(await db.SiteSettings.AsNoTracking().FirstOrDefaultAsync(ct)));

    [HttpPut, RequirePermission("system.settings", "manage")]
    public async Task<IActionResult> Put(ContractReminderTemplatesRequest request, CancellationToken ct)
    {
        string renewalSubject, renewalBody, endSubject, endBody;
        try
        {
            renewalSubject = ContractReminderTemplates.Validate(request.RenewalSubject, false);
            renewalBody = ContractReminderTemplates.Validate(request.RenewalBody, true);
            endSubject = ContractReminderTemplates.Validate(request.EndSubject, false);
            endBody = ContractReminderTemplates.Validate(request.EndBody, true);
        }
        catch (ArgumentException error) { return BadRequest(new { message = error.Message }); }
        var settings = await db.SiteSettings.FirstOrDefaultAsync(ct);
        if (settings is null) return NotFound();
        settings.ContractRenewalEmailSubjectTemplate = renewalSubject;
        settings.ContractRenewalEmailBodyTemplate = renewalBody;
        settings.ContractEndEmailSubjectTemplate = endSubject;
        settings.ContractEndEmailBodyTemplate = endBody;
        settings.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(ContractReminderTemplates.Get(settings));
    }
}
