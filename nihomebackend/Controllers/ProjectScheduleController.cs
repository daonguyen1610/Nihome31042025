using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

[ApiController]
[Authorize]
[Route("api/operational-projects/{projectId:int}/master-schedule")]
[Route("api/v1/operational-projects/{projectId:int}/master-schedule")]
public sealed class ProjectScheduleController(IProjectScheduleService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ProjectScheduleResponse>> Get(int projectId, CancellationToken ct)
    {
        var userId = UserId();
        if (userId is null) return Unauthorized();
        try
        {
            var result = await service.GetAsync(projectId, userId.Value, ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (ProjectScheduleOperationException exception) { return StatusCode(exception.StatusCode, new { message = exception.Message }); }
    }

    [HttpPost("revisions")]
    [RequirePermission("operations.projects", "manage")]
    [Idempotency("operations.projects.master-schedule.create", requireKey: true)]
    public async Task<ActionResult<ProjectScheduleResponse>> CreateRevision(
        int projectId, [FromBody] SaveProjectScheduleRevisionRequest request, CancellationToken ct)
    {
        var userId = UserId();
        if (userId is null) return Unauthorized();
        try { return Ok(await service.CreateRevisionAsync(projectId, request, userId.Value, ct)); }
        catch (ProjectScheduleOperationException exception) { return StatusCode(exception.StatusCode, new { message = exception.Message }); }
    }

    [HttpPut("revisions/{revisionId:int}")]
    [RequirePermission("operations.projects", "manage")]
    [Idempotency("operations.projects.master-schedule.update", requireKey: true)]
    public async Task<ActionResult<ProjectScheduleResponse>> UpdateRevision(
        int projectId, int revisionId, [FromBody] SaveProjectScheduleRevisionRequest request, CancellationToken ct)
    {
        var userId = UserId();
        if (userId is null) return Unauthorized();
        try
        {
            var result = await service.UpdateRevisionAsync(projectId, revisionId, request, userId.Value, ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (ProjectScheduleOperationException exception) { return StatusCode(exception.StatusCode, new { message = exception.Message }); }
    }

    [HttpPost("revisions/{revisionId:int}/submit")]
    [RequirePermission("operations.projects", "manage")]
    [Idempotency("operations.projects.master-schedule.submit", requireKey: true)]
    public async Task<ActionResult<ProjectScheduleResponse>> Submit(
        int projectId, int revisionId, [FromBody] SubmitProjectScheduleRevisionRequest request, CancellationToken ct)
    {
        var userId = UserId();
        if (userId is null) return Unauthorized();
        try
        {
            var result = await service.SubmitAsync(projectId, revisionId, request, userId.Value, ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (ProjectScheduleOperationException exception) { return StatusCode(exception.StatusCode, new { message = exception.Message }); }
    }

    [HttpPost("revisions/{revisionId:int}/review")]
    [Idempotency("operations.projects.master-schedule.review", requireKey: true)]
    public async Task<ActionResult<ProjectScheduleResponse>> Review(
        int projectId, int revisionId, [FromBody] ReviewProjectScheduleRevisionRequest request, CancellationToken ct)
    {
        var userId = UserId();
        if (userId is null) return Unauthorized();
        try
        {
            var result = await service.ReviewAsync(projectId, revisionId, request, userId.Value, ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (ProjectScheduleOperationException exception) { return StatusCode(exception.StatusCode, new { message = exception.Message }); }
    }

    private int? UserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
