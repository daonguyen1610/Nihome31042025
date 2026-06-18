using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using NihomeBackend.Services.Audit;
#pragma warning disable CS4014

namespace NihomeBackend.Controllers;

[ApiController]
[Authorize]
[RequirePermission("content.news", "view")]
[Route("api/news")]
[Route("api/v1/news")]
public class NewsController(
    NewsService svc,
    IAuditLogger audit,
    INotificationService notifications) : ControllerBase
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
    [RequirePermission("content.news", "manage")]
    public async Task<IActionResult> Create([FromBody] UpsertNewsRequest req)
    {
        var result = await svc.CreateAsync(req);
        audit.Log(new AuditEvent
        {
            Action = "news.create",
            ResourceType = "NewsArticle",
            ResourceId = result.Id.ToString(),
            Message = $"Created news '{result.Title}'",
            NewValue = result,
        });
        notifications.CreateForAdminsAsync(
            "News",
            $"Tin tức mới được tạo: {result.Title}",
            null,
            $"/admin/posts/{result.Slug}");
        return CreatedAtAction(nameof(GetBySlug), new { slug = result.Slug }, result);
    }

    [HttpPut("{id:int}")]
    [RequirePermission("content.news", "manage")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertNewsRequest req)
    {
        var result = await svc.UpdateAsync(id, req);
        if (result == null)
        {
            audit.Log(new AuditEvent
            {
                Action = "news.update",
                ResourceType = "NewsArticle",
                ResourceId = id.ToString(),
                Message = $"Update failed: news {id} not found",
                Status = AuditStatus.Failure,
                FailureReason = "not_found",
            });
            return NotFound();
        }
        audit.Log(new AuditEvent
        {
            Action = "news.update",
            ResourceType = "NewsArticle",
            ResourceId = id.ToString(),
            Message = $"Updated news '{result.Title}'",
            NewValue = result,
        });
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("content.news", "manage")]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await svc.DeleteAsync(id);
        if (!ok)
        {
            audit.Log(new AuditEvent
            {
                Action = "news.delete",
                ResourceType = "NewsArticle",
                ResourceId = id.ToString(),
                Message = $"Delete failed: news {id} not found",
                Status = AuditStatus.Failure,
                FailureReason = "not_found",
            });
            return NotFound();
        }
        audit.Log("news.delete", "NewsArticle", id.ToString(), $"Deleted news {id}");
        return NoContent();
    }
}
