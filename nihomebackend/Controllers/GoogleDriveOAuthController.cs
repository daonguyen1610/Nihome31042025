using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Services.GoogleDrive;

namespace NihomeBackend.Controllers;

public sealed record GoogleDriveConnectionTestResponse(
    string? AccountEmail,
    string FolderName,
    string FolderLink,
    bool IsSharedDrive,
    bool CanAddChildren);

[ApiController]
[Route("api/site-settings/google-drive")]
public class GoogleDriveOAuthController(
    GoogleDriveOAuthService oauth,
    IGoogleDriveSettingsStore settingsStore,
    IGoogleDriveAdapter drive,
    ILogger<GoogleDriveOAuthController> logger) : ControllerBase
{
    [HttpGet("configuration")]
    [Authorize]
    [RequirePermission("system.settings", "view")]
    public async Task<ActionResult<GoogleDriveAdminConfigurationResponse>> Configuration(CancellationToken ct) =>
        Ok(await settingsStore.GetAdminAsync(ct));

    [HttpPut("configuration")]
    [Authorize]
    [RequirePermission("system.settings", "manage")]
    public async Task<ActionResult<GoogleDriveAdminConfigurationResponse>> UpdateConfiguration(
        [FromBody] UpdateGoogleDriveConfigurationRequest request,
        CancellationToken ct)
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("uid");
        if (!int.TryParse(rawUserId, out var userId) || userId <= 0) return Unauthorized();
        try
        {
            return Ok(await settingsStore.UpdateAsync(request, userId, ct));
        }
        catch (GoogleDriveSettingsValidationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (GoogleDriveSettingsConcurrencyException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpGet("status")]
    [Authorize]
    [RequirePermission("system.settings", "view")]
    public async Task<ActionResult<GoogleDriveAdminStatusResponse>> Status(CancellationToken ct) =>
        Ok(await oauth.GetStatusAsync(ct));

    [HttpPost("test")]
    [Authorize]
    [RequirePermission("system.settings", "manage")]
    public async Task<ActionResult<GoogleDriveConnectionTestResponse>> Test(CancellationToken ct)
    {
        try
        {
            var options = await settingsStore.GetRuntimeAsync(ct);
            if (!options.Enabled || string.IsNullOrWhiteSpace(options.RefreshToken))
            {
                return BadRequest(new
                {
                    code = "DRIVE_NOT_CONNECTED",
                    message = "Google Drive chưa được bật hoặc chưa kết nối tài khoản.",
                });
            }

            var connection = await drive.CheckConnectionAsync(ct);
            if (!connection.IsFolder || connection.IsTrashed)
            {
                return BadRequest(new
                {
                    code = "DRIVE_INVALID_ROOT",
                    message = "Thư mục gốc không phải thư mục hợp lệ hoặc đang bị xóa.",
                });
            }

            return Ok(new GoogleDriveConnectionTestResponse(
                connection.AccountEmail,
                connection.FolderName,
                connection.FolderLink,
                connection.IsSharedDrive,
                connection.CanAddChildren));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                "Google Drive connection test failed ({ExceptionType}).",
                exception.GetType().Name);
            return BadRequest(new
            {
                code = "DRIVE_TEST_FAILED",
                message = "Không đọc được cấu hình hoặc không kết nối được Google Drive. Kiểm tra quyền truy cập thư mục gốc và kết nối tài khoản.",
            });
        }
    }

    [HttpPost("oauth/start")]
    [Authorize]
    [RequirePermission("system.settings", "manage")]
    public async Task<ActionResult<GoogleDriveOAuthStartResponse>> Start(CancellationToken ct)
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("uid");
        if (!int.TryParse(rawUserId, out var userId) || userId <= 0) return Unauthorized();
        try
        {
            return Ok(await oauth.CreateAuthorizationRequestAsync(userId, ct));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("oauth/disconnect")]
    [Authorize]
    [RequirePermission("system.settings", "manage")]
    public async Task<ActionResult<GoogleDriveDisconnectResponse>> Disconnect(CancellationToken ct)
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("uid");
        if (!int.TryParse(rawUserId, out var userId) || userId <= 0) return Unauthorized();
        try
        {
            return Ok(await oauth.DisconnectAsync(userId, ct));
        }
        catch (GoogleDriveSettingsValidationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (GoogleDriveSettingsConcurrencyException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpGet("oauth/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        CancellationToken ct)
    {
        GoogleDriveOAuthResult result;
        try
        {
            result = await oauth.CompleteAsync(code, state, error, ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                "Google Drive OAuth callback failed ({ExceptionType}).",
                exception.GetType().Name);
            result = GoogleDriveOAuthResult.TokenExchangeFailed;
        }
        return Redirect(await oauth.BuildFrontendResultUrlAsync(result, ct));
    }
}