using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

[ApiController]
[Route("api/employment-types")]
[Route("api/v1/employment-types")]
public class EmploymentTypesController(EmploymentTypeService svc) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false)
        => Ok(await svc.GetAllAsync(includeInactive));

    [HttpPost]
    [Authorize]
    [RequirePermission("recruitment.options", "manage")]
    public async Task<IActionResult> Create([FromBody] UpsertEmploymentTypeRequest req)
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
    [Authorize]
    [RequirePermission("recruitment.options", "manage")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertEmploymentTypeRequest req)
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
    [RequirePermission("recruitment.options", "manage")]
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
