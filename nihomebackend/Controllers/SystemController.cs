using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Models;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

[ApiController]
[Authorize]
[RequirePermission("system.uploads", "manage")]
[Route("api/[controller]")]
public class SystemController(
    TimeService timeService,
    IWebHostEnvironment env,
    ILogger<SystemController> logger) : ControllerBase
{
    private const string ManagedImagePrefix = "/images/upload/";
    private static readonly HashSet<string> AllowedImageExtensions =
        [".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg"];
    private static readonly HashSet<string> AllowedVideoExtensions =
        [".mp4", ".webm", ".mov", ".m4v"];
    private static readonly HashSet<string> AllowedCvExtensions =
        [".pdf", ".doc", ".docx", ".xls", ".xlsx", ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg"];

    [HttpGet("health")]
    [AllowAnonymous]
    public ActionResult<HealthResponse> GetHealth()
    {
        return Ok(new HealthResponse
        {
            Name = "Nihome Backend",
            Environment = HttpContext.RequestServices
                .GetRequiredService<IHostEnvironment>()
                .EnvironmentName,
            Status = "Healthy",
            TimestampUtc = timeService.GetUtcNow()
        });
    }

    [HttpPost("upload-image")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadImage(
        [FromForm] IFormFile? file,
        [FromForm] string? previousImageUrl,
        [FromForm] string? folder,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "No file uploaded" });
        }

        if (file.Length > 5 * 1024 * 1024)
        {
            return BadRequest(new { message = "File quá lớn (tối đa 5MB)" });
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension) || !AllowedImageExtensions.Contains(extension))
        {
            return BadRequest(new { message = "Invalid image format" });
        }

        try
        {
            var safeFolder = SanitizeFolder(folder);
            var uploadDir = Path.Combine(env.ContentRootPath, "wwwroot", "images", "upload", safeFolder);
            Directory.CreateDirectory(uploadDir);

            var fileName = $"{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(uploadDir, fileName);

            await using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream, cancellationToken);

            DeleteManagedUpload(previousImageUrl, fileName);

            var urlPath = string.IsNullOrEmpty(safeFolder)
                ? $"/images/upload/{fileName}"
                : $"/images/upload/{safeFolder}/{fileName}";

            return Ok(new { imageUrl = urlPath });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error uploading image file");
            return StatusCode(500, new { message = "Error uploading image" });
        }
    }

    [HttpPost("upload-video")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadVideo(
        [FromForm] IFormFile? file,
        [FromForm] string? previousImageUrl,
        [FromForm] string? folder,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "No file uploaded" });
        }

        if (file.Length > 25 * 1024 * 1024)
        {
            return BadRequest(new { message = "File quá lớn (tối đa 25MB)" });
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension) || !AllowedVideoExtensions.Contains(extension))
        {
            return BadRequest(new { message = "Invalid video format" });
        }

        try
        {
            var safeFolder = SanitizeFolder(folder);
            var uploadDir = Path.Combine(env.ContentRootPath, "wwwroot", "images", "upload", safeFolder);
            Directory.CreateDirectory(uploadDir);

            var fileName = $"{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(uploadDir, fileName);

            await using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream, cancellationToken);

            DeleteManagedUpload(previousImageUrl, fileName);

            var urlPath = string.IsNullOrEmpty(safeFolder)
                ? $"/images/upload/{fileName}"
                : $"/images/upload/{safeFolder}/{fileName}";

            return Ok(new { mediaUrl = urlPath });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error uploading video file");
            return StatusCode(500, new { message = "Error uploading video" });
        }
    }

    [HttpPost("upload-cv")]
    [AllowAnonymous]
    [Consumes("multipart/form-data")]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
    public async Task<IActionResult> UploadCv(
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file uploaded" });

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension) || !AllowedCvExtensions.Contains(extension))
            return BadRequest(new { message = "Chỉ chấp nhận file PDF, DOC, DOCX, XLS, XLSX và ảnh" });

        try
        {
            var uploadDir = Path.Combine(env.ContentRootPath, "wwwroot", "files", "cv");
            Directory.CreateDirectory(uploadDir);

            var fileName = $"{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(uploadDir, fileName);

            await using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream, cancellationToken);

            return Ok(new { cvUrl = $"/files/cv/{fileName}" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error uploading CV file");
            return StatusCode(500, new { message = "Error uploading CV" });
        }
    }

    private void DeleteManagedUpload(string? imageUrl, string? currentFileName = null)
    {
        imageUrl = NormalizeManagedUploadUrl(imageUrl);
        if (string.IsNullOrWhiteSpace(imageUrl) ||
            !imageUrl.StartsWith(ManagedImagePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var previousFileName = Path.GetFileName(imageUrl);
        if (!string.IsNullOrWhiteSpace(currentFileName) &&
            string.Equals(previousFileName, currentFileName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Use the full URL path to support subfolder structure (e.g. /images/upload/projects/slug/uuid.jpg)
        var relativePath = imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(env.ContentRootPath, "wwwroot", relativePath);
        if (System.IO.File.Exists(fullPath))
        {
            System.IO.File.Delete(fullPath);
        }
    }

    private static string? NormalizeManagedUploadUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return imageUrl;
        }

        if (imageUrl.StartsWith(ManagedImagePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return imageUrl;
        }

        if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) &&
            uri.AbsolutePath.StartsWith(ManagedImagePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return uri.AbsolutePath;
        }

        return imageUrl;
    }

    // Allow only safe subfolder names: letters, digits, hyphens, forward slashes.
    // Prevents path traversal attacks (blocks "..", absolute paths, etc.).
    private static string SanitizeFolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            return string.Empty;

        // Strip any leading/trailing slashes and collapse repeated slashes
        var parts = folder
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => p != ".." && p != "." && System.Text.RegularExpressions.Regex.IsMatch(p, @"^[\w\-]+$"))
            .ToArray();

        return string.Join("/", parts);
    }
}
