using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;
using NihomeBackend.Services.Audit;

namespace NihomeBackend.Controllers;

[ApiController]
[Route("api/reports/projects")]
[Route("api/v1/reports/projects")]
[Authorize]
public sealed class ProjectReportsController(
    IProjectReportService reports,
    IAuditLogger audit) : ControllerBase
{
    [HttpGet]
    [RequirePermission("reports.projects", "view")]
    public async Task<ActionResult<ProjectReportResponse>> Get(
        [FromQuery] ProjectReportQuery query,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return Unauthorized();
        var report = await reports.GetAsync(query, userId.Value, ct);
        return report is null ? NotFound() : Ok(report);
    }

    [HttpGet("export")]
    [RequirePermission("reports.projects", "export")]
    public async Task<IActionResult> Export(
        [FromQuery] ProjectReportExportQuery query,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return Unauthorized();
        var export = await reports.ExportAsync(query, userId.Value, ct);
        if (export is null) return NotFound();

        audit.Log(new AuditEvent
        {
            Action = "project-report.export",
            ResourceType = "ProjectReport",
            ResourceId = query.ProjectId?.ToString(),
            Message = $"Exported project operational report as {query.Format.ToLowerInvariant()}.",
            Metadata = new
            {
                query.ProjectId,
                query.From,
                query.To,
                Format = query.Format.ToLowerInvariant(),
                query.Language,
                export.ProjectCount,
            },
        });
        return File(export.Content, export.ContentType, export.FileName);
    }

    private int? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var userId) ? userId : null;
    }
}
