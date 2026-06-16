using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

[ApiController]
[Route("api/job-applications")]
[Route("api/v1/job-applications")]
public class JobApplicationsController(JobApplicationService svc) : ControllerBase
{
    /// <summary>Admin: list all applications, optionally filter by position or status.</summary>
    [HttpGet]
    [Authorize]
    [RequirePermission("recruitment.applications", "view")]
    public async Task<IActionResult> GetAll([FromQuery] int? positionId, [FromQuery] string? status)
        => Ok(await svc.GetAllAsync(positionId, status));

    /// <summary>Public: submit a job application.</summary>
    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] SubmitJobApplicationRequest req)
    {
        try
        {
            var created = await svc.SubmitAsync(req);
            return StatusCode(201, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Admin: update application status.</summary>
    [HttpPatch("{id:int}/status")]
    [Authorize]
    [RequirePermission("recruitment.applications", "manage")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateApplicationStatusRequest req)
    {
        try
        {
            var updated = await svc.UpdateStatusAsync(id, req.Status);
            return updated == null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Admin: delete an application.</summary>
    [HttpDelete("{id:int}")]
    [Authorize]
    [RequirePermission("recruitment.applications", "manage")]
    public async Task<IActionResult> Delete(int id)
        => await svc.DeleteAsync(id) ? NoContent() : NotFound();
}
