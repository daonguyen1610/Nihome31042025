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
/// M2 Concept option endpoints (NIH-114). Slice 1: metadata + status
/// transitions + finalize workflow. Feedback + media endpoints ship in
/// slice 2.
/// </summary>
[ApiController]
[Route("api/concept-options")]
[Route("api/v1/concept-options")]
[Authorize]
public class ConceptOptionsController(
    IConceptOptionService svc,
    IPermissionService permissions,
    IProjectAccessService projectAccess,
    IAuditLogger audit) : ControllerBase
{
    [HttpGet]
    [RequirePermission("design.concepts", "view")]
    public async Task<ActionResult<ConceptOptionListResponse>> List(
        [FromQuery] ConceptOptionListParams parameters, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!parameters.DesignProjectId.HasValue)
            return BadRequest(new { message = "DesignProjectId is required." });
        if (!await projectAccess.CanViewDesignProjectAsync(userId.Value, parameters.DesignProjectId.Value, ct))
            return NotFound();

        var result = await svc.ListAsync(parameters, ct);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [RequirePermission("design.concepts", "view")]
    public async Task<ActionResult<ConceptOptionResponse>> Get(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await CanAccessAsync(userId.Value, id, manage: false, ct)) return NotFound();

        var found = await svc.GetAsync(id, ct);
        return found is null ? NotFound() : Ok(found);
    }

    [HttpPost]
    [RequirePermission("design.concepts", "manage")]
    public async Task<ActionResult<ConceptOptionResponse>> Create(
        [FromBody] CreateConceptOptionRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await projectAccess.CanManageDesignProjectAsync(userId.Value, request.DesignProjectId, ct))
            return NotFound();
        try
        {
            var response = await svc.CreateAsync(request, userId.Value, ct);
            audit.Log(new AuditEvent
            {
                Action = "concept.create",
                ResourceType = EntityTypes.ConceptOption,
                ResourceId = response.Id.ToString(),
                Message = $"Concept option #{response.Id} created for project {response.DesignProjectId}.",
                NewValue = response,
            });
            return CreatedAtAction(nameof(Get), new { id = response.Id }, response);
        }
        catch (ConceptOptionOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [RequirePermission("design.concepts", "manage")]
    public async Task<ActionResult<ConceptOptionResponse>> Update(
        int id, [FromBody] UpdateConceptOptionRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await CanAccessAsync(userId.Value, id, manage: true, ct)) return NotFound();
        try
        {
            var response = await svc.UpdateAsync(id, request, userId.Value, ct);
            if (response is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "concept.update",
                ResourceType = EntityTypes.ConceptOption,
                ResourceId = id.ToString(),
                Message = $"Concept option #{id} updated.",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (ConceptOptionOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("design.concepts", "manage")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await CanAccessAsync(userId.Value, id, manage: true, ct)) return NotFound();

        try
        {
            var removed = await svc.DeleteAsync(id, ct);
            if (!removed) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "concept.delete",
                ResourceType = EntityTypes.ConceptOption,
                ResourceId = id.ToString(),
                Message = $"Concept option #{id} deleted.",
            });
            return NoContent();
        }
        catch (ConceptOptionOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Move the option to a new status. When the target is Finalized the
    /// server discards sibling options + unlocks the design project's Basic
    /// Design stage; permission-gated on the dedicated
    /// <c>design.concepts.finalize</c> code to match the RBAC catalogue.
    /// Baseline <c>manage</c> gate keeps the route out of read-only roles.
    /// </summary>
    [HttpPost("{id:int}/status")]
    [RequirePermission("design.concepts", "manage")]
    public async Task<ActionResult<ConceptOptionResponse>> Transition(
        int id, [FromBody] TransitionConceptOptionStatusRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var projectId = await projectAccess.ResolveDesignProjectIdAsync(
            DesignProjectResourceType.ConceptOption, id, ct);
        if (!projectId.HasValue ||
            !await projectAccess.CanManageDesignProjectAsync(userId.Value, projectId.Value, ct))
            return NotFound();

        // Finalize is a stricter permission on top of manage.
        if (string.Equals(request.Status, "Finalized", StringComparison.OrdinalIgnoreCase) &&
            !await projectAccess.CanApproveDesignProjectAsync(userId.Value, projectId.Value, ct))
        {
            return NotFound();
        }
        if (string.Equals(request.Status, "Finalized", StringComparison.OrdinalIgnoreCase) &&
            !await permissions.HasAsync(userId.Value, "design.concepts.finalize", ct))
        {
            return Forbid();
        }

        try
        {
            var response = await svc.TransitionStatusAsync(id, request, userId.Value, ct);
            if (response is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "concept.transition",
                ResourceType = EntityTypes.ConceptOption,
                ResourceId = id.ToString(),
                Message = $"Concept option #{id} transitioned to {response.Status}.",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (ConceptOptionOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private int? GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return int.TryParse(raw, out var id) ? id : null;
    }

    private async Task<bool> CanAccessAsync(int userId, int id, bool manage, CancellationToken ct)
    {
        var projectId = await projectAccess.ResolveDesignProjectIdAsync(
            DesignProjectResourceType.ConceptOption, id, ct);
        if (!projectId.HasValue) return false;

        return manage
            ? await projectAccess.CanManageDesignProjectAsync(userId, projectId.Value, ct)
            : await projectAccess.CanViewDesignProjectAsync(userId, projectId.Value, ct);
    }
}
