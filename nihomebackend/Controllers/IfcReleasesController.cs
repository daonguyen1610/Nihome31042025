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
/// M2 IFC Release endpoints (NIH-118). Slice 1: header CRUD, item /
/// recipient management, per-recipient acknowledgement, atomic release
/// action. PDF watermark + zip export + email fanout land in slice 2.
/// </summary>
[ApiController]
[Route("api/ifc-releases")]
[Route("api/v1/ifc-releases")]
[Authorize]
public class IfcReleasesController(
    IIfcReleaseService svc,
    IAuditLogger audit) : ControllerBase
{
    [HttpGet]
    [RequirePermission("design.ifc", "view")]
    public async Task<ActionResult<IfcReleaseListResponse>> List(
        [FromQuery] IfcReleaseListParams parameters, CancellationToken ct)
    {
        var result = await svc.ListAsync(parameters, ct);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [RequirePermission("design.ifc", "view")]
    public async Task<ActionResult<IfcReleaseResponse>> Get(int id, CancellationToken ct)
    {
        var found = await svc.GetAsync(id, ct);
        return found is null ? NotFound() : Ok(found);
    }

    [HttpPost]
    [RequirePermission("design.ifc", "manage")]
    public async Task<ActionResult<IfcReleaseResponse>> Create(
        [FromBody] CreateIfcReleaseRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.CreateAsync(request, userId.Value, ct);
            audit.Log(new AuditEvent
            {
                Action = "ifc-release.create",
                ResourceType = EntityTypes.IfcRelease,
                ResourceId = response.Id.ToString(),
                Message = $"IFC release #{response.Id} ({response.ReleaseNumber}) created for project {response.DesignProjectId}.",
                NewValue = response,
            });
            return CreatedAtAction(nameof(Get), new { id = response.Id }, response);
        }
        catch (IfcReleaseOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [RequirePermission("design.ifc", "manage")]
    public async Task<ActionResult<IfcReleaseResponse>> Update(
        int id, [FromBody] UpdateIfcReleaseRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.UpdateAsync(id, request, userId.Value, ct);
            if (response is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "ifc-release.update",
                ResourceType = EntityTypes.IfcRelease,
                ResourceId = id.ToString(),
                Message = $"IFC release #{id} updated.",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (IfcReleaseOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("design.ifc", "manage")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            var removed = await svc.DeleteAsync(id, ct);
            if (!removed) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "ifc-release.delete",
                ResourceType = EntityTypes.IfcRelease,
                ResourceId = id.ToString(),
                Message = $"IFC release #{id} deleted.",
            });
            return NoContent();
        }
        catch (IfcReleaseOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/items")]
    [RequirePermission("design.ifc", "manage")]
    public async Task<ActionResult<IfcReleaseResponse>> AddItems(
        int id, [FromBody] AddIfcReleaseItemsRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.AddItemsAsync(id, request, userId.Value, ct);
            audit.Log(new AuditEvent
            {
                Action = "ifc-release.add-items",
                ResourceType = EntityTypes.IfcRelease,
                ResourceId = id.ToString(),
                Message = $"Added {request.ShopDrawingIds.Count} drawing(s) to IFC release #{id}.",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (IfcReleaseOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}/items/{itemId:int}")]
    [RequirePermission("design.ifc", "manage")]
    public async Task<ActionResult<IfcReleaseResponse>> RemoveItem(
        int id, int itemId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.RemoveItemAsync(id, itemId, userId.Value, ct);
            return Ok(response);
        }
        catch (IfcReleaseOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/recipients")]
    [RequirePermission("design.ifc", "manage")]
    public async Task<ActionResult<IfcReleaseResponse>> AddRecipient(
        int id, [FromBody] AddIfcReleaseRecipientRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.AddRecipientAsync(id, request, userId.Value, ct);
            return Ok(response);
        }
        catch (IfcReleaseOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}/recipients/{recipientId:int}")]
    [RequirePermission("design.ifc", "manage")]
    public async Task<ActionResult<IfcReleaseResponse>> RemoveRecipient(
        int id, int recipientId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.RemoveRecipientAsync(id, recipientId, userId.Value, ct);
            return Ok(response);
        }
        catch (IfcReleaseOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/recipients/{recipientId:int}/acknowledge")]
    [RequirePermission("design.ifc", "manage")]
    public async Task<ActionResult<IfcReleaseResponse>> AcknowledgeRecipient(
        int id, int recipientId,
        [FromBody] AcknowledgeIfcReleaseRecipientRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.AcknowledgeRecipientAsync(id, recipientId, request, userId.Value, ct);
            audit.Log(new AuditEvent
            {
                Action = "ifc-release.acknowledge",
                ResourceType = EntityTypes.IfcRelease,
                ResourceId = id.ToString(),
                Message = $"Recipient #{recipientId} of IFC release #{id} acknowledged receipt.",
            });
            return Ok(response);
        }
        catch (IfcReleaseOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Atomic release: flips every bundled ShopDrawing to Released and
    /// locks the release header. Requires the stricter
    /// <c>design.ifc.release</c> permission — this is the only writer for
    /// the ShopDrawing "Released" state.
    /// </summary>
    [HttpPost("{id:int}/release")]
    [RequirePermission("design.ifc", "release")]
    public async Task<ActionResult<IfcReleaseResponse>> Release(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.ReleaseAsync(id, userId.Value, ct);
            audit.Log(new AuditEvent
            {
                Action = "ifc-release.release",
                ResourceType = EntityTypes.IfcRelease,
                ResourceId = id.ToString(),
                Message = $"IFC release #{id} shipped with {response.Items.Count} drawing(s).",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (IfcReleaseOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/cancel")]
    [RequirePermission("design.ifc", "manage")]
    public async Task<ActionResult<IfcReleaseResponse>> Cancel(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.CancelAsync(id, userId.Value, ct);
            audit.Log(new AuditEvent
            {
                Action = "ifc-release.cancel",
                ResourceType = EntityTypes.IfcRelease,
                ResourceId = id.ToString(),
                Message = $"IFC release #{id} cancelled.",
            });
            return Ok(response);
        }
        catch (IfcReleaseOperationException ex)
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
