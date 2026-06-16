using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

[ApiController]
[Authorize]
[RequirePermission("content.project-categories", "view")]
[Route("api/project-categories")]
[Route("api/v1/project-categories")]
public class ProjectCategoriesController(ProjectCategoryService svc) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false)
        => Ok(await svc.GetAllAsync(includeInactive));

    [HttpPost]
    [RequirePermission("content.project-categories", "manage")]
    public async Task<IActionResult> Create([FromBody] UpsertProjectCategoryRequest req)
    {
        try
        {
            var created = await svc.CreateAsync(req);
            return CreatedAtAction(nameof(GetAll), new { includeInactive = true }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [RequirePermission("content.project-categories", "manage")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertProjectCategoryRequest req)
    {
        try
        {
            var updated = await svc.UpdateAsync(id, req);
            return updated == null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("content.project-categories", "manage")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            return await svc.DeleteAsync(id) ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
