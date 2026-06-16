using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;
using NihomeBackend.Services.Audit;

namespace NihomeBackend.Controllers;

[ApiController]
[Authorize(Roles = "SUPER_ADMIN,ADMIN")]
[Route("api/processes")]
[Route("api/v1/processes")]
public class ProcessesController(ProcessService svc, IWebHostEnvironment env, IAuditLogger audit, ILogger<ProcessesController> logger) : ControllerBase
{
    private static readonly HashSet<string> AllowedImageExtensions =
        [".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg"];
    private static readonly HashSet<string> AllowedFileExtensions =
        [".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".zip", ".rar", ".txt"];

    private const long MaxImageBytes = 10 * 1024 * 1024;   // 10 MB
    private const long MaxFileBytes = 25 * 1024 * 1024;    // 25 MB

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll() => Ok(await svc.GetAllGroupedAsync());

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertProcessRequest req)
    {
        var result = await svc.CreateAsync(req);
        audit.Log(new AuditEvent
        {
            Action = "process.create",
            ResourceType = "ProcessDocument",
            ResourceId = result.Id.ToString(),
            Message = $"Created process '{result.Title}' (group={result.GroupKey})",
            NewValue = result,
        });
        return Created("", result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertProcessRequest req)
    {
        var result = await svc.UpdateAsync(id, req);
        if (result == null)
        {
            audit.Log(new AuditEvent
            {
                Action = "process.update",
                ResourceType = "ProcessDocument",
                ResourceId = id.ToString(),
                Message = $"Update failed: process {id} not found",
                Status = AuditStatus.Failure,
                FailureReason = "not_found",
            });
            return NotFound();
        }
        audit.Log(new AuditEvent
        {
            Action = "process.update",
            ResourceType = "ProcessDocument",
            ResourceId = id.ToString(),
            Message = $"Updated process '{result.Title}'",
            NewValue = result,
        });
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await svc.DeleteAsync(id);
        if (!ok)
        {
            audit.Log(new AuditEvent
            {
                Action = "process.delete",
                ResourceType = "ProcessDocument",
                ResourceId = id.ToString(),
                Message = $"Delete failed: process {id} not found",
                Status = AuditStatus.Failure,
                FailureReason = "not_found",
            });
            return NotFound();
        }
        audit.Log("process.delete", "ProcessDocument", id.ToString(), $"Deleted process {id}");
        return NoContent();
    }

    [HttpPost("upload-image")]
    [Consumes("multipart/form-data")]
    public Task<IActionResult> UploadImage([FromForm] IFormFile? file, [FromForm] string? groupKey, CancellationToken ct)
        => SaveUpload(file, groupKey, AllowedImageExtensions, MaxImageBytes, "image", ct);

    [HttpPost("upload-file")]
    [Consumes("multipart/form-data")]
    public Task<IActionResult> UploadFile([FromForm] IFormFile? file, [FromForm] string? groupKey, CancellationToken ct)
        => SaveUpload(file, groupKey, AllowedFileExtensions, MaxFileBytes, "file", ct);

    private async Task<IActionResult> SaveUpload(
        IFormFile? file,
        string? groupKey,
        HashSet<string> allowedExt,
        long maxBytes,
        string kind,
        CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file uploaded" });

        if (file.Length > maxBytes)
            return BadRequest(new { message = $"File too large (max {maxBytes / (1024 * 1024)} MB)" });

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension) || !allowedExt.Contains(extension))
            return BadRequest(new { message = $"Invalid {kind} format" });

        var safeGroup = SanitizeGroup(groupKey);

        try
        {
            var relativeDir = Path.Combine("processes", safeGroup);
            var uploadDir = Path.Combine(env.ContentRootPath, "wwwroot", relativeDir);
            Directory.CreateDirectory(uploadDir);

            var fileName = $"{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(uploadDir, fileName);

            await using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream, ct);

            var url = $"/{relativeDir.Replace(Path.DirectorySeparatorChar, '/')}/{fileName}";
            return Ok(new ProcessAssetInfo
            {
                DisplayName = Path.GetFileNameWithoutExtension(file.FileName),
                Url = url,
                OriginalFileName = file.FileName,
                ContentType = file.ContentType ?? "",
                FileSizeBytes = file.Length,
                SortOrder = 0,
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error uploading process {Kind}", kind);
            return StatusCode(500, new { message = $"Error uploading {kind}" });
        }
    }

    private static string SanitizeGroup(string? groupKey)
    {
        if (string.IsNullOrWhiteSpace(groupKey)) return "general";
        var cleaned = new string(groupKey
            .Trim()
            .ToLowerInvariant()
            .Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_')
            .ToArray());
        return string.IsNullOrEmpty(cleaned) ? "general" : cleaned;
    }
}
