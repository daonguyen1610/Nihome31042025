using System.Security.Claims;
using System.Text.Json;
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
    IPermissionService permissions,
    IBusinessDocumentStorageService documentStorage,
    IAuditLogger audit,
    INotificationService notifications) : ControllerBase
{
    [HttpGet]
    [RequirePermission("construction.acceptance", "view")]
    public async Task<ActionResult<AcceptanceRecordListResponse>> List(
        [FromQuery] AcceptanceRecordListParams parameters, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var canSeeAll = await CanSeeAllAsync(userId.Value, ct);
        var result = await svc.ListAsync(parameters, userId.Value, canSeeAll, ct);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [RequirePermission("construction.acceptance", "view")]
    public async Task<ActionResult<AcceptanceRecordResponse>> Get(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var found = await svc.GetAsync(id, userId.Value, await CanSeeAllAsync(userId.Value, ct), ct);
        return found is null ? NotFound() : Ok(found);
    }

    [HttpGet("{id:int}/documents/{fileName}/content")]
    [RequirePermission("construction.acceptance", "view")]
    public async Task<IActionResult> GetDocumentContent(int id, string fileName, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var record = await svc.GetAsync(id, userId.Value, await CanSeeAllAsync(userId.Value, ct), ct);
        if (record is null || !ReferencesFile(record.Documents, fileName)) return NotFound();
        var content = documentStorage.GetContent(BusinessDocumentArea.Acceptance, fileName);
        return content is null
            ? NotFound()
            : PhysicalFile(content.FullPath, content.ContentType, content.OriginalFileName, enableRangeProcessing: true);
    }

    private static bool ReferencesFile(string? documents, string fileName)
    {
        if (string.IsNullOrWhiteSpace(documents)) return false;
        try
        {
            return (JsonSerializer.Deserialize<List<string>>(documents) ?? [])
                .Any(path => string.Equals(
                    path,
                    $"/files/business-documents/acceptance/{fileName}",
                    StringComparison.Ordinal));
        }
        catch (JsonException)
        {
            return false;
        }
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
            var response = await svc.CreateAsync(request, userId.Value, await CanSeeAllAsync(userId.Value, ct), ct);
            audit.Log(new AuditEvent
            {
                Action = "acceptance-record.create",
                ResourceType = EntityTypes.AcceptanceRecord,
                ResourceId = response.Id.ToString(),
                Message = $"Acceptance record #{response.Id} ({response.AcceptanceCode}) created on project {response.DesignProjectId}.",
                NewValue = response,
            });
            await NotifyAdminsBestEffortAsync(
                $"Biên bản nghiệm thu mới: {response.AcceptanceCode}", response.Title);
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
            var canSeeAll = await CanSeeAllAsync(userId.Value, ct);
            var previous = await svc.GetAsync(id, userId.Value, canSeeAll, ct);
            var response = await svc.UpdateAsync(id, request, userId.Value, canSeeAll, ct);
            if (response is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "acceptance-record.update",
                ResourceType = EntityTypes.AcceptanceRecord,
                ResourceId = id.ToString(),
                Message = $"Acceptance record #{id} updated.",
                OldValue = previous,
                NewValue = response,
            });
            if (response.CreatedByUserId.HasValue && response.CreatedByUserId.Value != userId.Value)
                await NotifyUserBestEffortAsync(response.CreatedByUserId.Value,
                    $"Biên bản nghiệm thu đã cập nhật: {response.AcceptanceCode}", response.Title);
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
            var canSeeAll = await CanSeeAllAsync(userId.Value, ct);
            var previous = await svc.GetAsync(id, userId.Value, canSeeAll, ct);
            var response = await svc.TransitionAsync(id, request, userId.Value, canSeeAll, ct);
            if (response is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = $"acceptance-record.status.{response.Status.ToLowerInvariant()}",
                ResourceType = EntityTypes.AcceptanceRecord,
                ResourceId = id.ToString(),
                Message = $"Acceptance record #{id} -> {response.Status}.",
                OldValue = previous,
                NewValue = response,
            });
            if (response.Status == "Submitted")
                await NotifyAdminsBestEffortAsync(
                    $"Biên bản nghiệm thu chờ duyệt: {response.AcceptanceCode}", response.Title);
            else if (response.CreatedByUserId.HasValue && response.CreatedByUserId.Value != userId.Value)
                await NotifyUserBestEffortAsync(response.CreatedByUserId.Value,
                    $"Biên bản nghiệm thu chuyển sang {response.Status}: {response.AcceptanceCode}", response.Title);
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
            var canSeeAll = await CanSeeAllAsync(userId.Value, ct);
            var previous = await svc.GetAsync(id, userId.Value, canSeeAll, ct);
            var response = await svc.ApproveAsync(id, request, userId.Value, canSeeAll, ct);
            if (response is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "acceptance-record.approve",
                ResourceType = EntityTypes.AcceptanceRecord,
                ResourceId = id.ToString(),
                Message = $"Acceptance record #{id} approved.",
                OldValue = previous,
                NewValue = response,
            });
            var recipientId = response.SubmittedByUserId ?? response.CreatedByUserId;
            if (recipientId.HasValue && recipientId.Value != userId.Value)
                await NotifyUserBestEffortAsync(recipientId.Value,
                    $"Biên bản nghiệm thu đã được duyệt: {response.AcceptanceCode}", response.Title);
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
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var canSeeAll = await CanSeeAllAsync(userId.Value, ct);
            var previous = await svc.GetAsync(id, userId.Value, canSeeAll, ct);
            var removed = await svc.DeleteAsync(id, userId.Value, canSeeAll, ct);
            if (!removed) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "acceptance-record.delete",
                ResourceType = EntityTypes.AcceptanceRecord,
                ResourceId = id.ToString(),
                Message = $"Acceptance record #{id} deleted.",
                OldValue = previous,
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
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var result = await svc.BulkDeleteAsync(
                request, userId.Value, await CanSeeAllAsync(userId.Value, ct), ct);
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

    private Task<bool> CanSeeAllAsync(int userId, CancellationToken ct) =>
        permissions.HasAsync(userId, "construction.acceptance.view.all", ct);

    private async Task NotifyAdminsBestEffortAsync(string title, string body)
    {
        try
        {
            await notifications.CreateForAdminsAsync(
                "AcceptanceRecord", title, body, "/admin/construction/acceptance");
        }
        catch
        {
        }
    }

    private async Task NotifyUserBestEffortAsync(int userId, string title, string body)
    {
        try
        {
            await notifications.CreateAsync(
                userId, "AcceptanceRecord", title, body, "/admin/construction/acceptance");
        }
        catch
        {
        }
    }
}
