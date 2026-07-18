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
/// M2 Basic Design endpoints (NIH-115). Slice 1: metadata + status
/// transitions + Shop Drawing unlock gate. File uploads + attach-to-permit
/// bridge ship in slice 2.
/// </summary>
[ApiController]
[Route("api/basic-design-docs")]
[Route("api/v1/basic-design-docs")]
[Authorize]
public class BasicDesignDocsController(
    IBasicDesignDocService svc,
    IPermissionService permissions,
    IAuditLogger audit) : ControllerBase
{
    [HttpGet]
    [RequirePermission("design.basic", "view")]
    public async Task<ActionResult<BasicDesignDocListResponse>> List(
        [FromQuery] BasicDesignDocListParams parameters, CancellationToken ct)
    {
        var result = await svc.ListAsync(parameters, ct);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [RequirePermission("design.basic", "view")]
    public async Task<ActionResult<BasicDesignDocResponse>> Get(int id, CancellationToken ct)
    {
        var found = await svc.GetAsync(id, ct);
        return found is null ? NotFound() : Ok(found);
    }

    [HttpPost]
    [RequirePermission("design.basic", "manage")]
    public async Task<ActionResult<BasicDesignDocResponse>> Create(
        [FromBody] CreateBasicDesignDocRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.CreateAsync(request, userId.Value, ct);
            audit.Log(new AuditEvent
            {
                Action = "basic-design.create",
                ResourceType = EntityTypes.BasicDesignDoc,
                ResourceId = response.Id.ToString(),
                Message = $"Basic-design doc #{response.Id} ({response.DocumentCode}) created for project {response.DesignProjectId}.",
                NewValue = response,
            });
            return CreatedAtAction(nameof(Get), new { id = response.Id }, response);
        }
        catch (BasicDesignDocOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [RequirePermission("design.basic", "manage")]
    public async Task<ActionResult<BasicDesignDocResponse>> Update(
        int id, [FromBody] UpdateBasicDesignDocRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.UpdateAsync(id, request, userId.Value, ct);
            if (response is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "basic-design.update",
                ResourceType = EntityTypes.BasicDesignDoc,
                ResourceId = id.ToString(),
                Message = $"Basic-design doc #{id} updated.",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (BasicDesignDocOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("design.basic", "manage")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            var removed = await svc.DeleteAsync(id, ct);
            if (!removed) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "basic-design.delete",
                ResourceType = EntityTypes.BasicDesignDoc,
                ResourceId = id.ToString(),
                Message = $"Basic-design doc #{id} deleted.",
            });
            return NoContent();
        }
        catch (BasicDesignDocOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Transition status. Baseline manage gate keeps read-only roles out;
    /// <c>InternallyApproved</c> requires the stricter
    /// <c>design.basic.approve</c> permission.
    /// </summary>
    [HttpPost("{id:int}/status")]
    [RequirePermission("design.basic", "manage")]
    public async Task<ActionResult<BasicDesignDocResponse>> Transition(
        int id, [FromBody] TransitionBasicDesignDocStatusRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        if (string.Equals(request.Status, "InternallyApproved", StringComparison.OrdinalIgnoreCase)
            && !await permissions.HasAsync(userId.Value, "design.basic.approve", ct))
        {
            return Forbid();
        }

        try
        {
            var response = await svc.TransitionStatusAsync(id, request, userId.Value, ct);
            if (response is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "basic-design.transition",
                ResourceType = EntityTypes.BasicDesignDoc,
                ResourceId = id.ToString(),
                Message = $"Basic-design doc #{id} transitioned to {response.Status}.",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (BasicDesignDocOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Push the parent design project from BasicDesign to ShopDrawing.
    /// Blocked when the 3-discipline readiness gate is not met.
    /// </summary>
    [HttpPost("design-project/{projectId:int}/unlock-shop-drawing")]
    [RequirePermission("design.projects", "manage")]
    public async Task<ActionResult<DesignProjectResponse>> UnlockShopDrawing(int projectId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.UnlockShopDrawingAsync(projectId, userId.Value, ct);
            audit.Log(new AuditEvent
            {
                Action = "basic-design.unlock-shop",
                ResourceType = EntityTypes.DesignProject,
                ResourceId = projectId.ToString(),
                Message = $"Design project #{projectId} unlocked to ShopDrawing stage.",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (BasicDesignDocOperationException ex)
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
