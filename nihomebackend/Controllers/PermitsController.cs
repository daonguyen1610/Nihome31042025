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
/// happens implicitly via <see cref="IDesignProjectService"/> while authorized
/// operators can also manage individual checklist rows.
/// </summary>
[ApiController]
[Route("api/permits")]
[Route("api/v1/permits")]
[Authorize]
public class PermitsController(
    IPermitChecklistService svc,
    IBusinessDocumentStorageService documentStorage,
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

    [HttpGet("{id:int}/documents/{fileName}/content")]
    [RequirePermission("permit.checklists", "view")]
    public async Task<IActionResult> GetDocumentContent(int id, string fileName, CancellationToken ct)
    {
        var permit = await svc.GetAsync(id, ct);
        if (permit is null || !new[] { permit.SubmittedFilePath, permit.IssuedFilePath }.Any(path => string.Equals(
            path,
            $"/files/business-documents/permits/{fileName}",
            StringComparison.Ordinal))) return NotFound();
        var content = documentStorage.GetContent(BusinessDocumentArea.Permits, fileName);
        return content is null
            ? NotFound()
            : PhysicalFile(content.FullPath, content.ContentType, content.OriginalFileName, enableRangeProcessing: true);
    }

    [HttpPost]
    [RequirePermission("permit.checklists", "manage")]
    public async Task<ActionResult<PermitChecklistItemResponse>> Create(
        [FromBody] CreatePermitChecklistItemRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.CreateAsync(request, userId.Value, ct);
            audit.Log(new AuditEvent
            {
                Action = "permit.create",
                ResourceType = EntityTypes.PermitChecklistItem,
                ResourceId = response.Id.ToString(),
                Message = $"Permit checklist item #{response.Id} created ({response.PermitTypeCode}).",
                NewValue = response,
            });
            return CreatedAtAction(nameof(Get), new { id = response.Id }, response);
        }
        catch (PermitChecklistDuplicateException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (PermitChecklistOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
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

    [HttpPost("{id:int}/documents/{kind}")]
    [RequirePermission("permit.checklists", "manage")]
    public async Task<ActionResult<PermitChecklistItemResponse>> UploadDocument(
        int id,
        PermitDocumentKind kind,
        [FromForm] IFormFile? file,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.UploadDocumentAsync(id, kind, file, userId.Value, ct);
            if (response is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "permit.upload-document",
                ResourceType = EntityTypes.PermitChecklistItem,
                ResourceId = id.ToString(),
                Message = $"Permit checklist item #{id} {kind} document uploaded.",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (BusinessDocumentStorageException ex)
        {
            audit.Log(new AuditEvent
            {
                Action = "permit.upload-document",
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

    [HttpDelete("{id:int}")]
    [RequirePermission("permit.checklists", "manage")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var response = await svc.DeleteAsync(id, ct);
        if (response is null) return NotFound();

        audit.Log(new AuditEvent
        {
            Action = "permit.delete",
            ResourceType = EntityTypes.PermitChecklistItem,
            ResourceId = id.ToString(),
            Message = $"Permit checklist item #{id} deleted ({response.PermitTypeCode}).",
            OldValue = response,
        });
        return NoContent();
    }

    private int? GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return int.TryParse(raw, out var id) ? id : null;
    }
}
