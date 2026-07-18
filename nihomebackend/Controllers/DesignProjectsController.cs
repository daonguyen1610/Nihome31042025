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
    IAuditLogger audit) : ControllerBase
{
    [HttpGet]
    [RequirePermission("design.projects", "view")]
    public async Task<ActionResult<DesignProjectListResponse>> List([FromQuery] DesignProjectListParams parameters, CancellationToken ct)
    {
        var result = await svc.ListAsync(parameters, ct);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [RequirePermission("design.projects", "view")]
    public async Task<ActionResult<DesignProjectResponse>> Get(int id, CancellationToken ct)
    {
        var found = await svc.GetAsync(id, ct);
        return found is null ? NotFound() : Ok(found);
    }

    [HttpPost]
    [RequirePermission("design.projects", "manage")]
    public async Task<ActionResult<DesignProjectResponse>> Create([FromBody] CreateDesignProjectRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
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
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            var removed = await svc.DeleteAsync(id, ct);
            if (!removed) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "design-project.delete",
                ResourceType = EntityTypes.DesignProject,
                ResourceId = id.ToString(),
                Message = $"Design project #{id} deleted.",
            });
            return NoContent();
        }
        catch (DesignProjectOperationException ex)
        {
            audit.Log(new AuditEvent
            {
                Action = "design-project.delete",
                ResourceType = EntityTypes.DesignProject,
                ResourceId = id.ToString(),
                Message = ex.Message,
                Status = AuditStatus.Failure,
                FailureReason = ex.Message,
            });
            return BadRequest(new { message = ex.Message });
        }
    }

    private int? GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return int.TryParse(raw, out var id) ? id : null;
    }
}
