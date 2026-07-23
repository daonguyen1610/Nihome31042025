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
/// M4 Punch List (Danh mục lỗi tồn đọng / NIH-146) endpoints — CRUD +
/// status transitions + bulk delete. Guarded by
/// <c>construction.punch.view</c> / <c>construction.punch.manage</c>;
/// the Verified transition needs the stricter
/// <c>construction.punch.verify</c> permission.
/// </summary>
[ApiController]
[Route("api/punch-items")]
[Route("api/v1/punch-items")]
[Authorize]
public class PunchItemsController(
    IPunchItemService svc,
    IAuditLogger audit) : ControllerBase
{
    [HttpGet]
    [RequirePermission("construction.punch", "view")]
    public async Task<ActionResult<PunchItemListResponse>> List(
        [FromQuery] PunchItemListParams parameters, CancellationToken ct)
    {
        return Ok(await svc.ListAsync(parameters, ct));
    }

    [HttpGet("{id:int}")]
    [RequirePermission("construction.punch", "view")]
    public async Task<ActionResult<PunchItemResponse>> Get(int id, CancellationToken ct)
    {
        var found = await svc.GetAsync(id, ct);
        return found is null ? NotFound() : Ok(found);
    }

    [HttpPost]
    [RequirePermission("construction.punch", "manage")]
    public async Task<ActionResult<PunchItemResponse>> Create(
        [FromBody] CreatePunchItemRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.CreateAsync(request, userId.Value, ct);
            audit.Log(new AuditEvent
            {
                Action = "punch-item.create",
                ResourceType = EntityTypes.PunchItem,
                ResourceId = response.Id.ToString(),
                Message = $"Punch #{response.Id} ({response.PunchCode}) raised on project {response.DesignProjectId}.",
                NewValue = response,
            });
            return CreatedAtAction(nameof(Get), new { id = response.Id }, response);
        }
        catch (PunchItemOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [RequirePermission("construction.punch", "manage")]
    public async Task<ActionResult<PunchItemResponse>> Update(
        int id, [FromBody] UpdatePunchItemRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.UpdateAsync(id, request, userId.Value, ct);
            if (response is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "punch-item.update",
                ResourceType = EntityTypes.PunchItem,
                ResourceId = id.ToString(),
                Message = $"Punch #{id} updated.",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (PunchItemOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// General status transition — any target except <c>Verified</c>.
    /// Handles reopen (Fixed/Verified → Open), Cancelled and the
    /// InProgress/Fixed forward moves.
    /// </summary>
    [HttpPost("{id:int}/status")]
    [RequirePermission("construction.punch", "manage")]
    public async Task<ActionResult<PunchItemResponse>> TransitionStatus(
        int id, [FromBody] TransitionPunchStatusRequest request, CancellationToken ct)
    {
        if (string.Equals(request.Status?.Trim(), "Verified", StringComparison.OrdinalIgnoreCase))
        {
            // Route Verified through the dedicated endpoint so its
            // stricter permission is enforced declaratively.
            return BadRequest(new { message = "Dùng POST /verify để chuyển sang trạng thái Đã xác nhận." });
        }
        return await ApplyTransition(id, request, ct);
    }

    /// <summary>
    /// Site-verified sign-off. Gated by the stricter
    /// <c>construction.punch.verify</c> permission so ordinary crew
    /// can't close their own fixes.
    /// </summary>
    [HttpPost("{id:int}/verify")]
    [RequirePermission("construction.punch", "verify")]
    public async Task<ActionResult<PunchItemResponse>> Verify(
        int id, [FromBody] TransitionPunchStatusRequest? request, CancellationToken ct)
    {
        var body = request ?? new TransitionPunchStatusRequest();
        body.Status = "Verified";
        return await ApplyTransition(id, body, ct);
    }

    private async Task<ActionResult<PunchItemResponse>> ApplyTransition(
        int id, TransitionPunchStatusRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.TransitionStatusAsync(id, request, userId.Value, ct);
            if (response is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "punch-item.status",
                ResourceType = EntityTypes.PunchItem,
                ResourceId = id.ToString(),
                Message = $"Punch #{id} -> {response.Status} (reopens={response.ReopenCount}).",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (PunchItemOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("construction.punch", "manage")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            var removed = await svc.DeleteAsync(id, ct);
            if (!removed) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "punch-item.delete",
                ResourceType = EntityTypes.PunchItem,
                ResourceId = id.ToString(),
                Message = $"Punch #{id} deleted.",
            });
            return NoContent();
        }
        catch (PunchItemOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("bulk-delete")]
    [RequirePermission("construction.punch", "manage")]
    public async Task<ActionResult<PunchItemBulkDeleteResponse>> BulkDelete(
        [FromBody] BulkDeletePunchItemsRequest request, CancellationToken ct)
    {
        try
        {
            var result = await svc.BulkDeleteAsync(request.Ids ?? new List<int>(), ct);
            audit.Log(new AuditEvent
            {
                Action = "punch-item.bulk-delete",
                ResourceType = EntityTypes.PunchItem,
                Message = $"Bulk delete requested={result.Requested} deleted={result.Deleted} failed={result.Failures.Count}.",
                NewValue = result,
            });
            return Ok(result);
        }
        catch (PunchItemOperationException ex)
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
