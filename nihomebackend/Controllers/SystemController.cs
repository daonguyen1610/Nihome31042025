using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Models;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SystemController(
    TimeService timeService,
    IWebHostEnvironment env,
    ILogger<SystemController> logger) : ControllerBase
{
    private const string ManagedImagePrefix = "/images/upload/";
    private static readonly HashSet<string> AllowedImageExtensions =
        [".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg"];

    [HttpGet("health")]
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
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "No file uploaded" });
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension) || !AllowedImageExtensions.Contains(extension))
        {
            return BadRequest(new { message = "Invalid image format" });
        }

        try
        {
            var uploadDir = Path.Combine(env.ContentRootPath, "wwwroot", "images", "upload");
            Directory.CreateDirectory(uploadDir);

            var fileName = $"{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(uploadDir, fileName);

            await using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream, cancellationToken);

            DeleteManagedUpload(previousImageUrl, fileName);

            return Ok(new
            {
                imageUrl = $"/images/upload/{fileName}"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error uploading image file");
            return StatusCode(500, new { message = "Error uploading image" });
        }
    }

    private void DeleteManagedUpload(string? imageUrl, string? currentFileName = null)
    {
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

        var fullPath = Path.Combine(env.ContentRootPath, "wwwroot", "images", "upload", previousFileName);
        if (System.IO.File.Exists(fullPath))
        {
            System.IO.File.Delete(fullPath);
        }
    }
}
