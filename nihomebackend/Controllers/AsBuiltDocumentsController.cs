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
    IBusinessDocumentStorageService documentStorage,
    IAuditLogger audit,
    INotificationService notifications) : ControllerBase
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

    [HttpGet("{id:int}/content")]
    [RequirePermission("construction.asbuilt", "view")]
    public async Task<IActionResult> GetContent(int id, CancellationToken ct)
    {
        var document = await svc.GetAsync(id, ct);
        if (document?.FileUrl is null) return NotFound();
        var fileName = Path.GetFileName(document.FileUrl);
        if (!string.Equals(
            document.FileUrl,
            $"/files/business-documents/as-built/{fileName}",
            StringComparison.Ordinal)) return NotFound();
        var content = documentStorage.GetContent(BusinessDocumentArea.AsBuilt, fileName);
        return content is null
            ? NotFound()
            : PhysicalFile(content.FullPath, content.ContentType, content.OriginalFileName, enableRangeProcessing: true);
    }

    [HttpGet("export")]
    [RequirePermission("construction.asbuilt", "view")]
    public async Task<IActionResult> Export(
        [FromQuery] AsBuiltDocumentListParams parameters,
        CancellationToken ct)
    {
        var rows = await svc.ExportAsync(parameters, ct);
        audit.Log(new AuditEvent
        {
            Action = "as-built-document.export",
            ResourceType = EntityTypes.AsBuiltDocument,
            Message = $"Exported {rows.Count} as-built documents.",
        });

        var csv = BuildCsv(rows);
        var content = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
        return File(content, "text/csv; charset=utf-8", $"as-built-documents-{DateTime.UtcNow:yyyy-MM-dd}.csv");
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
            await NotifyAdminsBestEffortAsync(
                $"Hồ sơ hoàn công mới: {response.DocumentCode}",
                response.Title);
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
            if (response.CreatedByUserId.HasValue && response.CreatedByUserId.Value != userId.Value)
            {
                await NotifyUserBestEffortAsync(
                    response.CreatedByUserId.Value,
                    $"Hồ sơ hoàn công đã được cập nhật: {response.DocumentCode}",
                    response.Title);
            }
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
            if (response.Status == "Submitted")
            {
                await NotifyAdminsBestEffortAsync(
                    $"Hồ sơ hoàn công chờ duyệt: {response.DocumentCode}",
                    response.Title);
            }
            else if (response.CreatedByUserId.HasValue && response.CreatedByUserId.Value != userId.Value)
            {
                await NotifyUserBestEffortAsync(
                    response.CreatedByUserId.Value,
                    $"Hồ sơ hoàn công đã chuyển sang {response.Status}: {response.DocumentCode}",
                    response.Title);
            }
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
            var recipientId = response.SubmittedByUserId ?? response.CreatedByUserId;
            if (recipientId.HasValue && recipientId.Value != userId.Value)
            {
                await NotifyUserBestEffortAsync(
                    recipientId.Value,
                    $"Hồ sơ hoàn công đã được duyệt: {response.DocumentCode}",
                    response.Title);
            }
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

    private async Task NotifyAdminsBestEffortAsync(string title, string body)
    {
        try
        {
            await notifications.CreateForAdminsAsync(
                "AsBuiltDocument", title, body, "/admin/construction/asbuilt");
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
                userId, "AsBuiltDocument", title, body, "/admin/construction/asbuilt");
        }
        catch
        {
        }
    }

    private static string BuildCsv(IEnumerable<AsBuiltDocumentResponse> rows)
    {
        var csv = new StringBuilder();
        csv.AppendLine("Code,Title,Category,Project,Status,Submitted By,Submitted At,Approved By,Approved At,Updated At,File URL");
        foreach (var row in rows)
        {
            csv.AppendLine(string.Join(',', new[]
            {
                CsvCell(row.DocumentCode),
                CsvCell(row.Title),
                CsvCell(row.Category),
                CsvCell(row.DesignProjectName),
                CsvCell(row.Status),
                CsvCell(row.SubmittedByName),
                CsvCell(row.SubmittedAt?.ToString("O")),
                CsvCell(row.ApprovedByName),
                CsvCell(row.ApprovedAt?.ToString("O")),
                CsvCell(row.UpdatedAt.ToString("O")),
                CsvCell(row.FileUrl),
            }));
        }
        return csv.ToString();
    }

    private static string CsvCell(string? value)
    {
        var safeValue = value ?? string.Empty;
        if (safeValue.Length > 0 && "=+-@".Contains(safeValue[0])) safeValue = $"'{safeValue}";
        return $"\"{safeValue.Replace("\"", "\"\"")}\"";
    }
}
