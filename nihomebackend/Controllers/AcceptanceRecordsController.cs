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
/// M4 partial acceptance (Nghiệm thu từng phần / NIH-143) endpoints.
/// Guarded by <c>construction.acceptance.view</c> /
/// <c>construction.acceptance.manage</c> for CRUD + non-approving
/// transitions, and by <c>construction.acceptance.approve</c> for the
/// dedicated <c>/approve</c> action.
/// </summary>
[ApiController]
[Route("api/acceptance-records")]
[Route("api/v1/acceptance-records")]
[Authorize]
public class AcceptanceRecordsController(
    IAcceptanceRecordService svc,
    IAuditLogger audit) : ControllerBase
{
    [HttpGet]
    [RequirePermission("construction.acceptance", "view")]
    public async Task<ActionResult<AcceptanceRecordListResponse>> List(
        [FromQuery] AcceptanceRecordListParams parameters, CancellationToken ct)
    {
        var result = await svc.ListAsync(parameters, ct);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [RequirePermission("construction.acceptance", "view")]
    public async Task<ActionResult<AcceptanceRecordResponse>> Get(int id, CancellationToken ct)
    {
        var found = await svc.GetAsync(id, ct);
        return found is null ? NotFound() : Ok(found);
    }

    [HttpPost]
    [RequirePermission("construction.acceptance", "manage")]
    public async Task<ActionResult<AcceptanceRecordResponse>> Create(
        [FromBody] CreateAcceptanceRecordRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.CreateAsync(request, userId.Value, ct);
            audit.Log(new AuditEvent
            {
                Action = "acceptance-record.create",
                ResourceType = EntityTypes.AcceptanceRecord,
                ResourceId = response.Id.ToString(),
                Message = $"Acceptance record #{response.Id} ({response.AcceptanceCode}) created on project {response.DesignProjectId}.",
                NewValue = response,
            });
            return CreatedAtAction(nameof(Get), new { id = response.Id }, response);
        }
        catch (AcceptanceRecordOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [RequirePermission("construction.acceptance", "manage")]
    public async Task<ActionResult<AcceptanceRecordResponse>> Update(
        int id, [FromBody] UpdateAcceptanceRecordRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.UpdateAsync(id, request, userId.Value, ct);
            if (response is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "acceptance-record.update",
                ResourceType = EntityTypes.AcceptanceRecord,
                ResourceId = id.ToString(),
                Message = $"Acceptance record #{id} updated.",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (AcceptanceRecordOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/status")]
    [RequirePermission("construction.acceptance", "manage")]
    public async Task<ActionResult<AcceptanceRecordResponse>> Transition(
        int id, [FromBody] TransitionAcceptanceStatusRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.TransitionAsync(id, request, userId.Value, ct);
            if (response is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = $"acceptance-record.status.{response.Status.ToLowerInvariant()}",
                ResourceType = EntityTypes.AcceptanceRecord,
                ResourceId = id.ToString(),
                Message = $"Acceptance record #{id} -> {response.Status}.",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (AcceptanceRecordOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/approve")]
    [RequirePermission("construction.acceptance", "approve")]
    public async Task<ActionResult<AcceptanceRecordResponse>> Approve(
        int id, [FromBody] TransitionAcceptanceStatusRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.ApproveAsync(id, request, userId.Value, ct);
            if (response is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "acceptance-record.approve",
                ResourceType = EntityTypes.AcceptanceRecord,
                ResourceId = id.ToString(),
                Message = $"Acceptance record #{id} approved.",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (AcceptanceRecordOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("construction.acceptance", "manage")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            var removed = await svc.DeleteAsync(id, ct);
            if (!removed) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "acceptance-record.delete",
                ResourceType = EntityTypes.AcceptanceRecord,
                ResourceId = id.ToString(),
                Message = $"Acceptance record #{id} deleted.",
            });
            return NoContent();
        }
        catch (AcceptanceRecordOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("bulk-delete")]
    [RequirePermission("construction.acceptance", "manage")]
    public async Task<ActionResult<AcceptanceRecordBulkDeleteResponse>> BulkDelete(
        [FromBody] BulkDeleteAcceptanceRecordsRequest request, CancellationToken ct)
    {
        try
        {
            var result = await svc.BulkDeleteAsync(request, ct);
            audit.Log(new AuditEvent
            {
                Action = "acceptance-record.bulk-delete",
                ResourceType = EntityTypes.AcceptanceRecord,
                Message = $"Acceptance bulk delete — deleted={result.DeletedIds.Count} skipped={result.SkippedIds.Count}.",
                NewValue = result,
            });
            return Ok(result);
        }
        catch (AcceptanceRecordOperationException ex)
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
