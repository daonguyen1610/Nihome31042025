using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Constants;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;
using NihomeBackend.Services.Audit;
using NihomeBackend.Services.HardDelete;

namespace NihomeBackend.Controllers;

/// <summary>
/// CRM Tender (Gói thầu) endpoints — list / detail / CRUD plus the
/// NIH-97 detail-page workflow (checklist inline-edit, capability-doc
/// library attach, Mark Won / Mark Lost, timeline).
/// </summary>
[ApiController]
[Route("api/tenders")]
[Route("api/v1/tenders")]
[Authorize]
public class TendersController(
    ITenderService svc,
    IWebHostEnvironment env,
    IAuditLogger audit) : ControllerBase
{
    // Upload limits mirror ContractsController — 20 MB, same extension
    // whitelist. Physical files land under wwwroot/files/tenders and are
    // served only through the resource-bound content endpoint below.
    private const long MaxFileSizeBytes = 20L * 1024 * 1024;
    private const string StorageSubfolder = "tenders";

    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".png", ".jpg", ".jpeg",
        };

    [HttpGet]
    [RequirePermission("crm.tenders", "view")]
    public async Task<ActionResult<TenderListResponse>> List([FromQuery] TenderListParams parameters, CancellationToken ct)
    {
        var result = await svc.ListAsync(parameters, ct);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [RequirePermission("crm.tenders", "view")]
    public async Task<ActionResult<TenderResponse>> Get(int id, CancellationToken ct)
    {
        var found = await svc.GetAsync(id, ct);
        return found is null ? NotFound() : Ok(found);
    }

    [HttpPost]
    [RequirePermission("crm.tenders", "manage")]
    public async Task<ActionResult<TenderResponse>> Create([FromBody] CreateTenderRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.CreateAsync(request, userId.Value, ct);
            audit.Log(new AuditEvent
            {
                Action = "tender.create",
                ResourceType = EntityTypes.Tender,
                ResourceId = response.Id.ToString(),
                Message = $"Tender #{response.Id} ({response.Code}) created.",
                NewValue = response,
            });
            return CreatedAtAction(nameof(Get), new { id = response.Id }, response);
        }
        catch (TenderOperationException ex)
        {
            return LogAndBadRequest("tender.create", ex);
        }
    }

    [HttpPut("{id:int}")]
    [RequirePermission("crm.tenders", "manage")]
    public async Task<ActionResult<TenderResponse>> Update(int id, [FromBody] UpdateTenderRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.UpdateAsync(id, request, userId.Value, ct);
            if (response is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "tender.update",
                ResourceType = EntityTypes.Tender,
                ResourceId = id.ToString(),
                Message = $"Tender #{id} updated (status={response.Status}).",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (TenderOperationException ex)
        {
            return LogAndBadRequest("tender.update", ex, id);
        }
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("crm.tenders", "manage")]
    public async Task<IActionResult> Delete(
        int id,
        [FromBody] ConfirmDeletionRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var result = await svc.DeleteAsync(id, request, userId.Value, ct);
            if (result is null) return NotFound();
            return result.IsComplete ? NoContent() : AcceptedOperation(result);
        }
        catch (TenderOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (DeletionPlanChangedException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (HardDeleteOperationConflictException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("{id:int}/deletion-impact")]
    [RequirePermission("crm.tenders", "manage")]
    public async Task<ActionResult<DeletionImpactResponse>> GetDeletionImpact(
        int id, CancellationToken ct)
    {
        var impact = await svc.GetDeletionImpactAsync(id, ct);
        return impact is null ? NotFound() : Ok(impact);
    }

    // ---------- helpers ----------

    private ActionResult<TenderResponse> LogAndBadRequest(string action, TenderOperationException ex, int? id = null)
    {
        audit.Log(new AuditEvent
        {
            Action = action,
            ResourceType = EntityTypes.Tender,
            ResourceId = id?.ToString(),
            Message = ex.Message,
            Status = AuditStatus.Failure,
            FailureReason = ex.Message,
        });
        return BadRequest(new { message = ex.Message });
    }

    private IActionResult AcceptedOperation(HardDeleteOperationResult result)
    {
        Response.Headers.Location = Url.Action(
            nameof(HardDeleteOperationsController.GetStatus),
            "HardDeleteOperations",
            new { operationId = result.OperationId })!;
        return StatusCode(StatusCodes.Status202Accepted, result);
    }

    // -------- NIH-97 detail-page workflow --------

    [HttpPatch("{id:int}/checklist/{itemId:int}")]
    [RequirePermission("crm.tenders", "manage")]
    public async Task<ActionResult<TenderResponse>> UpdateChecklistItem(
        int id, int itemId, [FromBody] UpdateTenderChecklistItemRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var updated = await svc.UpdateChecklistItemAsync(id, itemId, request, userId.Value, ct);
            if (updated is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "tender.checklist.update",
                ResourceType = EntityTypes.Tender,
                ResourceId = id.ToString(),
                Message = $"Tender #{id} checklist item #{itemId} updated.",
            });
            return Ok(updated);
        }
        catch (TenderOperationException ex)
        {
            return LogAndBadRequest("tender.checklist.update", ex, id);
        }
    }

    [HttpGet("{id:int}/checklist/{itemId:int}/content")]
    [RequirePermission("crm.tenders", "view")]
    public async Task<IActionResult> GetChecklistFileContent(
        int id, int itemId, CancellationToken ct)
    {
        var file = await svc.GetChecklistFileAsync(id, itemId, ct);
        if (file is null) return NotFound();

        var fullPath = ResolveManagedChecklistFile(file.FilePath);
        if (fullPath is null || !System.IO.File.Exists(fullPath)) return NotFound();

        return PhysicalFile(
            fullPath,
            GetContentType(Path.GetExtension(fullPath)),
            file.OriginalFileName,
            enableRangeProcessing: true);
    }

    [HttpPost("{id:int}/checklist/{itemId:int}/upload")]
    [Consumes("multipart/form-data")]
    [RequirePermission("crm.tenders", "manage")]
    public async Task<ActionResult<TenderResponse>> UploadChecklistFile(
        int id, int itemId, [FromForm] IFormFile? file, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var stored = await StoreUploadedFileAsync(file, "cl", ct);
        if (stored.Result is BadRequestObjectResult bad) return bad;
        if (stored.Payload is null) return BadRequest(new { message = "Không upload được file." });

        try
        {
            var updated = await svc.AttachChecklistFileAsync(id, itemId,
                stored.Payload.filePath, stored.Payload.originalFileName, userId.Value, ct);
            if (updated is null)
            {
                DeleteStoredFile(stored.Payload.fullPath);
                return NotFound();
            }

            audit.Log(new AuditEvent
            {
                Action = "tender.checklist.upload",
                ResourceType = EntityTypes.Tender,
                ResourceId = id.ToString(),
                Message = $"Tender #{id} checklist #{itemId} — {stored.Payload.originalFileName}",
            });
            return Ok(updated);
        }
        catch (TenderOperationException ex)
        {
            DeleteStoredFile(stored.Payload.fullPath);
            return LogAndBadRequest("tender.checklist.upload", ex, id);
        }
        catch
        {
            try
            {
                var attached = await svc.IsChecklistFileAttachedAsync(
                    id, itemId, stored.Payload.filePath, CancellationToken.None);
                if (!attached) DeleteStoredFile(stored.Payload.fullPath);
            }
            catch
            {
            }
            throw;
        }
    }

    [HttpPost("{id:int}/checklist/attach-from-library")]
    [RequirePermission("crm.tenders", "manage")]
    public async Task<ActionResult<TenderResponse>> AttachChecklistFromLibrary(
        int id, [FromBody] AttachTenderChecklistFromLibraryRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var updated = await svc.AttachChecklistFromLibraryAsync(id, request, userId.Value, ct);
            if (updated is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "tender.checklist.attach-library",
                ResourceType = EntityTypes.Tender,
                ResourceId = id.ToString(),
                Message = $"Tender #{id} attached {request.Items.Count} library documents.",
            });
            return Ok(updated);
        }
        catch (TenderOperationException ex)
        {
            return LogAndBadRequest("tender.checklist.attach-library", ex, id);
        }
    }

    [HttpPost("{id:int}/mark-won")]
    [RequirePermission("crm.tenders", "mark-result")]
    public async Task<ActionResult<TenderResponse>> MarkWon(
        int id, [FromBody] MarkTenderWonRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var updated = await svc.MarkWonAsync(id, request, userId.Value, ct);
            if (updated is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "tender.mark-won",
                ResourceType = EntityTypes.Tender,
                ResourceId = id.ToString(),
                Message = $"Tender #{id} → Won (opportunity #{request.OpportunityId}).",
            });
            return Ok(updated);
        }
        catch (TenderOperationException ex)
        {
            return LogAndBadRequest("tender.mark-won", ex, id);
        }
    }

    [HttpPost("{id:int}/mark-lost")]
    [RequirePermission("crm.tenders", "mark-result")]
    public async Task<ActionResult<TenderResponse>> MarkLost(
        int id, [FromBody] MarkTenderLostRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var updated = await svc.MarkLostAsync(id, request, userId.Value, ct);
            if (updated is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "tender.mark-lost",
                ResourceType = EntityTypes.Tender,
                ResourceId = id.ToString(),
                Message = $"Tender #{id} → Lost ({request.ReasonCode}).",
            });
            return Ok(updated);
        }
        catch (TenderOperationException ex)
        {
            return LogAndBadRequest("tender.mark-lost", ex, id);
        }
    }

    [HttpPost("{id:int}/transition")]
    [RequirePermission("crm.tenders", "mark-result")]
    public async Task<ActionResult<TenderResponse>> Transition(
        int id, [FromBody] TransitionTenderRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var updated = await svc.TransitionAsync(id, request, userId.Value, ct);
            if (updated is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "tender.transition",
                ResourceType = EntityTypes.Tender,
                ResourceId = id.ToString(),
                Message = $"Tender #{id} → {updated.Status}.",
                NewValue = updated,
            });
            return Ok(updated);
        }
        catch (TenderOperationException ex)
        {
            return LogAndBadRequest("tender.transition", ex, id);
        }
    }

    [HttpGet("{id:int}/timeline")]
    [RequirePermission("crm.tenders", "view")]
    public async Task<ActionResult<List<TenderTimelineEvent>>> Timeline(
        int id, [FromQuery] int limit = 100, CancellationToken ct = default)
    {
        var events = await svc.GetTimelineAsync(id, limit, ct);
        return events is null ? NotFound() : Ok(events);
    }

    // -------- upload helpers --------

    private sealed record UploadedPayload(
        string filePath,
        string originalFileName,
        long fileSize,
        string contentType,
        string fullPath);

    private struct StoredFile
    {
        public UploadedPayload? Payload;
        public IActionResult? Result;
    }

    private async Task<StoredFile> StoreUploadedFileAsync(IFormFile? file, string kindTag, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return new StoredFile { Result = BadRequest(new { message = "Chưa chọn file." }) };
        }
        if (file.Length > MaxFileSizeBytes)
        {
            return new StoredFile { Result = BadRequest(new { message = "File quá lớn (tối đa 20MB)." }) };
        }
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            return new StoredFile
            {
                Result = BadRequest(new { message = "Định dạng file không được hỗ trợ (PDF/DOC/DOCX/XLS/XLSX/PNG/JPG)." }),
            };
        }

        var uploadDir = Path.Combine(env.ContentRootPath, "wwwroot", "files", StorageSubfolder);
        Directory.CreateDirectory(uploadDir);
        var storedName = $"{kindTag}-{Guid.NewGuid():N}{extension}";
        var storedPath = Path.Combine(uploadDir, storedName);
        try
        {
            await using var stream = new FileStream(storedPath, FileMode.CreateNew);
            await file.CopyToAsync(stream, ct);
        }
        catch
        {
            DeleteStoredFile(storedPath);
            throw;
        }
        return new StoredFile
        {
            Payload = new UploadedPayload(
                $"/files/{StorageSubfolder}/{storedName}",
                file.FileName,
                file.Length,
                string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                storedPath),
        };
    }

    private string? ResolveManagedChecklistFile(string filePath)
    {
        string? subfolder = filePath switch
        {
            var path when path.StartsWith("/files/tenders/", StringComparison.Ordinal) => "tenders",
            var path when path.StartsWith("/files/capability/", StringComparison.Ordinal) => "capability",
            _ => null,
        };
        if (subfolder is null) return null;

        var fileName = Path.GetFileName(filePath);
        if (string.IsNullOrWhiteSpace(fileName)
            || !string.Equals(filePath, $"/files/{subfolder}/{fileName}", StringComparison.Ordinal))
        {
            return null;
        }

        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension)) return null;
        return Path.Combine(env.ContentRootPath, "wwwroot", "files", subfolder, fileName);
    }

    private static void DeleteStoredFile(string fullPath)
    {
        try
        {
            if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string GetContentType(string extension) => extension.ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xls" => "application/vnd.ms-excel",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        _ => "application/octet-stream",
    };

    private int? GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return int.TryParse(raw, out var id) ? id : null;
    }
}
