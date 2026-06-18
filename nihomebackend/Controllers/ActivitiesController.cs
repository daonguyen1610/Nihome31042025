using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

[ApiController]
[Authorize]
[RequirePermission("content.activities", "view")]
[Route("api/activities")]
[Route("api/v1/activities")]
public class ActivitiesController(ActivityService svc, INotificationService notifications) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll([FromQuery] string lang = "vi") => Ok(await svc.GetAllAsync(lang));

    [HttpGet("{slug}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBySlug(string slug, [FromQuery] string lang = "vi")
    {
        var item = await svc.GetBySlugAsync(slug, lang);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [RequirePermission("content.activities", "manage")]
    public async Task<IActionResult> Create([FromBody] UpsertActivityRequest req)
    {
        var result = await svc.CreateAsync(req);

        try
        {
            await notifications.CreateForAdminsAsync(
                "Activity",
                $"Bài đăng mới: {result.Title}",
                null,
                $"/admin/posts/{result.Slug}");
        }
        catch { /* best-effort */ }

        return CreatedAtAction(nameof(GetBySlug), new { slug = result.Slug }, result);
    }

    [HttpPut("{id:int}")]
    [RequirePermission("content.activities", "manage")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertActivityRequest req)
    {
        var result = await svc.UpdateAsync(id, req);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("content.activities", "manage")]
    public async Task<IActionResult> Delete(int id)
    {
        return await svc.DeleteAsync(id) ? NoContent() : NotFound();
    }
}
