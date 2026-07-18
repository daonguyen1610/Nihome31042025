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
/// M3 Permitting checklist endpoints (NIH-137). Per-project auto-generation
/// happens implicitly via <see cref="IDesignProjectService"/> so there is no
/// public POST here — the FE only reads + patches existing rows.
/// </summary>
[ApiController]
[Route("api/permits")]
[Route("api/v1/permits")]
[Authorize]
public class PermitsController(
    IPermitChecklistService svc,
    IAuditLogger audit) : ControllerBase
{
    [HttpGet]
    [RequirePermission("permit.checklists", "view")]
    public async Task<ActionResult<PermitChecklistListResponse>> List(
        [FromQuery] PermitChecklistListParams parameters, CancellationToken ct)
    {
        var result = await svc.ListAsync(parameters, ct);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [RequirePermission("permit.checklists", "view")]
    public async Task<ActionResult<PermitChecklistItemResponse>> Get(int id, CancellationToken ct)
    {
        var found = await svc.GetAsync(id, ct);
        return found is null ? NotFound() : Ok(found);
    }

    [HttpPatch("{id:int}")]
    [RequirePermission("permit.checklists", "manage")]
    public async Task<ActionResult<PermitChecklistItemResponse>> Update(
        int id, [FromBody] UpdatePermitChecklistItemRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.UpdateAsync(id, request, userId.Value, ct);
            if (response is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "permit.update",
                ResourceType = EntityTypes.PermitChecklistItem,
                ResourceId = id.ToString(),
                Message = $"Permit checklist item #{id} updated ({response.PermitTypeCode} → {response.Status}).",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (PermitChecklistOperationException ex)
        {
            audit.Log(new AuditEvent
            {
                Action = "permit.update",
                ResourceType = EntityTypes.PermitChecklistItem,
                ResourceId = id.ToString(),
                Message = ex.Message,
                Status = AuditStatus.Failure,
                FailureReason = ex.Message,
            });
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Re-run the auto-generator for a given project. Useful after the master
    /// template gains a new permit type (e.g. a new local requirement) so
    /// existing projects catch up without a manual DB touch. Idempotent.
    /// </summary>
    [HttpPost("design-project/{projectId:int}/ensure")]
    [RequirePermission("permit.checklists", "manage")]
    public async Task<ActionResult<PermitChecklistListResponse>> Ensure(int projectId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            await svc.EnsureForProjectAsync(projectId, userId.Value, ct);
            audit.Log(new AuditEvent
            {
                Action = "permit.ensure",
                ResourceType = EntityTypes.PermitChecklistItem,
                ResourceId = projectId.ToString(),
                Message = $"Permit checklist regenerated for design project #{projectId}.",
            });
            var listing = await svc.ListAsync(new PermitChecklistListParams
            {
                DesignProjectId = projectId,
                PageSize = 200,
            }, ct);
            return Ok(listing);
        }
        catch (PermitChecklistOperationException ex)
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
