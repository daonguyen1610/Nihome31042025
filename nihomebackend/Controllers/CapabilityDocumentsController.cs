using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Authorization;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;
using NihomeBackend.Services.Audit;
using NihomeBackend.Services.HardDelete;

namespace NihomeBackend.Controllers;

/// <summary>
/// Capability-document (hồ sơ năng lực) endpoints — shared repository of
/// legal/discipline documents that Sales pick from when preparing a
/// Tender. Read is gated on <c>crm.capability-docs.view</c>; all mutations
/// (upload, replace, delete) require <c>crm.capability-docs.manage</c>.
///
/// File upload uses <c>multipart/form-data</c> and stores physical assets
/// under <c>wwwroot/files/capability/{guid}.{ext}</c>; the returned path is
/// host-relative so the frontend can resolve it against the current API
/// origin (see repository memory image_handling_exploration.md).
/// </summary>
[ApiController]
[Route("api/capability-documents")]
[Route("api/v1/capability-documents")]
[Authorize]
public class CapabilityDocumentsController(
    ICapabilityDocumentService svc,
    IBusinessRootHardDeleteService hardDelete,
    AppDbContext db,
    IWebHostEnvironment env,
    IAuditLogger audit,
    ILogger<CapabilityDocumentsController> logger) : ControllerBase
{
    private const long MaxFileSizeBytes = 20L * 1024 * 1024; // 20MB per AC
    private const string StorageSubfolder = "capability";

    /// <summary>Allowed extensions per AC. Kept as a hash-set for O(1) lookup.</summary>
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".png", ".jpg", ".jpeg",
        };

    [HttpGet]
    [RequirePermission("crm.capability-docs", "view")]
    public async Task<ActionResult<CapabilityDocumentListResponse>> List(
        [FromQuery] string? tagCode,
        [FromQuery] int? issuedYear,
        [FromQuery] string? search,
        [FromQuery] string? expiryState,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await svc.ListAsync(tagCode, issuedYear, search, expiryState, page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [RequirePermission("crm.capability-docs", "view")]
    public async Task<ActionResult<CapabilityDocumentDetailResponse>> Get(int id, CancellationToken ct)
    {
        var found = await svc.GetAsync(id, ct);
        return found is null ? NotFound() : Ok(found);
    }

    [HttpGet("files/{fileName}/content")]
    [RequirePermission("crm.capability-docs", "view")]
    public async Task<IActionResult> GetFileContent(string fileName, CancellationToken ct)
    {
        var safeFileName = Path.GetFileName(fileName);
        if (!string.Equals(safeFileName, fileName, StringComparison.Ordinal)) return NotFound();
        var extension = Path.GetExtension(safeFileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension)) return NotFound();
        var managedPath = $"/files/{StorageSubfolder}/{safeFileName}";
        var isReferenced = await db.CapabilityDocuments.AsNoTracking()
            .AnyAsync(document => document.FilePath == managedPath, ct)
            || await db.CapabilityDocumentVersions.AsNoTracking()
                .AnyAsync(version => version.FilePath == managedPath, ct);
        if (!isReferenced) return NotFound();
        var fullPath = Path.Combine(env.ContentRootPath, "wwwroot", "files", StorageSubfolder, safeFileName);
        if (!System.IO.File.Exists(fullPath)) return NotFound();
        return PhysicalFile(
            fullPath,
            GetContentType(extension),
            safeFileName,
            enableRangeProcessing: true);
    }

    /// <summary>
    /// Create a single document. Upload the file first via
    /// <see cref="Upload(IFormFile, CancellationToken)"/> to obtain a
    /// <c>filePath</c>, then post metadata here.
    /// </summary>
    [HttpPost]
    [RequirePermission("crm.capability-docs", "manage")]
    public async Task<ActionResult<CapabilityDocumentResponse>> Create(
        [FromBody] UpsertCapabilityDocumentRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.CreateAsync(request, userId.Value, ct);
            audit.Log(new AuditEvent
            {
                Action = "capability-doc.create",
                ResourceType = EntityTypes.CapabilityDocument,
                ResourceId = response.Id.ToString(),
                Message = $"Capability document #{response.Id} ({response.Name}) created.",
                NewValue = response,
            });
            return CreatedAtAction(nameof(Get), new { id = response.Id }, response);
        }
        catch (CapabilityDocumentOperationException ex)
        {
            return LogAndBadRequest("capability-doc.create", ex);
        }
    }

    /// <summary>
    /// Upload a single file to shared capability storage and return the
    /// host-relative path so the FE can supply it in a follow-up
    /// <see cref="Create"/> or <see cref="ReplaceFile"/> call.
    /// </summary>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequirePermission("crm.capability-docs", "manage")]
    public async Task<IActionResult> Upload(
        [FromForm] IFormFile? file,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "Chưa chọn file." });
        }
        if (file.Length > MaxFileSizeBytes)
        {
            return BadRequest(new { message = "File quá lớn (tối đa 20MB)." });
        }
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            return BadRequest(new { message = "Định dạng file không được hỗ trợ (PDF/DOC/DOCX/XLS/XLSX/PNG/JPG)." });
        }

        try
        {
            var uploadDir = Path.Combine(env.ContentRootPath, "wwwroot", "files", StorageSubfolder);
            Directory.CreateDirectory(uploadDir);

            var storedName = $"{Guid.NewGuid():N}{extension}";
            var storedPath = Path.Combine(uploadDir, storedName);
            await using (var stream = new FileStream(storedPath, FileMode.Create))
            {
                await file.CopyToAsync(stream, ct);
            }

            return Ok(new
            {
                filePath = $"/files/{StorageSubfolder}/{storedName}",
                originalFileName = file.FileName,
                fileSize = file.Length,
                contentType = string.IsNullOrWhiteSpace(file.ContentType)
                    ? "application/octet-stream"
                    : file.ContentType,
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to store capability-document upload {File}", file.FileName);
            return StatusCode(500, new { message = "Không thể lưu file lên máy chủ." });
        }
    }

    [HttpPut("{id:int}")]
    [RequirePermission("crm.capability-docs", "manage")]
    public async Task<ActionResult<CapabilityDocumentResponse>> Update(
        int id,
        [FromBody] UpsertCapabilityDocumentRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.UpdateAsync(id, request, userId.Value, ct);
            if (response is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "capability-doc.update",
                ResourceType = EntityTypes.CapabilityDocument,
                ResourceId = id.ToString(),
                Message = $"Capability document #{id} metadata updated.",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (CapabilityDocumentOperationException ex)
        {
            return LogAndBadRequest("capability-doc.update", ex, id);
        }
    }

    [HttpPost("{id:int}/replace-file")]
    [RequirePermission("crm.capability-docs", "manage")]
    public async Task<ActionResult<CapabilityDocumentResponse>> ReplaceFile(
        int id,
        [FromBody] ReplaceCapabilityDocumentFileRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.ReplaceFileAsync(id, request, userId.Value, ct);
            if (response is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "capability-doc.replace-file",
                ResourceType = EntityTypes.CapabilityDocument,
                ResourceId = id.ToString(),
                Message = $"Capability document #{id} file replaced -> V{response.CurrentVersion}.",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (CapabilityDocumentOperationException ex)
        {
            return LogAndBadRequest("capability-doc.replace-file", ex, id);
        }
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("crm.capability-docs", "manage")]
    public async Task<IActionResult> Delete(
        int id,
        [FromBody] ConfirmDeletionRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var result = await hardDelete.DeleteCapabilityAsync(id, request, userId.Value, ct);
            if (result is null) return NotFound();
            return result.IsComplete ? NoContent() : AcceptedOperation(result);
        }
        catch (BusinessRootDeleteException ex)
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
    [RequirePermission("crm.capability-docs", "manage")]
    public async Task<ActionResult<DeletionImpactResponse>> GetDeletionImpact(int id, CancellationToken ct)
    {
        var impact = await hardDelete.GetCapabilityImpactAsync(id, ct);
        return impact is null ? NotFound() : Ok(impact);
    }

    /// <summary>
    /// Bulk download — packages the selected documents into a single ZIP
    /// preserving the original filenames (including Vietnamese diacritics
    /// per NIH-98 AC). Duplicate filenames within the archive are suffixed
    /// with the document id so nothing is silently overwritten.
    /// </summary>
    [HttpPost("download-zip")]
    [RequirePermission("crm.capability-docs", "view")]
    public async Task<IActionResult> DownloadZip(
        [FromBody] CapabilityDocumentsZipRequest request,
        CancellationToken ct)
    {
        if (request.Ids.Count == 0)
        {
            return BadRequest(new { message = "Chưa chọn hồ sơ." });
        }

        var docs = await svc.GetManyAsync(request.Ids, ct);
        if (docs.Count == 0)
        {
            return NotFound(new { message = "Không tìm thấy hồ sơ nào." });
        }

        var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var doc in docs)
            {
                var normalized = CapabilityDocumentService.NormalizeManagedPath(doc.FilePath);
                if (normalized is null) continue;
                var relative = normalized.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var fullPath = Path.Combine(env.ContentRootPath, "wwwroot", relative);
                if (!System.IO.File.Exists(fullPath)) continue;

                var entryName = EnsureUniqueEntryName(usedNames, doc);
                var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                await using var entryStream = entry.Open();
                await using var fileStream = System.IO.File.OpenRead(fullPath);
                await fileStream.CopyToAsync(entryStream, ct);
            }
        }

        buffer.Position = 0;
        audit.Log(new AuditEvent
        {
            Action = "capability-doc.download-zip",
            ResourceType = EntityTypes.CapabilityDocument,
            Message = $"Downloaded {docs.Count} documents as ZIP.",
        });

        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        return File(buffer, "application/zip", $"capability-documents-{timestamp}.zip");
    }

    // ---------- helpers ----------

    private static string EnsureUniqueEntryName(HashSet<string> used, CapabilityDocumentResponse doc)
    {
        var baseName = string.IsNullOrWhiteSpace(doc.OriginalFileName)
            ? $"document-{doc.Id}{Path.GetExtension(doc.FilePath)}"
            : doc.OriginalFileName;
        var candidate = baseName;
        if (!used.Add(candidate))
        {
            var name = Path.GetFileNameWithoutExtension(baseName);
            var ext = Path.GetExtension(baseName);
            candidate = $"{name}-{doc.Id}{ext}";
            used.Add(candidate);
        }
        return candidate;
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

    private ActionResult<CapabilityDocumentResponse> LogAndBadRequest(
        string action, CapabilityDocumentOperationException ex, int? id = null)
    {
        audit.Log(new AuditEvent
        {
            Action = action,
            ResourceType = EntityTypes.CapabilityDocument,
            ResourceId = id?.ToString(),
            Message = ex.Message,
            Status = AuditStatus.Failure,
            FailureReason = ex.Message,
        });
        return BadRequest(new { message = ex.Message });
    }

    private int? GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return int.TryParse(raw, out var id) ? id : null;
    }

    private IActionResult AcceptedOperation(HardDeleteOperationResult result)
    {
        Response.Headers.Location = Url.Action(nameof(HardDeleteOperationsController.GetStatus),
            "HardDeleteOperations", new { operationId = result.OperationId })!;
        return StatusCode(StatusCodes.Status202Accepted, result);
    }
}
