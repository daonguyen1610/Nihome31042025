using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Constants;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;
using NihomeBackend.Services.Audit;

namespace NihomeBackend.Controllers;

/// <summary>
/// M4 Construction schedule (Tiến độ Gantt / NIH-141) endpoints — CRUD
/// on <see cref="Models.ConstructionTask"/>, progress updates, dependency
/// wiring and bulk delete. Guarded by <c>construction.tasks.view</c> /
/// <c>construction.tasks.manage</c>.
/// </summary>
[ApiController]
[Route("api/construction-tasks")]
[Route("api/v1/construction-tasks")]
[Authorize]
public class ConstructionTasksController(
    IConstructionTaskService svc,
    IAuditLogger audit) : ControllerBase
{
    [HttpGet]
    [RequirePermission("construction.tasks", "view")]
    public async Task<ActionResult<ConstructionTaskListResponse>> List(
        [FromQuery] ConstructionTaskListParams parameters, CancellationToken ct)
    {
        var result = await svc.ListAsync(parameters, ct);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [RequirePermission("construction.tasks", "view")]
    public async Task<ActionResult<ConstructionTaskResponse>> Get(int id, CancellationToken ct)
    {
        var found = await svc.GetAsync(id, ct);
        return found is null ? NotFound() : Ok(found);
    }

    [HttpPost]
    [RequirePermission("construction.tasks", "manage")]
    public async Task<ActionResult<ConstructionTaskResponse>> Create(
        [FromBody] CreateConstructionTaskRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.CreateAsync(request, userId.Value, ct);
            audit.Log(new AuditEvent
            {
                Action = "construction-task.create",
                ResourceType = EntityTypes.ConstructionTask,
                ResourceId = response.Id.ToString(),
                Message = $"Construction task #{response.Id} ({response.TaskCode}) created on project {response.DesignProjectId}.",
                NewValue = response,
            });
            return CreatedAtAction(nameof(Get), new { id = response.Id }, response);
        }
        catch (ConstructionTaskOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [RequirePermission("construction.tasks", "manage")]
    public async Task<ActionResult<ConstructionTaskResponse>> Update(
        int id, [FromBody] UpdateConstructionTaskRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.UpdateAsync(id, request, userId.Value, ct);
            if (response is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "construction-task.update",
                ResourceType = EntityTypes.ConstructionTask,
                ResourceId = id.ToString(),
                Message = $"Construction task #{id} updated.",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (ConstructionTaskOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("{id:int}/progress")]
    [RequirePermission("construction.tasks", "manage")]
    public async Task<ActionResult<ConstructionTaskResponse>> UpdateProgress(
        int id, [FromBody] UpdateConstructionTaskProgressRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.UpdateProgressAsync(id, request, userId.Value, ct);
            if (response is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "construction-task.progress",
                ResourceType = EntityTypes.ConstructionTask,
                ResourceId = id.ToString(),
                Message = $"Construction task #{id} progress -> {response.ProgressPercent}% ({response.Status}).",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (ConstructionTaskOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}/predecessors")]
    [RequirePermission("construction.tasks", "manage")]
    public async Task<ActionResult<ConstructionTaskResponse>> SetPredecessors(
        int id, [FromBody] SetConstructionTaskPredecessorsRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.SetPredecessorsAsync(id, request, userId.Value, ct);
            audit.Log(new AuditEvent
            {
                Action = "construction-task.predecessors",
                ResourceType = EntityTypes.ConstructionTask,
                ResourceId = id.ToString(),
                Message = $"Construction task #{id} predecessors set ({response.Predecessors.Count} edge(s)).",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (ConstructionTaskOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("construction.tasks", "manage")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            var removed = await svc.DeleteAsync(id, ct);
            if (!removed) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "construction-task.delete",
                ResourceType = EntityTypes.ConstructionTask,
                ResourceId = id.ToString(),
                Message = $"Construction task #{id} deleted.",
            });
            return NoContent();
        }
        catch (ConstructionTaskOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("bulk-delete")]
    [RequirePermission("construction.tasks", "manage")]
    public async Task<ActionResult<ConstructionTaskBulkDeleteResponse>> BulkDelete(
        [FromBody] BulkDeleteConstructionTasksRequest request, CancellationToken ct)
    {
        try
        {
            var result = await svc.BulkDeleteAsync(request.Ids ?? new List<int>(), ct);
            audit.Log(new AuditEvent
            {
                Action = "construction-task.bulk-delete",
                ResourceType = EntityTypes.ConstructionTask,
                Message = $"Bulk delete requested={result.Requested} deleted={result.Deleted} failed={result.Failures.Count}.",
                NewValue = result,
            });
            return Ok(result);
        }
        catch (ConstructionTaskOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private int? GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return int.TryParse(raw, out var id) ? id : null;
    }
}
