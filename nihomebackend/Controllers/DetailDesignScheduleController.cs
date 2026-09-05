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
[Route("api/operational-projects/{projectId:int}/design-schedule")]
[Route("api/v1/operational-projects/{projectId:int}/design-schedule")]
[Authorize]
public sealed class DetailDesignScheduleController(
    IDetailDesignScheduleService service,
    IProjectAccessService access,
    IAuditLogger audit) : ControllerBase
{
    [HttpGet]
    [RequirePermission("operations.projects", "view")]
    public async Task<ActionResult<DesignScheduleResponse>> Get(
        int projectId,
        [FromQuery] DesignScheduleQuery query,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var result = await service.GetAsync(projectId, query, userId.Value, ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (DesignScheduleOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("initialize")]
    [RequirePermission("design.schedule", "manage")]
    [Idempotency("operations.projects.design-schedule.initialize", requireKey: true)]
    public async Task<ActionResult<DesignScheduleResponse>> Initialize(
        int projectId,
        [FromBody] InitializeDesignScheduleRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await access.CanManageDesignScheduleAsync(userId.Value, projectId, ct)) return NotFound();
        try
        {
            var result = await service.InitializeAsync(projectId, request, userId.Value, ct);
            Audit("design-schedule.initialize", projectId, projectId, result);
            return Ok(result);
        }
        catch (DesignScheduleOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPut("phases/{phaseId:int}")]
    [RequirePermission("design.schedule", "manage")]
    [Idempotency("operations.projects.design-schedule.phases.update", requireKey: true)]
    public async Task<ActionResult<DesignSchedulePhaseResponse>> UpdatePhase(
        int projectId,
        int phaseId,
        [FromBody] UpsertDesignSchedulePhaseRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await access.CanManageDesignScheduleAsync(userId.Value, projectId, ct)) return NotFound();
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        try
        {
            var result = await service.UpdatePhaseAsync(projectId, phaseId, request, userId.Value, ct);
            if (result is null) return NotFound();
            Audit("design-schedule.phase.update", projectId, phaseId, result);
            CrmConcurrency.SetResponseEntityTag(Response, result.RowVersion);
            return Ok(result);
        }
        catch (DesignScheduleOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("phases/{phaseId:int}/tasks")]
    [RequirePermission("design.schedule", "manage")]
    [Idempotency("operations.projects.design-schedule.tasks.create", requireKey: true)]
    public async Task<ActionResult<DesignScheduleTaskResponse>> CreateTask(
        int projectId,
        int phaseId,
        [FromBody] UpsertDesignScheduleTaskRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await access.CanManageDesignScheduleAsync(userId.Value, projectId, ct)) return NotFound();
        try
        {
            var result = await service.CreateTaskAsync(projectId, phaseId, request, userId.Value, ct);
            if (result is null) return NotFound();
            Audit("design-schedule.task.create", projectId, result.Id, result);
            CrmConcurrency.SetResponseEntityTag(Response, result.RowVersion);
            return Created($"/api/operational-projects/{projectId}/design-schedule", result);
        }
        catch (DesignScheduleOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPut("tasks/{taskId:int}")]
    [RequirePermission("design.schedule", "manage")]
    [Idempotency("operations.projects.design-schedule.tasks.update", requireKey: true)]
    public async Task<ActionResult<DesignScheduleTaskResponse>> UpdateTask(
        int projectId,
        int taskId,
        [FromBody] UpsertDesignScheduleTaskRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await access.CanManageDesignScheduleAsync(userId.Value, projectId, ct)) return NotFound();
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        try
        {
            var result = await service.UpdateTaskAsync(projectId, taskId, request, userId.Value, ct);
            if (result is null) return NotFound();
            Audit("design-schedule.task.update", projectId, taskId, result);
            CrmConcurrency.SetResponseEntityTag(Response, result.RowVersion);
            return Ok(result);
        }
        catch (DesignScheduleOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    private void Audit(string action, int projectId, int entityId, object value) => audit.Log(new AuditEvent
    {
        Action = action,
        ResourceType = "DetailDesignSchedule",
        ResourceId = $"{projectId}:{entityId}",
        Message = $"Operational project #{projectId} design schedule record #{entityId} changed.",
        NewValue = value,
    });

    private int? GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return int.TryParse(raw, out var id) ? id : null;
    }
}