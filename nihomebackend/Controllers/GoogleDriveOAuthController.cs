using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Services.GoogleDrive;

namespace NihomeBackend.Controllers;

[ApiController]
[Route("api/site-settings/google-drive")]
public class GoogleDriveOAuthController(
    GoogleDriveOAuthService oauth,
    IGoogleDriveSettingsStore settingsStore,
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