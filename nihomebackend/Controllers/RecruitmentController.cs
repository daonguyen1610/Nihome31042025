using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

[ApiController]
[Route("api/recruitment")]
[Route("api/v1/recruitment")]
public class RecruitmentController(RecruitmentMetadataService metadataService) : ControllerBase
{
    [HttpGet("metadata")]
    [AllowAnonymous]
    public async Task<IActionResult> GetMetadata([FromQuery] string lang = "vi", [FromQuery] bool includeInactive = false)
    {
        if (includeInactive && !(User.Identity?.IsAuthenticated ?? false))
        {
            includeInactive = false;
        }

        var metadata = await metadataService.GetAsync(lang, includeInactive);
        return Ok(metadata);
    }

    [HttpGet("metadata-items")]
    [Authorize(Roles = "SUPER_ADMIN,ADMIN")]
    public async Task<IActionResult> GetMetadataItems([FromQuery] bool includeInactive = false)
        => Ok(await metadataService.GetAllItemsAsync(includeInactive));

    [HttpPost("metadata-items")]
    [Authorize(Roles = "SUPER_ADMIN,ADMIN")]
    public async Task<IActionResult> CreateMetadataItem([FromBody] UpsertRecruitmentMetadataItemRequest req)
    {
        try
        {
            var created = await metadataService.CreateAsync(req);
            return CreatedAtAction(nameof(GetMetadataItems), new { includeInactive = true }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("metadata-items/{id:int}")]
    [Authorize(Roles = "SUPER_ADMIN,ADMIN")]
    public async Task<IActionResult> UpdateMetadataItem(int id, [FromBody] UpsertRecruitmentMetadataItemRequest req)
    {
        try
        {
            var updated = await metadataService.UpdateAsync(id, req);
            return updated == null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("metadata-items/{id:int}")]
    [Authorize(Roles = "SUPER_ADMIN,ADMIN")]
    public async Task<IActionResult> DeleteMetadataItem(int id)
    {
        try
        {
            return await metadataService.DeleteAsync(id) ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
