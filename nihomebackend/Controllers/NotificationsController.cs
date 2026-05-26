using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
[Route("api/v1/notifications")]
public class NotificationsController(INotificationService svc) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int skip = 0, [FromQuery] int take = 20)
    {
        var userId = GetUserId();
        return userId == null ? Unauthorized() : Ok(await svc.GetForUserAsync(userId.Value, skip, take));
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = GetUserId();
        return userId == null ? Unauthorized() : Ok(new { count = await svc.GetUnreadCountAsync(userId.Value) });
    }

    [HttpPatch("{id:long}/mark-read")]
    public async Task<IActionResult> MarkRead(long id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await svc.MarkReadAsync(id, userId.Value);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost("mark-all-read")]
    public async Task<IActionResult> MarkAllRead()
    {
        var userId = GetUserId();
        return userId == null ? Unauthorized() : Ok(new { count = await svc.MarkAllReadAsync(userId.Value) });
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        return await svc.DeleteAsync(id, userId.Value) ? NoContent() : NotFound();
    }

    private int? GetUserId()
    {
        var principal = HttpContext?.User;
        if (principal == null) return null;

        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("uid");

        return int.TryParse(value, out var userId) ? userId : null;
    }
}
