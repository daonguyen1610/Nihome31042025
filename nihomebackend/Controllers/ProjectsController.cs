using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using NihomeBackend.Services.Audit;

namespace NihomeBackend.Controllers;

[ApiController]
[Authorize(Roles = "SUPER_ADMIN,ADMIN")]
[Route("api/projects")]
[Route("api/v1/projects")]
public class ProjectsController(ProjectService svc, IAuditLogger audit) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll() => Ok(await svc.GetAllAsync());

    [HttpGet("{slug}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBySlug(string slug)
    {
        var item = await svc.GetBySlugAsync(slug);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpPost]
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
        return CreatedAtAction(nameof(GetBySlug), new { slug = result.Slug }, result);
    }

    [HttpPut("{id:int}")]
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
