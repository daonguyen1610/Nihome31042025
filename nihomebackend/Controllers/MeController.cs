using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Models.Rbac;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

[ApiController]
[Route("api/users/me")]
[Route("api/v1/users/me")]
[Authorize]
public class MeController(
    AppDbContext db,
    PasswordService passwordService,
    IdempotencyService idempotency,
    FingerprintService fingerprint,
    IPermissionService permissionService,
    IWebHostEnvironment env,
    ILogger<MeController> logger) : ControllerBase
{
    private const string UpdateMeScope = "users.me.update";
    private const long MaxDocumentSize = 10 * 1024 * 1024; // 10MB
    private static readonly HashSet<string> AllowedDocumentExtensions =
        [".jpg", ".jpeg", ".png", ".gif", ".webp"];

    [HttpGet]
    public async Task<ActionResult<MeResponse>> GetMe()
    {
        var userId = GetCurrentUserId();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return NotFound();
        return Ok(MapMe(user));
    }

    [HttpGet("permissions")]
    public async Task<ActionResult<MePermissionsResponse>> GetPermissions(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Id, u.Role, u.RoleEntityId, u.IsActive })
            .FirstOrDefaultAsync(ct);
        if (user == null) return NotFound();

        var codes = await permissionService.GetForUserAsync(userId, ct);
        return Ok(new MePermissionsResponse
        {
            Role = UserRoleCodeMapper.ToCode(user.Role),
            RoleId = user.RoleEntityId,
            Permissions = codes.OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList(),
        });
    }

    [HttpPut]
    [Idempotency(UpdateMeScope)]
    public async Task<ActionResult<MeResponse>> UpdateMe(
        [FromBody] UpdateMeRequest req,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        var fp = fingerprint.Compute(HttpContext);
        var userId = GetCurrentUserId();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null) return NotFound();

        if (req.Email != null)
        {
            var normalizedEmail = EmailUniqueness.Normalize(req.Email);
            if (string.IsNullOrEmpty(normalizedEmail))
            {
                return BadRequest(new { message = "Email is required." });
            }

            if (!normalizedEmail.Equals(user.Email, StringComparison.Ordinal) &&
                await EmailUniqueness.IsTakenAsync(db, normalizedEmail, excludeUserId: user.Id, ct))
            {
                return Conflict(new { message = "Email already registered." });
            }

            user.Email = normalizedEmail;
        }

        if (req.FullName != null)
        {
            user.FullName = string.IsNullOrWhiteSpace(req.FullName) ? user.FullName : req.FullName.Trim();
        }

        await db.SaveChangesAsync(ct);

        var response = MapMe(user);
        await idempotency.SaveAsync(UpdateMeScope, idempotencyKey, fp, user.Id, StatusCodes.Status200OK, response, ct);
        return Ok(response);
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        if (string.IsNullOrEmpty(req.NewPassword) || req.NewPassword.Length < 6)
        {
            return BadRequest(new { message = "Mật khẩu mới phải có ít nhất 6 ký tự." });
        }

        var userId = GetCurrentUserId();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return NotFound();

        if (!passwordService.Verify(user, req.CurrentPassword ?? string.Empty))
        {
            return BadRequest(new { message = "Mật khẩu hiện tại không đúng." });
        }

        user.PasswordHash = passwordService.Hash(user, req.NewPassword);
        await db.SaveChangesAsync();
        return Ok(new { message = "Đổi mật khẩu thành công." });
    }

    [HttpGet("documents")]
    public async Task<ActionResult<IEnumerable<UserDocumentResponse>>> GetDocuments()
    {
        var userId = GetCurrentUserId();
        var docs = await db.UserDocuments.AsNoTracking()
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new UserDocumentResponse
            {
                Id = d.Id,
                DocumentType = d.DocumentType.ToString(),
                OriginalName = d.OriginalName,
                FileUrl = d.FileUrl,
                ContentType = d.ContentType,
                Size = d.Size,
                CreatedAt = d.CreatedAt,
            })
            .ToListAsync();
        return Ok(docs);
    }

    [HttpPost("documents")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<UserDocumentResponse>> UploadDocument(
        [FromForm] IFormFile? file,
        [FromForm] string? documentType,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "Chưa chọn tệp." });
        }

        if (file.Length > MaxDocumentSize)
        {
            return BadRequest(new { message = "Tệp quá lớn (tối đa 10MB)." });
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension) || !AllowedDocumentExtensions.Contains(extension))
        {
            return BadRequest(new { message = "Định dạng không hợp lệ (chỉ JPEG, PNG, GIF, WebP)." });
        }

        var userId = GetCurrentUserId();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return NotFound();

        var docType = ParseDocumentType(documentType);

        try
        {
            var uploadDir = Path.Combine(env.ContentRootPath, "wwwroot", "images", "upload", "documents");
            Directory.CreateDirectory(uploadDir);

            var fileName = $"{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(uploadDir, fileName);

            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            var doc = new UserDocument
            {
                UserId = userId,
                DocumentType = docType,
                OriginalName = Path.GetFileName(file.FileName),
                FileUrl = $"/images/upload/documents/{fileName}",
                ContentType = file.ContentType ?? "application/octet-stream",
                Size = file.Length,
                CreatedAt = DateTime.UtcNow,
            };
            db.UserDocuments.Add(doc);
            await db.SaveChangesAsync(cancellationToken);

            return Ok(new UserDocumentResponse
            {
                Id = doc.Id,
                DocumentType = doc.DocumentType.ToString(),
                OriginalName = doc.OriginalName,
                FileUrl = doc.FileUrl,
                ContentType = doc.ContentType,
                Size = doc.Size,
                CreatedAt = doc.CreatedAt,
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error uploading user document");
            return StatusCode(500, new { message = "Lỗi khi tải tệp lên." });
        }
    }

    [HttpDelete("documents/{id:int}")]
    public async Task<IActionResult> DeleteDocument(int id)
    {
        var userId = GetCurrentUserId();
        var doc = await db.UserDocuments.FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);
        if (doc == null) return NotFound();

        try
        {
            if (!string.IsNullOrEmpty(doc.FileUrl) && doc.FileUrl.StartsWith("/images/upload/documents/"))
            {
                var relative = doc.FileUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var filePath = Path.Combine(env.ContentRootPath, "wwwroot", relative);
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete document file {Url}", doc.FileUrl);
        }

        db.UserDocuments.Remove(doc);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private int GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("uid");
        return int.TryParse(raw, out var id) ? id : 0;
    }

    private static UserDocumentType ParseDocumentType(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return UserDocumentType.OTHER;
        return Enum.TryParse<UserDocumentType>(raw.Trim(), ignoreCase: true, out var v) ? v : UserDocumentType.OTHER;
    }

    private static MeResponse MapMe(ApplicationUser u) => new()
    {
        Id = u.Id,
        PhoneNumber = u.PhoneNumber,
        FullName = u.FullName,
        Email = u.Email,
        Role = u.Role.ToString(),
        IsActive = u.IsActive,
        AvatarUrl = u.AvatarUrl,
    };
}

public class UpdateMeRequest
{
    [MaxLength(150)]
    public string? FullName { get; set; }

    [EmailAddress, MaxLength(150)]
    public string? Email { get; set; }
}

public class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, MinLength(6)]
    public string NewPassword { get; set; } = string.Empty;
}

public class MeResponse
{
    public int Id { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? AvatarUrl { get; set; }
}

public class UserDocumentResponse
{
    public int Id { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime CreatedAt { get; set; }
}
