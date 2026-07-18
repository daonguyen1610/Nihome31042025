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
/// M2 Shop Drawing endpoints (NIH-116). Slice 1: metadata + state
/// transitions + bulk delete of drafts. File uploads + cross-review
/// workflow + IFC bundle bridge (NIH-118) ship in slice 2.
/// </summary>
[ApiController]
[Route("api/shop-drawings")]
[Route("api/v1/shop-drawings")]
[Authorize]
public class ShopDrawingsController(
    IShopDrawingService svc,
    IPermissionService permissions,
    IAuditLogger audit) : ControllerBase
{
    [HttpGet]
    [RequirePermission("design.shop", "view")]
    public async Task<ActionResult<ShopDrawingListResponse>> List(
        [FromQuery] ShopDrawingListParams parameters, CancellationToken ct)
    {
        var result = await svc.ListAsync(parameters, ct);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [RequirePermission("design.shop", "view")]
    public async Task<ActionResult<ShopDrawingResponse>> Get(int id, CancellationToken ct)
    {
        var found = await svc.GetAsync(id, ct);
        return found is null ? NotFound() : Ok(found);
    }

    [HttpPost]
    [RequirePermission("design.shop", "manage")]
    public async Task<ActionResult<ShopDrawingResponse>> Create(
        [FromBody] CreateShopDrawingRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.CreateAsync(request, userId.Value, ct);
            audit.Log(new AuditEvent
            {
                Action = "shop-drawing.create",
                ResourceType = EntityTypes.ShopDrawing,
                ResourceId = response.Id.ToString(),
                Message = $"Shop-drawing #{response.Id} ({response.DrawingCode}) created for project {response.DesignProjectId}.",
                NewValue = response,
            });
            return CreatedAtAction(nameof(Get), new { id = response.Id }, response);
        }
        catch (ShopDrawingOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [RequirePermission("design.shop", "manage")]
    public async Task<ActionResult<ShopDrawingResponse>> Update(
        int id, [FromBody] UpdateShopDrawingRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.UpdateAsync(id, request, userId.Value, ct);
            if (response is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "shop-drawing.update",
                ResourceType = EntityTypes.ShopDrawing,
                ResourceId = id.ToString(),
                Message = $"Shop-drawing #{id} updated.",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (ShopDrawingOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("design.shop", "manage")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            var removed = await svc.DeleteAsync(id, ct);
            if (!removed) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "shop-drawing.delete",
                ResourceType = EntityTypes.ShopDrawing,
                ResourceId = id.ToString(),
                Message = $"Shop-drawing #{id} deleted.",
            });
            return NoContent();
        }
        catch (ShopDrawingOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Bulk-delete drafts. Returns partial success — the FE bulk-select
    /// hook can toast per-row failures without blowing up the whole
    /// operation when one row has left the Drafting state.
    /// </summary>
    [HttpPost("bulk-delete")]
    [RequirePermission("design.shop", "manage")]
    public async Task<ActionResult<ShopDrawingBulkDeleteResponse>> BulkDelete(
        [FromBody] BulkDeleteShopDrawingsRequest request, CancellationToken ct)
    {
        try
        {
            var result = await svc.BulkDeleteAsync(request.Ids ?? new List<int>(), ct);
            audit.Log(new AuditEvent
            {
                Action = "shop-drawing.bulk-delete",
                ResourceType = EntityTypes.ShopDrawing,
                ResourceId = string.Join(",", request.Ids ?? new List<int>()),
                Message = $"Shop-drawing bulk delete: {result.Deleted}/{result.Requested} removed.",
                NewValue = result,
            });
            return Ok(result);
        }
        catch (ShopDrawingOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Transition status. Baseline manage gate keeps read-only roles out;
    /// <c>Approved</c> requires the stricter
    /// <c>design.shop.approve</c> permission (Design Lead's final review).
    /// </summary>
    [HttpPost("{id:int}/status")]
    [RequirePermission("design.shop", "manage")]
    public async Task<ActionResult<ShopDrawingResponse>> Transition(
        int id, [FromBody] TransitionShopDrawingStatusRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        if (string.Equals(request.Status, "Approved", StringComparison.OrdinalIgnoreCase)
            && !await permissions.HasAsync(userId.Value, "design.shop.approve", ct))
        {
            return Forbid();
        }

        try
        {
            var response = await svc.TransitionStatusAsync(id, request, userId.Value, ct);
            if (response is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "shop-drawing.transition",
                ResourceType = EntityTypes.ShopDrawing,
                ResourceId = id.ToString(),
                Message = $"Shop-drawing #{id} transitioned to {response.Status}.",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (ShopDrawingOperationException ex)
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
