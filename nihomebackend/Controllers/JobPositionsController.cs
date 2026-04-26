using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

[ApiController]
[Route("api/job-positions")]
[Route("api/v1/job-positions")]
public class JobPositionsController(JobPositionService svc) : ControllerBase
{
    /// <summary>Public: list active positions. Admin: optionally include inactive.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false)
    {
        // Only admin can see inactive positions
        if (includeInactive && !User.Identity?.IsAuthenticated == true)
            return Ok(await svc.GetAllAsync(false));
        return Ok(await svc.GetAllAsync(includeInactive));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var item = await svc.GetByIdAsync(id);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Authorize(Roles = "SUPER_ADMIN,ADMIN")]
    public async Task<IActionResult> Create([FromBody] UpsertJobPositionRequest req)
    {
        var created = await svc.CreateAsync(req);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "SUPER_ADMIN,ADMIN")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertJobPositionRequest req)
    {
        var updated = await svc.UpdateAsync(id, req);
        return updated == null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "SUPER_ADMIN,ADMIN")]
    public async Task<IActionResult> Delete(int id)
        => await svc.DeleteAsync(id) ? NoContent() : NotFound();
}
