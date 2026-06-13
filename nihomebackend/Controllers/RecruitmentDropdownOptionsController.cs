using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

[ApiController]
[Route("api/recruitment-dropdown-options")]
[Route("api/v1/recruitment-dropdown-options")]
public class RecruitmentDropdownOptionsController(RecruitmentDropdownOptionService svc) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetByType([FromQuery] string type, [FromQuery] bool includeInactive = false)
    {
        if (string.IsNullOrWhiteSpace(type))
            return BadRequest(new { message = "type is required" });

        return Ok(await svc.GetByTypeAsync(type, includeInactive));
    }

    [HttpPost]
    [Authorize(Roles = "SUPER_ADMIN,ADMIN")]
    public async Task<IActionResult> Create([FromBody] UpsertRecruitmentDropdownOptionRequest req)
    {
        try
        {
            var created = await svc.CreateAsync(req);
            return CreatedAtAction(nameof(GetByType), new { type = created.Type, includeInactive = true }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "SUPER_ADMIN,ADMIN")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertRecruitmentDropdownOptionRequest req)
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
    [Authorize(Roles = "SUPER_ADMIN,ADMIN")]
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
