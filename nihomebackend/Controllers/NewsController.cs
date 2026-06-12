using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Data;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using NihomeBackend.Services.Audit;

namespace NihomeBackend.Controllers;

[ApiController]
[Route("api/news")]
[Route("api/v1/news")]
public class NewsController(NewsService svc, IAuditLogger audit, AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string lang = "vi") => Ok(await svc.GetAllAsync(lang));

    [HttpGet("{slug}")]
    public async Task<IActionResult> GetBySlug(string slug, [FromQuery] string lang = "vi")
    {
        var item = await svc.GetBySlugAsync(slug, lang);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertNewsRequest req)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var result = await svc.CreateAsync(req);
        audit.LogTransactional(new AuditEvent
        {
            Action = "news.create",
            ResourceType = "NewsArticle",
            ResourceId = result.Id.ToString(),
            Message = $"Created news '{result.Title}'",
            NewValue = result,
        }, db);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return CreatedAtAction(nameof(GetBySlug), new { slug = result.Slug }, result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertNewsRequest req)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
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
        audit.LogTransactional(new AuditEvent
        {
            Action = "news.update",
            ResourceType = "NewsArticle",
            ResourceId = id.ToString(),
            Message = $"Updated news '{result.Title}'",
            NewValue = result,
        }, db);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
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
        audit.LogTransactional(new AuditEvent
        {
            Action = "news.delete",
            ResourceType = "NewsArticle",
            ResourceId = id.ToString(),
            Message = $"Deleted news {id}",
        }, db);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return NoContent();
    }
}
