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

// Procurement uploads the filled investment-rate form inside its input-price work package and the
// package lead approves it there, mirroring the project BOQ upload beside it.
[ApiController]
[Authorize]
[Route("api/work-packages/{id:int}/investment-rates")]
[Route("api/v1/work-packages/{id:int}/investment-rates")]
public sealed class WorkPackageInvestmentRatesController(
    IDepartmentWorkPackageService packages,
    InvestmentRateWorkbookService workbooks,
    IPermissionService permissions,
    IAuditLogger audit,
    ILogger<WorkPackageInvestmentRatesController> logger) : ControllerBase
{
    [HttpPost("preview"), Consumes("multipart/form-data")]
    [RequestSizeLimit(ProjectBoqWorkbookParser.MaxFileBytes + 1024 * 1024)]
    [RequirePermission("work.packages", "progress")]
    public Task<IActionResult> Preview(int id, [FromForm] string? templateCode, IFormFile? file, CancellationToken ct) =>
        Run("preview", id, async userId =>
        {
            await packages.RequireInvestmentRateUploadProjectAsync(id, userId, ct);
            return Ok(await workbooks.PreviewAsync(templateCode, file, ct));
        });

    [HttpPost, Consumes("multipart/form-data")]
    [RequestSizeLimit(ProjectBoqWorkbookParser.MaxFileBytes + 1024 * 1024)]
    [RequirePermission("work.packages", "progress")]
    public Task<IActionResult> Import(int id, [FromForm] string? templateCode, IFormFile? file, [FromForm] string? sourceHash,
        CancellationToken ct) => Run("import", id, async userId =>
        {
            var projectId = await packages.RequireInvestmentRateUploadProjectAsync(id, userId, ct);
            var revisionId = await workbooks.ImportAsync(projectId, id, templateCode, file, sourceHash, userId, ct);
            var result = await packages.RecordInvestmentRateUploadAsync(id, revisionId, userId, ct);
            audit.Log(new AuditEvent
            {
                Action = "work.packages.investment-rate.import",
                ResourceType = EntityTypes.ProjectInvestmentRateRevision,
                ResourceId = revisionId.ToString(),
                Message = $"Investment-rate workbook uploaded in work package #{id}.",
                NewValue = new { result.TemplateCode, result.SourceFileName, result.ConvertedAreaSqm, result.RatePerSqm, result.Total },
                Metadata = new { workPackageId = id, operationalProjectId = projectId },
            });
            return StatusCode(StatusCodes.Status201Created, result);
        });

    [HttpGet("{revisionId:int}/file")]
    [RequirePermission("work.packages", "view")]
    public Task<IActionResult> File(int id, int revisionId, CancellationToken ct) => Run("download", id, async userId =>
    {
        var canViewAll = await permissions.HasAsync(userId, "work.packages.view.all", ct);
        var content = await packages.CanViewInvestmentRateAsync(id, revisionId, userId, canViewAll, ct)
            ? await workbooks.GetWorkbookAsync(id, revisionId, ct)
            : null;
        return content == null ? NotFound() : PhysicalFile(content.FullPath, content.ContentType, content.OriginalFileName);
    });

    [HttpPost("{revisionId:int}/approval-email-preview")]
    [RequirePermission("work.packages", "progress")]
    public Task<IActionResult> PreviewApproval(int id, int revisionId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] WorkPackageApprovalRecipientsRequest? request, CancellationToken ct) =>
        Run("approval-preview", id, async userId =>
            Ok(await packages.PreviewInvestmentRateApprovalEmailAsync(id, revisionId, userId, ct, request)));

    [HttpPost("{revisionId:int}/submit")]
    [RequirePermission("work.packages", "progress")]
    [Idempotency("work.packages.investment-rates.submit", requireKey: true)]
    public Task<IActionResult> Submit(int id, int revisionId, [FromBody] SubmitBusinessRecordRequest request, CancellationToken ct) =>
        Run("submit", id, async userId =>
        {
            request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
            var result = await packages.SubmitInvestmentRateAsync(id, revisionId, request, userId, ct);
            return result is null ? NotFound() : Ok(result);
        });

    [HttpPost("{revisionId:int}/decision")]
    [RequirePermission("work.packages", "verify")]
    [Idempotency("work.packages.investment-rates.decision", requireKey: true)]
    public Task<IActionResult> Decide(int id, int revisionId, [FromBody] DecideBusinessRecordRequest request, CancellationToken ct) =>
        Run("decision", id, async userId =>
        {
            request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
            var result = await packages.DecideInvestmentRateAsync(id, revisionId, request, userId, ct);
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
        catch (BoqWorkbookException error)
        {
            logger.LogWarning("Work package investment-rate {Operation} rejected for package {PackageId}: {Code}, {IssueCount} issues (trace {TraceId})",
                operation, packageId, error.Code, error.Result?.IssueCount ?? 0, HttpContext.TraceIdentifier);
            return BadRequest(BoqWorkbookErrors.Body(error));
        }
    }
}
