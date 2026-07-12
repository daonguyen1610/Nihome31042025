using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
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

    // ------------------------ Template administration ------------------------

    /// <summary>List all seeded notification templates (read-only overview).</summary>
    [HttpGet("templates")]
    [RequirePermission("system.notifications", "manage")]
    public async Task<ActionResult<List<NotificationTemplateResponse>>> ListTemplates()
    {
        var items = await svc.ListTemplatesAsync();
        return Ok(items.Select(MapTemplate).ToList());
    }

    /// <summary>Fetch a single template by its code.</summary>
    [HttpGet("templates/{code}")]
    [RequirePermission("system.notifications", "manage")]
    public async Task<ActionResult<NotificationTemplateResponse>> GetTemplate(string code)
    {
        var template = await svc.GetTemplateAsync(code);
        return template == null ? NotFound() : Ok(MapTemplate(template));
    }

    /// <summary>
    /// Update the channel + active flag of a seeded template. Title/body
    /// content is edited through the translation admin (they live in the
    /// standard translation table under keys
    /// <c>notification.&lt;code&gt;.title</c> / <c>...body</c>).
    /// </summary>
    [HttpPut("templates/{code}")]
    [RequirePermission("system.notifications", "manage")]
    public async Task<ActionResult<NotificationTemplateResponse>> UpdateTemplate(
        string code,
        [FromBody] UpdateNotificationTemplateRequest req)
    {
        var updated = await svc.UpdateTemplateAsync(code, req.Channel, req.IsActive);
        return updated == null ? NotFound() : Ok(MapTemplate(updated));
    }

    private static NotificationTemplateResponse MapTemplate(NotificationTemplate t) => new()
    {
        Code = t.Code,
        Module = t.Module,
        TitleKey = t.TitleKey,
        BodyKey = t.BodyKey,
        Channel = t.Channel.ToString(),
        IsActive = t.IsActive,
        AdminDescription = t.AdminDescription,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt,
    };

    private int? GetUserId()
    {
        var principal = HttpContext?.User;
        if (principal == null) return null;

        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("uid");

        return int.TryParse(value, out var userId) ? userId : null;
    }
}
