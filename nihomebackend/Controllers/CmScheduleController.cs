using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

[ApiController, Authorize]
[Route("api/operational-projects/{projectId:int}/construction/schedule")]
[Route("api/v1/operational-projects/{projectId:int}/construction/schedule")]
public sealed class CmScheduleController(CmScheduleService service, IProjectAccessService access, TranslationService translations, ILogger<CmScheduleController> logger) : ControllerBase
{
    [HttpGet, RequirePermission("construction.tasks", "view")]
    public Task<IActionResult> Workspace(int projectId, CancellationToken ct) => Run(projectId, actor => service.WorkspaceAsync(projectId, actor, ct), ct);
    [HttpPost("baselines"), RequirePermission("construction.cm-preparation", "manage"), Idempotency("cm.schedule.create", requireKey: true)]
    public Task<IActionResult> Create(int projectId, CmScheduleDraftRequest request, CancellationToken ct) => Run(projectId, actor => service.CreateAsync(projectId, request, actor, ct), ct);
    [HttpPost("baselines/{id:int}/cancel"), RequirePermission("construction.cm-preparation", "manage"), Idempotency("cm.schedule.cancel", requireKey: true)]
    public Task<IActionResult> Cancel(int projectId, int id, ProcurementTransitionRequest request, CancellationToken ct) =>
        Run(projectId, actor => service.CancelDraftAsync(projectId, id, CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion), actor, ct), ct);
    [HttpGet("baselines/{id:int}/email-preview"), RequirePermission("construction.cm-preparation", "manage")]
    public Task<IActionResult> PreviewEmail(int projectId, int id, CancellationToken ct) => Run(projectId, actor => service.PreviewEmailAsync(projectId, id, actor, ct), ct);
    [HttpPost("baselines/{id:int}/submit"), RequirePermission("construction.cm-preparation", "manage"), Idempotency("cm.schedule.submit", requireKey: true)]
    public Task<IActionResult> Submit(int projectId, int id, CmScheduleSubmitRequest request, CancellationToken ct) =>
        Run(projectId, actor => service.SubmitAsync(projectId, id, CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion), request.PreviewToken, actor, ct), ct);
    [HttpPost("baselines/{id:int}/decision"), RequirePermission("construction.cm-preparation", "view"), Idempotency("cm.schedule.decide", requireKey: true)]
    public Task<IActionResult> Decide(int projectId, int id, CmScheduleDecisionRequest request, CancellationToken ct) =>
        Run(projectId, actor => service.DecideAsync(projectId, id, request, actor, ct), ct);
    [HttpPost("snapshots"), RequirePermission("construction.tasks", "view"), Idempotency("cm.schedule.capture", requireKey: true)]
    public Task<IActionResult> Capture(int projectId, CancellationToken ct) => Run(projectId, actor => service.CaptureAsync(projectId, actor, ct), ct);
    [HttpPost("tasks/{taskId:int}/actual"), RequirePermission("construction.tasks", "view"), Idempotency("cm.schedule.actual", requireKey: true)]
    public Task<IActionResult> UpdateActual(int projectId, int taskId, CmActualUpdateRequest request, CancellationToken ct) =>
        Run(projectId, actor => service.UpdateActualAsync(projectId, taskId, request, actor, ct), ct);
    [HttpPost("snapshots/{id:int}/recovery"), RequirePermission("construction.tasks", "view"), Idempotency("cm.schedule.recover", requireKey: true)]
    public Task<IActionResult> Recover(int projectId, int id, CmRecoveryRequest request, CancellationToken ct) =>
        Run(projectId, actor => service.RecoverAsync(projectId, id, request, actor, ct), ct);
    [HttpPost("snapshots/{id:int}/review"), RequirePermission("construction.cm-preparation", "manage"), Idempotency("cm.schedule.review", requireKey: true)]
    public Task<IActionResult> Review(int projectId, int id, ProcurementTransitionRequest request, CancellationToken ct) =>
        Run(projectId, actor => service.ReviewRecoveryAsync(projectId, id, CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion), actor, ct), ct);
    private async Task<IActionResult> Run(int projectId, Func<int, Task<object>> action, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Unauthorized();
        if (!await access.CanViewOperationalProjectAsync(actor, projectId, ct)) return NotFound();
        try
        {
            var result = await action(actor);
            if (!HttpMethods.IsGet(Request.Method)) logger.LogInformation("CM schedule {Operation} completed for project {ProjectId} by user {UserId}. Correlation {CorrelationId}", Request.Path.Value, projectId, actor, HttpContext.TraceIdentifier);
            return Ok(result);
        }
        catch (Exception ex) when (IsDeadlock(ex))
        {
            logger.LogWarning("CM schedule {Operation} conflicted for project {ProjectId}. Code {Code}. Refresh and retry. Correlation {CorrelationId}", Request.Path.Value, projectId, "CM_SCHEDULE_conflict", HttpContext.TraceIdentifier);
            return Conflict(new { code = "CM_SCHEDULE_conflict", message = "Dữ liệu đang được cập nhật. Tải lại tiến độ và thử lại." });
        }
        catch (CmScheduleException ex)
        {
            logger.LogWarning("CM schedule {Operation} rejected for project {ProjectId}. Code {Code}. Correct workspace inputs. Correlation {CorrelationId}", Request.Path.Value, projectId, "CM_SCHEDULE_" + ex.Code, HttpContext.TraceIdentifier);
            var map = await translations.GetTranslationMapAsync("vi");
            return BadRequest(new { code = "CM_SCHEDULE_" + ex.Code, message = map.GetValueOrDefault("cmSchedule.error." + ex.Code, "Không thể hoàn tất thao tác tiến độ. Kiểm tra dữ liệu và thử lại.") });
        }
    }
    private static bool IsDeadlock(Exception error)
    {
        for (Exception? current = error; current is not null; current = current.InnerException)
            if (current is Microsoft.Data.SqlClient.SqlException { Number: 1205 }) return true;
        return false;
    }

}
