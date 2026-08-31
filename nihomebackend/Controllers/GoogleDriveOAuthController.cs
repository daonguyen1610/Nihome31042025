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
    ILogger<GoogleDriveOAuthController> logger) : ControllerBase
{
    [HttpGet("status")]
    [Authorize]
    [RequirePermission("system.settings", "view")]
    public async Task<ActionResult<GoogleDriveAdminStatusResponse>> Status(CancellationToken ct) =>
        Ok(await oauth.GetStatusAsync(ct));

    [HttpPost("oauth/start")]
    [Authorize]
    [RequirePermission("system.settings", "manage")]
    public ActionResult<GoogleDriveOAuthStartResponse> Start()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("uid");
        if (!int.TryParse(rawUserId, out var userId) || userId <= 0) return Unauthorized();
        return Ok(oauth.CreateAuthorizationRequest(userId));
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
        return Redirect(oauth.BuildFrontendResultUrl(result));
    }
}