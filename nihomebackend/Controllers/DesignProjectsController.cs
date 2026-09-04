using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Constants;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;
using NihomeBackend.Services.Audit;
using NihomeBackend.Services.HardDelete;

namespace NihomeBackend.Controllers;

/// <summary>
/// M2 Design Project (Dự án thiết kế) endpoints — NIH-113 overview slice.
/// Per-stage documents (Concept / Basic / Shop Drawing / Revision / IFC)
/// are exposed by their own controllers in NIH-114..118.
/// </summary>
[ApiController]
[Route("api/design-projects")]
[Route("api/v1/design-projects")]
[Authorize]
public class DesignProjectsController(
    IDesignProjectService svc,
    IProjectAccessService projectAccess,
    IAuditLogger audit) : ControllerBase
{
    [HttpGet]
    [RequirePermission("design.projects", "view")]
    public async Task<ActionResult<DesignProjectListResponse>> List([FromQuery] DesignProjectListParams parameters, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var result = await svc.ListAsync(parameters, userId.Value, ct);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [RequirePermission("design.projects", "view")]
    public async Task<ActionResult<DesignProjectResponse>> Get(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await projectAccess.CanViewDesignProjectAsync(userId.Value, id, ct)) return NotFound();
        var found = await svc.GetAsync(id, ct);
        return found is null ? NotFound() : Ok(found);
    }

    [HttpPost]
    [RequirePermission("design.projects", "manage")]
    public async Task<ActionResult<DesignProjectResponse>> Create([FromBody] CreateDesignProjectRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var operationalProjectId = await projectAccess.ResolveDesignCreateOperationalProjectIdAsync(
            request.OperationalProjectId, request.ContractId, ct);
        if (!operationalProjectId.HasValue ||
            !await projectAccess.CanManageTeamAsync(userId.Value, operationalProjectId.Value, ct))
        {
            return NotFound();
        }
        try
        {
            var response = await svc.CreateAsync(request, userId.Value, ct);
            audit.Log(new AuditEvent
            {
                Action = "design-project.create",
                ResourceType = EntityTypes.DesignProject,
                ResourceId = response.Id.ToString(),
                Message = $"Design project #{response.Id} ({response.ProjectCode}) created.",
                NewValue = response,
            });
            return CreatedAtAction(nameof(Get), new { id = response.Id }, response);
        }
        catch (DesignProjectOperationException ex)
        {
            audit.Log(new AuditEvent
            {
                Action = "design-project.create",
                ResourceType = EntityTypes.DesignProject,
                Message = ex.Message,
                Status = AuditStatus.Failure,
                FailureReason = ex.Message,
            });
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [RequirePermission("design.projects", "manage")]
    public async Task<ActionResult<DesignProjectResponse>> Update(int id, [FromBody] UpdateDesignProjectRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await projectAccess.CanManageDesignProjectAsync(userId.Value, id, ct)) return NotFound();
        try
        {
            var response = await svc.UpdateAsync(id, request, userId.Value, ct);
            if (response is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "design-project.update",
                ResourceType = EntityTypes.DesignProject,
                ResourceId = id.ToString(),
                Message = $"Design project #{id} updated.",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (DesignProjectOperationException ex)
        {
            audit.Log(new AuditEvent
            {
                Action = "design-project.update",
                ResourceType = EntityTypes.DesignProject,
                ResourceId = id.ToString(),
                Message = ex.Message,
                Status = AuditStatus.Failure,
                FailureReason = ex.Message,
            });
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("design.projects", "manage")]
    public async Task<IActionResult> Delete(
        int id,
        [FromBody] ConfirmDeletionRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await projectAccess.CanManageDesignProjectAsync(userId.Value, id, ct)) return NotFound();
        try
        {
            var result = await svc.DeleteAsync(id, request, userId.Value, ct);
            if (result is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = result.IsComplete ? "design-project.delete" : "design-project.delete_requested",
                ResourceType = EntityTypes.DesignProject,
                ResourceId = id.ToString(),
                Message = $"Design project #{id} durable deletion is {result.Status}.",
                NewValue = result,
            });
            return result.IsComplete ? NoContent() : AcceptedOperation(result);
        }
        catch (DesignProjectOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (DeletionPlanChangedException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (HardDeleteOperationConflictException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("{id:int}/deletion-impact")]
    [RequirePermission("design.projects", "manage")]
    public async Task<ActionResult<DeletionImpactResponse>> GetDeletionImpact(
        int id,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await projectAccess.CanManageDesignProjectAsync(userId.Value, id, ct)) return NotFound();
        var impact = await svc.GetDeletionImpactAsync(id, ct);
        return impact is null ? NotFound() : Ok(impact);
    }

    private int? GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return int.TryParse(raw, out var id) ? id : null;
    }

    private IActionResult AcceptedOperation(HardDeleteOperationResult result)
    {
        Response.Headers.Location = Url.Action(
            nameof(HardDeleteOperationsController.GetStatus),
            "HardDeleteOperations",
            new { operationId = result.OperationId })!;
        return StatusCode(StatusCodes.Status202Accepted, result);
    }
}
