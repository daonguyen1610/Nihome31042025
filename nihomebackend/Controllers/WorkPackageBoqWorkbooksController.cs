using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using NihomeBackend.Authorization;
using NihomeBackend.Constants;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using NihomeBackend.Services.Audit;

namespace NihomeBackend.Controllers;

// Procurement uploads the filled BOQ form inside its input-price work package and the package
// lead approves it there. Scope follows the work package, not Procurement control permissions.
[ApiController]
[Authorize]
[Route("api/work-packages/{id:int}/boq-workbooks")]
[Route("api/v1/work-packages/{id:int}/boq-workbooks")]
public sealed class WorkPackageBoqWorkbooksController(
    IDepartmentWorkPackageService packages,
    ProjectBoqWorkbookService workbooks,
    IPermissionService permissions,
    IAuditLogger audit,
    ILogger<WorkPackageBoqWorkbooksController> logger) : ControllerBase
{
    [HttpPost("preview"), Consumes("multipart/form-data")]
    [RequestSizeLimit(ProjectBoqWorkbookParser.MaxFileBytes + 1024 * 1024)]
    [RequirePermission("work.packages", "progress")]
    public Task<IActionResult> Preview(int id, [FromForm] string? templateCode, IFormFile? file, CancellationToken ct) =>
        Run("preview", id, async userId =>
        {
            var projectId = await packages.RequireBoqUploadProjectAsync(id, userId, ct);
            return Ok(await workbooks.PreviewAsync(projectId, templateCode, file, ct));
        });

    [HttpPost, Consumes("multipart/form-data")]
    [RequestSizeLimit(ProjectBoqWorkbookParser.MaxFileBytes + 1024 * 1024)]
    [RequirePermission("work.packages", "progress")]
    public Task<IActionResult> Import(int id, [FromForm] string? templateCode, IFormFile? file, [FromForm] string? sourceHash,
        CancellationToken ct) => Run("import", id, async userId =>
        {
            var projectId = await packages.RequireBoqUploadProjectAsync(id, userId, ct);
            var revision = await workbooks.ImportAsync(projectId, templateCode, file, sourceHash, null, userId, ct, id);
            var result = await packages.RecordBoqUploadAsync(id, revision.Id, userId, ct);
            audit.Log(new AuditEvent
            {
                Action = "work.packages.boq-workbook.import",
                ResourceType = EntityTypes.ProjectBoqRevision,
                ResourceId = revision.Id.ToString(),
                Message = $"BOQ workbook uploaded in work package #{id}.",
                NewValue = new { revision.TemplateCode, revision.SourceFileName, result.LineCount, result.Total },
                Metadata = new { workPackageId = id, operationalProjectId = projectId },
            });
            return StatusCode(StatusCodes.Status201Created, result);
        });

    [HttpGet("{revisionId:int}/file")]
    [RequirePermission("work.packages", "view")]
    public Task<IActionResult> File(int id, int revisionId, CancellationToken ct) => Run("download", id, async userId =>
    {
        var canViewAll = await permissions.HasAsync(userId, "work.packages.view.all", ct);
        var projectId = await packages.FindVisibleBoqProjectAsync(id, revisionId, userId, canViewAll, ct);
        var content = projectId is null ? null : await workbooks.GetWorkbookAsync(projectId.Value, revisionId, ct);
        return content == null ? NotFound() : PhysicalFile(content.FullPath, content.ContentType, content.OriginalFileName);
    });

    [HttpPost("{revisionId:int}/approval-email-preview")]
    [RequirePermission("work.packages", "progress")]
    public Task<IActionResult> PreviewApproval(int id, int revisionId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] WorkPackageApprovalRecipientsRequest? request, CancellationToken ct) =>
        Run("approval-preview", id, async userId => Ok(await packages.PreviewBoqApprovalEmailAsync(id, revisionId, userId, ct, request)));

    [HttpPost("{revisionId:int}/submit")]
    [RequirePermission("work.packages", "progress")]
    [Idempotency("work.packages.boq-workbooks.submit", requireKey: true)]
    public Task<IActionResult> Submit(int id, int revisionId, [FromBody] SubmitBusinessRecordRequest request, CancellationToken ct) =>
        Run("submit", id, async userId =>
        {
            request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
            var result = await packages.SubmitBoqWorkbookAsync(id, revisionId, request, userId, ct);
            return result is null ? NotFound() : Ok(result);
        });

    [HttpPost("{revisionId:int}/decision")]
    [RequirePermission("work.packages", "verify")]
    [Idempotency("work.packages.boq-workbooks.decision", requireKey: true)]
    public Task<IActionResult> Decide(int id, int revisionId, [FromBody] DecideBusinessRecordRequest request, CancellationToken ct) =>
        Run("decision", id, async userId =>
        {
            request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
            var result = await packages.DecideBoqWorkbookAsync(id, revisionId, request, userId, ct);
            return result is null ? NotFound() : Ok(result);
        });

    private async Task<IActionResult> Run(string operation, int packageId, Func<int, Task<IActionResult>> action)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("uid");
        if (!int.TryParse(claim, out var userId)) return Unauthorized();
        try
        {
            return await action(userId);
        }
        catch (DepartmentWorkPackageScopeException)
        {
            return NotFound();
        }
        catch (DepartmentWorkPackageOperationException error)
        {
            return BadRequest(new { message = error.Message });
        }
        catch (ProcurementOperationException error)
        {
            return BadRequest(new { message = error.Message });
        }
        catch (BoqWorkbookException error)
        {
            logger.LogWarning("Work package BOQ {Operation} rejected for package {PackageId}: {Code}, {IssueCount} issues (trace {TraceId})",
                operation, packageId, error.Code, error.Result?.IssueCount ?? 0, HttpContext.TraceIdentifier);
            return BadRequest(BoqWorkbookErrors.Body(error));
        }
    }
}
