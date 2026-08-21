using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Constants;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;
using NihomeBackend.Services.Audit;

namespace NihomeBackend.Controllers;

[ApiController]
[Route("api/handover-records")]
[Route("api/v1/handover-records")]
[Authorize]
public class HandoverRecordsController(
    IHandoverRecordService service,
    IPermissionService permissions,
    IBusinessDocumentStorageService documentStorage,
    IAuditLogger audit,
    INotificationService notifications) : ControllerBase
{
    [HttpGet]
    [RequirePermission("construction.handover", "view")]
    public async Task<ActionResult<HandoverRecordListResponse>> List(
        [FromQuery] HandoverRecordListParams parameters, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        return Ok(await service.ListAsync(parameters, userId.Value, await CanSeeAllAsync(userId.Value, ct), ct));
    }

    [HttpGet("export")]
    [RequirePermission("construction.handover", "view")]
    public async Task<IActionResult> Export(
        [FromQuery] HandoverRecordListParams parameters, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var rows = await service.ExportAsync(parameters, userId.Value, await CanSeeAllAsync(userId.Value, ct), ct);
        audit.Log(new AuditEvent
        {
            Action = "handover-record.export",
            ResourceType = EntityTypes.HandoverRecord,
            Message = $"Exported {rows.Count} handover records.",
        });
        var csv = BuildCsv(rows);
        var content = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
        return File(content, "text/csv; charset=utf-8", $"handover-records-{DateTime.UtcNow:yyyy-MM-dd}.csv");
    }

    [HttpGet("{id:int}")]
    [RequirePermission("construction.handover", "view")]
    public async Task<ActionResult<HandoverRecordResponse>> Get(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var response = await service.GetAsync(id, userId.Value, await CanSeeAllAsync(userId.Value, ct), ct);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpGet("{id:int}/documents/{fileName}/content")]
    [RequirePermission("construction.handover", "view")]
    public async Task<IActionResult> GetDocumentContent(int id, string fileName, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var record = await service.GetAsync(id, userId.Value, await CanSeeAllAsync(userId.Value, ct), ct);
        if (record is null || !record.Documents.Any(path => string.Equals(
            path,
            $"/files/business-documents/handover/{fileName}",
            StringComparison.Ordinal))) return NotFound();
        var content = documentStorage.GetContent(BusinessDocumentArea.Handover, fileName);
        return content is null
            ? NotFound()
            : PhysicalFile(content.FullPath, content.ContentType, content.OriginalFileName, enableRangeProcessing: true);
    }

    [HttpPost]
    [RequirePermission("construction.handover", "manage")]
    public async Task<ActionResult<HandoverRecordResponse>> Create(
        [FromBody] CreateHandoverRecordRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await service.CreateAsync(request, userId.Value, await CanManageAllAsync(userId.Value, ct), ct);
            LogChange("handover-record.create", response.Id, "Handover record created.", null, response);
            await NotifyAdminsBestEffortAsync($"Hồ sơ bàn giao mới: {response.HandoverCode}", response.Title);
            return CreatedAtAction(nameof(Get), new { id = response.Id }, response);
        }
        catch (HandoverRecordOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (HandoverRecordConflictException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpPut("{id:int}")]
    [RequirePermission("construction.handover", "manage")]
    public async Task<ActionResult<HandoverRecordResponse>> Update(
        int id, [FromBody] UpdateHandoverRecordRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var canManageAll = await CanManageAllAsync(userId.Value, ct);
            var previous = await service.GetAsync(id, userId.Value, canManageAll, ct);
            var response = await service.UpdateAsync(id, request, userId.Value, canManageAll, ct);
            if (response is null) return NotFound();
            LogChange("handover-record.update", id, "Handover record updated.", previous, response);
            return Ok(response);
        }
        catch (HandoverRecordOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (HandoverRecordConflictException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpPost("{id:int}/status")]
    [RequirePermission("construction.handover", "manage")]
    public async Task<ActionResult<HandoverRecordResponse>> Transition(
        int id, [FromBody] TransitionHandoverStatusRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var canManageAll = await CanManageAllAsync(userId.Value, ct);
            var previous = await service.GetAsync(id, userId.Value, canManageAll, ct);
            var response = await service.TransitionAsync(id, request, userId.Value, canManageAll, ct);
            if (response is null) return NotFound();
            LogChange($"handover-record.status.{response.Status.ToLowerInvariant()}", id,
                $"Handover record -> {response.Status}.", previous, response);
            if (response.Status == "ReadyForHandover")
                await NotifyAdminsBestEffortAsync($"Hồ sơ bàn giao sẵn sàng: {response.HandoverCode}", response.Title);
            else
                await NotifyResponsibleBestEffortAsync(response, userId.Value);
            return Ok(response);
        }
        catch (HandoverRecordOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (HandoverRecordConflictException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpPost("{id:int}/complete")]
    [RequirePermission("construction.handover", "complete")]
    public async Task<ActionResult<HandoverRecordResponse>> Complete(
        int id, [FromBody] TransitionHandoverStatusRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var canManageAll = await CanManageAllAsync(userId.Value, ct);
            var previous = await service.GetAsync(id, userId.Value, canManageAll, ct);
            var response = await service.CompleteAsync(id, request, userId.Value, canManageAll, ct);
            if (response is null) return NotFound();
            LogChange("handover-record.complete", id, "Project handover completed.", previous, response);
            await NotifyResponsibleBestEffortAsync(response, userId.Value);
            return Ok(response);
        }
        catch (HandoverRecordOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (HandoverRecordConflictException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("construction.handover", "manage")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var canManageAll = await CanManageAllAsync(userId.Value, ct);
            var previous = await service.GetAsync(id, userId.Value, canManageAll, ct);
            if (!await service.DeleteAsync(id, userId.Value, canManageAll, ct)) return NotFound();
            LogChange("handover-record.delete", id, "Handover record deleted.", previous, null);
            return NoContent();
        }
        catch (HandoverRecordOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (HandoverRecordConflictException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    private int? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return int.TryParse(value, out var userId) ? userId : null;
    }

    private Task<bool> CanSeeAllAsync(int userId, CancellationToken ct) =>
        permissions.HasAsync(userId, "construction.handover.view.all", ct);

    private Task<bool> CanManageAllAsync(int userId, CancellationToken ct) =>
        permissions.HasAsync(userId, "construction.handover.manage.all", ct);

    private void LogChange(string action, int id, string message, object? oldValue, object? newValue) =>
        audit.Log(new AuditEvent
        {
            Action = action,
            ResourceType = EntityTypes.HandoverRecord,
            ResourceId = id.ToString(),
            Message = message,
            OldValue = oldValue,
            NewValue = newValue,
        });

    private async Task NotifyAdminsBestEffortAsync(string title, string body)
    {
        try
        {
            await notifications.CreateForAdminsAsync("HandoverRecord", title, body, "/admin/construction/handover");
        }
        catch
        {
        }
    }

    private async Task NotifyResponsibleBestEffortAsync(HandoverRecordResponse response, int actorId)
    {
        if (response.ResponsibleUserId == actorId) return;
        try
        {
            await notifications.CreateAsync(response.ResponsibleUserId, "HandoverRecord",
                $"Hồ sơ bàn giao chuyển sang {response.Status}: {response.HandoverCode}",
                response.Title, "/admin/construction/handover");
        }
        catch
        {
        }
    }

    private static string BuildCsv(IEnumerable<HandoverRecordResponse> rows)
    {
        static string Escape(string? value)
        {
            var safe = value ?? string.Empty;
            if (safe.Length > 0 && (safe[0] is '=' or '+' or '-' or '@' or '\t' or '\r')) safe = $"'{safe}";
            return $"\"{safe.Replace("\"", "\"\"")}\"";
        }
        var builder = new StringBuilder();
        builder.AppendLine("Code,Project,Title,Planned date,Actual date,Responsible,Status,Ready,Open punch items");
        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(',',
                Escape(row.HandoverCode), Escape(row.DesignProjectName), Escape(row.Title),
                Escape(row.PlannedHandoverDate.ToString("yyyy-MM-dd")),
                Escape(row.ActualHandoverDate?.ToString("yyyy-MM-dd")),
                Escape(row.ResponsibleUserName), Escape(row.Status), Escape(row.Readiness.IsReady.ToString()),
                row.Readiness.UnresolvedPunchItems));
        }
        return builder.ToString();
    }
}