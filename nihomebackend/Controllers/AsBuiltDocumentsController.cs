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
/// M4 as-built dossier (Hồ sơ Hoàn công / NIH-145) endpoints. Guarded
/// by <c>construction.asbuilt.view</c> / <c>construction.asbuilt.manage</c>
/// for CRUD + non-approving transitions, and by
/// <c>construction.asbuilt.approve</c> for the dedicated <c>/approve</c>
/// action.
/// </summary>
[ApiController]
[Route("api/as-built-documents")]
[Route("api/v1/as-built-documents")]
[Authorize]
public class AsBuiltDocumentsController(
    IAsBuiltDocumentService svc,
    IAuditLogger audit) : ControllerBase
{
    [HttpGet]
    [RequirePermission("construction.asbuilt", "view")]
    public async Task<ActionResult<AsBuiltDocumentListResponse>> List(
        [FromQuery] AsBuiltDocumentListParams parameters, CancellationToken ct)
    {
        return Ok(await svc.ListAsync(parameters, ct));
    }

    [HttpGet("{id:int}")]
    [RequirePermission("construction.asbuilt", "view")]
    public async Task<ActionResult<AsBuiltDocumentResponse>> Get(int id, CancellationToken ct)
    {
        var found = await svc.GetAsync(id, ct);
        return found is null ? NotFound() : Ok(found);
    }

    [HttpPost]
    [RequirePermission("construction.asbuilt", "manage")]
    public async Task<ActionResult<AsBuiltDocumentResponse>> Create(
        [FromBody] CreateAsBuiltDocumentRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.CreateAsync(request, userId.Value, ct);
            audit.Log(new AuditEvent
            {
                Action = "as-built-document.create",
                ResourceType = EntityTypes.AsBuiltDocument,
                ResourceId = response.Id.ToString(),
                Message = $"As-built document #{response.Id} ({response.DocumentCode}) created on project {response.DesignProjectId}.",
                NewValue = response,
            });
            return CreatedAtAction(nameof(Get), new { id = response.Id }, response);
        }
        catch (AsBuiltDocumentOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [RequirePermission("construction.asbuilt", "manage")]
    public async Task<ActionResult<AsBuiltDocumentResponse>> Update(
        int id, [FromBody] UpdateAsBuiltDocumentRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.UpdateAsync(id, request, userId.Value, ct);
            if (response is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "as-built-document.update",
                ResourceType = EntityTypes.AsBuiltDocument,
                ResourceId = id.ToString(),
                Message = $"As-built document #{id} updated.",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (AsBuiltDocumentOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/status")]
    [RequirePermission("construction.asbuilt", "manage")]
    public async Task<ActionResult<AsBuiltDocumentResponse>> Transition(
        int id, [FromBody] TransitionAsBuiltStatusRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.TransitionAsync(id, request, userId.Value, ct);
            if (response is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = $"as-built-document.status.{response.Status.ToLowerInvariant()}",
                ResourceType = EntityTypes.AsBuiltDocument,
                ResourceId = id.ToString(),
                Message = $"As-built document #{id} -> {response.Status}.",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (AsBuiltDocumentOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/approve")]
    [RequirePermission("construction.asbuilt", "approve")]
    public async Task<ActionResult<AsBuiltDocumentResponse>> Approve(
        int id, [FromBody] TransitionAsBuiltStatusRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.ApproveAsync(id, request, userId.Value, ct);
            if (response is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "as-built-document.approve",
                ResourceType = EntityTypes.AsBuiltDocument,
                ResourceId = id.ToString(),
                Message = $"As-built document #{id} approved.",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (AsBuiltDocumentOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("construction.asbuilt", "manage")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            var removed = await svc.DeleteAsync(id, ct);
            if (!removed) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "as-built-document.delete",
                ResourceType = EntityTypes.AsBuiltDocument,
                ResourceId = id.ToString(),
                Message = $"As-built document #{id} deleted.",
            });
            return NoContent();
        }
        catch (AsBuiltDocumentOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("bulk-delete")]
    [RequirePermission("construction.asbuilt", "manage")]
    public async Task<ActionResult<AsBuiltDocumentBulkDeleteResponse>> BulkDelete(
        [FromBody] BulkDeleteAsBuiltDocumentsRequest request, CancellationToken ct)
    {
        try
        {
            var result = await svc.BulkDeleteAsync(request, ct);
            audit.Log(new AuditEvent
            {
                Action = "as-built-document.bulk-delete",
                ResourceType = EntityTypes.AsBuiltDocument,
                Message = $"As-built bulk delete — deleted={result.DeletedIds.Count} skipped={result.SkippedIds.Count}.",
                NewValue = result,
            });
            return Ok(result);
        }
        catch (AsBuiltDocumentOperationException ex)
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
