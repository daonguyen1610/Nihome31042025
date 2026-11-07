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
    [StringLength(200)] public string? Subject { get; set; }
    [StringLength(8000)] public string? Body { get; set; }
    [StringLength(200)] public string? DecisionSubject { get; set; }
    [StringLength(8000)] public string? DecisionBody { get; set; }
}

[ApiController, Authorize, Route("api/site-settings/quote-approval-template")]
public sealed class QuoteApprovalTemplatesController(AppDbContext db) : ControllerBase
{
    [HttpGet, RequirePermission("system.settings", "view")]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var settings = await db.SiteSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        return Ok(BuildResponse(settings));
    }

    [HttpPut, RequirePermission("system.settings", "manage")]
    public async Task<IActionResult> Put(QuoteApprovalTemplateRequest request, CancellationToken ct)
    {
        string? subject = null, body = null, decisionSubject = null, decisionBody = null;
        try
        {
            if (request.Subject is not null) subject = QuoteApprovalEmailTemplates.Validate(request.Subject, false);
            if (request.Body is not null) body = QuoteApprovalEmailTemplates.Validate(request.Body, true);
            if (request.DecisionSubject is not null)
                decisionSubject = QuoteApprovalDecisionEmailTemplates.Validate(request.DecisionSubject, false);
            if (request.DecisionBody is not null)
                decisionBody = QuoteApprovalDecisionEmailTemplates.Validate(request.DecisionBody, true);
        }
        catch (ArgumentException error) { return BadRequest(new { message = error.Message }); }
        if (subject is null && body is null && decisionSubject is null && decisionBody is null)
            return BadRequest(new { message = "Gửi ít nhất một mẫu email cần cập nhật." });
        if ((subject is null) != (body is null) || (decisionSubject is null) != (decisionBody is null))
            return BadRequest(new { message = "Mỗi mẫu email phải gửi kèm cả tiêu đề và nội dung." });
        var settings = await db.SiteSettings.FirstOrDefaultAsync(ct);
        if (settings is null) return NotFound();
        if (subject is not null) settings.QuoteApprovalEmailSubjectTemplate = subject;
        if (body is not null) settings.QuoteApprovalEmailBodyTemplate = body;
        if (decisionSubject is not null) settings.QuoteApprovalDecisionEmailSubjectTemplate = decisionSubject;
        if (decisionBody is not null) settings.QuoteApprovalDecisionEmailBodyTemplate = decisionBody;
        settings.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(BuildResponse(settings));
    }

    private static object BuildResponse(NihomeBackend.Models.SiteSettings? settings)
    {
        var request = QuoteApprovalEmailTemplates.Get(settings);
        var decision = QuoteApprovalDecisionEmailTemplates.Get(settings);
        return new
        {
            subject = request.Subject,
            body = request.Body,
            decisionSubject = decision.Subject,
            decisionBody = decision.Body,
        };
    }
}
