using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Constants;
using NihomeBackend.Services;
using NihomeBackend.Services.Audit;

namespace NihomeBackend.Controllers;

[ApiController]
[Authorize]
[Route("api/operational-projects/{projectId:int}/procurement")]
[Route("api/v1/operational-projects/{projectId:int}/procurement")]
public sealed class ProjectBoqWorkbooksController(
    ProjectBoqWorkbookService workbooks,
    IProjectAccessService access,
    IAuditLogger audit,
    ILogger<ProjectBoqWorkbooksController> logger) : ControllerBase
{
    private const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [HttpGet("boq-templates")]
    [RequirePermission("proc.boq", "view")]
    public Task<IActionResult> Templates(int projectId, CancellationToken ct) => Run("templates", projectId, _ =>
        Task.FromResult<IActionResult>(Ok(ProjectBoqTemplateCatalog.All.Select(template => new { template.Code, template.NameKey, template.File }))), ct);

    [HttpGet("boq-templates/{code}/file")]
    [RequirePermission("proc.boq", "view")]
    public Task<IActionResult> Template(int projectId, string code, CancellationToken ct) => Run("template", projectId, _ =>
    {
        var template = ProjectBoqTemplateCatalog.Find(code);
        return Task.FromResult<IActionResult>(template == null
            ? NotFound()
            : File(ProjectBoqTemplateCatalog.ReadFile(template), XlsxType, template.File));
    }, ct);

    [HttpPost("boq-workbooks/preview"), Consumes("multipart/form-data")]
    [RequestSizeLimit(ProjectBoqWorkbookParser.MaxFileBytes + 1024 * 1024)]
    [RequirePermission("proc.boq", "manage")]
    public Task<IActionResult> Preview(int projectId, [FromForm] string? templateCode, IFormFile? file, CancellationToken ct) =>
        Run("preview", projectId, async _ => Ok(await workbooks.PreviewAsync(projectId, templateCode, file, ct)), ct);

    [HttpPost("boq-workbooks"), Consumes("multipart/form-data")]
    [RequestSizeLimit(ProjectBoqWorkbookParser.MaxFileBytes + 1024 * 1024)]
    [RequirePermission("proc.boq", "manage")]
    public Task<IActionResult> Import(int projectId, [FromForm] string? templateCode, IFormFile? file, [FromForm] string? sourceHash,
        [FromForm] int? upstreamContractId, CancellationToken ct) => Run("import", projectId, async userId =>
    {
        var result = await workbooks.ImportAsync(projectId, templateCode, file, sourceHash, upstreamContractId, userId, ct);
        audit.Log(new AuditEvent
        {
            Action = "proc.boq.workbook.import",
            ResourceType = EntityTypes.ProjectBoqRevision,
            ResourceId = result.Id.ToString(),
            Message = $"BOQ workbook imported into operational project #{projectId}.",
            NewValue = new { result.TemplateCode, result.SourceFileName, Lines = result.Lines.Count, result.CostTotal },
            Metadata = new { operationalProjectId = projectId },
        });
        return StatusCode(StatusCodes.Status201Created, result);
    }, ct);

    [HttpGet("boq-revisions/{id:int}/workbook")]
    [RequirePermission("proc.boq", "view")]
    public Task<IActionResult> Workbook(int projectId, int id, CancellationToken ct) => Run("download", projectId, async _ =>
    {
        var content = await workbooks.GetWorkbookAsync(projectId, id, ct);
        return content == null ? NotFound() : PhysicalFile(content.FullPath, content.ContentType, content.OriginalFileName);
    }, ct);

    private async Task<IActionResult> Run(string operation, int projectId, Func<int, Task<IActionResult>> action, CancellationToken ct)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("uid");
        if (!int.TryParse(claim, out var userId)) return Unauthorized();
        if (!await access.CanViewOperationalProjectAsync(userId, projectId, ct)) return NotFound();
        try
        {
            return await action(userId);
        }
        catch (BoqWorkbookException error)
        {
            logger.LogWarning("BOQ workbook {Operation} rejected for project {ProjectId}: {Code}, {IssueCount} issues, first {FirstIssue} (trace {TraceId})",
                operation, projectId, error.Code, error.Result?.IssueCount ?? 0, error.Result?.Issues.FirstOrDefault()?.Code, HttpContext.TraceIdentifier);
            return BadRequest(BoqWorkbookErrors.Body(error));
        }
        catch (ProcurementOperationException error)
        {
            return BadRequest(new { message = error.Message });
        }
    }
}
