using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
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
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false, [FromQuery] string lang = "vi")
    {
        // Only admin can see inactive positions
        if (includeInactive && !User.Identity?.IsAuthenticated == true)
            return Ok(await svc.GetAllAsync(false, lang));
        return Ok(await svc.GetAllAsync(includeInactive, lang));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, [FromQuery] string lang = "vi")
    {
        var item = await svc.GetByIdAsync(id, lang);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Authorize]
    [RequirePermission("recruitment.positions", "manage")]
    public async Task<IActionResult> Create([FromBody] UpsertJobPositionRequest req)
    {
        try
        {
            var created = await svc.CreateAsync(req);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [Authorize]
    [RequirePermission("recruitment.positions", "manage")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertJobPositionRequest req)
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
    [Authorize]
    [RequirePermission("recruitment.positions", "manage")]
    public async Task<IActionResult> Delete(int id)
        => await svc.DeleteAsync(id) ? NoContent() : NotFound();
}
