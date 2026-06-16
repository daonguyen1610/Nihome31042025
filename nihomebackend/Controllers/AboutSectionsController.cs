using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

[ApiController]
[Authorize]
[RequirePermission("content.about", "view")]
[Route("api/about-sections")]
[Route("api/v1/about-sections")]
public class AboutSectionsController(AboutSectionService svc) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = true) =>
        Ok(await svc.GetAllAsync(activeOnly));

    [HttpGet("{slug}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBySlug(string slug)
    {
        var item = await svc.GetBySlugAsync(slug);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [RequirePermission("content.about", "manage")]
    public async Task<IActionResult> Create([FromBody] UpsertAboutSectionRequest req)
    {
        var result = await svc.CreateAsync(req);
        return CreatedAtAction(nameof(GetBySlug), new { slug = result.Slug }, result);
    }

    [HttpPut("{id:int}")]
    [RequirePermission("content.about", "manage")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertAboutSectionRequest req)
    {
        var result = await svc.UpdateAsync(id, req);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("content.about", "manage")]
    public async Task<IActionResult> Delete(int id)
    {
        return await svc.DeleteAsync(id) ? NoContent() : NotFound();
    }
}
