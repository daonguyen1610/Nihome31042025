using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

/// <summary>
/// CRUD endpoints for approval workflow definitions. See <c>NIH-225</c> —
/// scope is limited to configuration; runtime evaluation is out of scope.
/// </summary>
[ApiController]
[Route("api/workflows")]
[Route("api/v1/workflows")]
[Authorize]
public class WorkflowsController(IWorkflowConfigService svc) : ControllerBase
{
    [HttpGet]
    [RequirePermission("workflow", "view")]
    public async Task<ActionResult<List<WorkflowConfigResponse>>> List(
        [FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => Ok(await svc.ListAsync(includeInactive, ct));

    [HttpGet("{id:int}")]
    [RequirePermission("workflow", "view")]
    public async Task<ActionResult<WorkflowConfigResponse>> GetById(int id, CancellationToken ct)
    {
        var found = await svc.GetByIdAsync(id, ct);
        return found == null ? NotFound() : Ok(found);
    }

    [HttpPost]
    [RequirePermission("workflow", "manage")]
    public async Task<ActionResult<WorkflowConfigResponse>> Create(
        [FromBody] UpsertWorkflowConfigRequest req, CancellationToken ct)
    {
        try
        {
            var created = await svc.CreateAsync(req, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (WorkflowConfigDuplicateException ex)
        {
            return Conflict(new { message = ex.Message, module = ex.Module, action = ex.Action });
        }
        catch (WorkflowConfigValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [RequirePermission("workflow", "manage")]
    public async Task<ActionResult<WorkflowConfigResponse>> Update(
        int id, [FromBody] UpsertWorkflowConfigRequest req, CancellationToken ct)
    {
        try
        {
            var updated = await svc.UpdateAsync(id, req, ct);
            return updated == null ? NotFound() : Ok(updated);
        }
        catch (WorkflowConfigDuplicateException ex)
        {
            return Conflict(new { message = ex.Message, module = ex.Module, action = ex.Action });
        }
        catch (WorkflowConfigValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("workflow", "manage")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => await svc.DeleteAsync(id, ct) ? NoContent() : NotFound();
}
