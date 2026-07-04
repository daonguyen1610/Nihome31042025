using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using NihomeBackend.Services.Audit;
namespace NihomeBackend.Controllers;

[ApiController]
[Authorize]
[RequirePermission("content.projects", "view")]
[Route("api/projects")]
[Route("api/v1/projects")]
public class ProjectsController(
    ProjectService svc,
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
    [RequirePermission("content.projects", "manage")]
    public async Task<IActionResult> Create([FromBody] UpsertProjectRequest req)
    {
        var result = await svc.CreateAsync(req);
        audit.Log(new AuditEvent
        {
            Action = "project.create",
            ResourceType = "Project",
            ResourceId = result.Id.ToString(),
            Message = $"Created project '{result.Name}'",
            NewValue = result,
        });
        try
        {
            await notifications.CreateForAdminsAsync(
                "Project",
                $"Dự án mới được tạo: {result.Name}",
                null,
                $"/admin/projects/{result.Slug}");
        }
        catch { /* best-effort */ }
        return CreatedAtAction(nameof(GetBySlug), new { slug = result.Slug }, result);
    }

    [HttpPut("{id:int}")]
    [RequirePermission("content.projects", "manage")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertProjectRequest req)
    {
        var result = await svc.UpdateAsync(id, req);
        if (result == null)
        {
            audit.Log(new AuditEvent
            {
                Action = "project.update",
                ResourceType = "Project",
                ResourceId = id.ToString(),
                Message = $"Update failed: project {id} not found",
                Status = AuditStatus.Failure,
                FailureReason = "not_found",
            });
            return NotFound();
        }
        audit.Log(new AuditEvent
        {
            Action = "project.update",
            ResourceType = "Project",
            ResourceId = id.ToString(),
            Message = $"Updated project '{result.Name}'",
            NewValue = result,
        });
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("content.projects", "manage")]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await svc.DeleteAsync(id);
        if (!ok)
        {
            audit.Log(new AuditEvent
            {
                Action = "project.delete",
                ResourceType = "Project",
                ResourceId = id.ToString(),
                Message = $"Delete failed: project {id} not found",
                Status = AuditStatus.Failure,
                FailureReason = "not_found",
            });
            return NotFound();
        }
        audit.Log("project.delete", "Project", id.ToString(), $"Deleted project {id}");
        return NoContent();
    }
}
