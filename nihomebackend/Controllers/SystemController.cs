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
    private const string DefaultUploadBucket = "misc";

    // Buckets accepted by /system/upload-image and /system/upload-video. Anything
    // outside this set falls back to "misc" so the upload folder stays tidy.
    public static readonly IReadOnlySet<string> AllowedUploadBuckets =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "activities",
            "news",
            "projects",
            "logos",
            DefaultUploadBucket,
        };

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
        [FromForm] string? category,
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

        var bucket = ResolveUploadBucket(category);

        try
        {
            var uploadDir = Path.Combine(env.ContentRootPath, "wwwroot", "images", "upload", bucket);
            Directory.CreateDirectory(uploadDir);

            var fileName = $"{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(uploadDir, fileName);

            await using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream, cancellationToken);

            var imageUrl = $"{ManagedImagePrefix}{bucket}/{fileName}";
            DeleteManagedUpload(previousImageUrl, imageUrl);

            return Ok(new
            {
                imageUrl
            });
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
        [FromForm] string? category,
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

        var bucket = ResolveUploadBucket(category);

        try
        {
            var uploadDir = Path.Combine(env.ContentRootPath, "wwwroot", "images", "upload", bucket);
            Directory.CreateDirectory(uploadDir);

            var fileName = $"{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(uploadDir, fileName);

            await using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream, cancellationToken);

            var mediaUrl = $"{ManagedImagePrefix}{bucket}/{fileName}";
            DeleteManagedUpload(previousImageUrl, mediaUrl);

            return Ok(new
            {
                mediaUrl
            });
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

    private void DeleteManagedUpload(string? imageUrl, string? currentImageUrl = null)
    {
        imageUrl = NormalizeManagedUploadUrl(imageUrl);
        if (string.IsNullOrWhiteSpace(imageUrl) ||
            !imageUrl.StartsWith(ManagedImagePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(currentImageUrl) &&
            string.Equals(imageUrl, currentImageUrl, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var uploadRoot = Path.GetFullPath(
            Path.Combine(env.ContentRootPath, "wwwroot", "images", "upload"));
        var relative = imageUrl[ManagedImagePrefix.Length..].Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(uploadRoot, relative));

        // Guard against forged previousImageUrl values that escape the upload root.
        var rootWithSeparator = uploadRoot.EndsWith(Path.DirectorySeparatorChar)
            ? uploadRoot
            : uploadRoot + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.Ordinal))
        {
            return;
        }

        if (System.IO.File.Exists(fullPath))
        {
            System.IO.File.Delete(fullPath);
        }
    }

    private static string ResolveUploadBucket(string? category)
    {
        if (string.IsNullOrWhiteSpace(category)) return DefaultUploadBucket;
        var trimmed = category.Trim();
        return AllowedUploadBuckets.Contains(trimmed)
            ? trimmed.ToLowerInvariant()
            : DefaultUploadBucket;
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
}
