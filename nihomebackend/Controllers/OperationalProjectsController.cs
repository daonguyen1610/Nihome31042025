using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Constants;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;
using NihomeBackend.Services.Audit;

namespace NihomeBackend.Controllers;

/// <summary>Central NICON project endpoints shared by operational modules.</summary>
[ApiController]
[Route("api/operational-projects")]
[Route("api/v1/operational-projects")]
[Authorize]
public class OperationalProjectsController(
    IOperationalProjectService service,
    IPermissionService permissions,
    IAuditLogger audit) : ControllerBase
{
    [HttpGet]
    [RequirePermission("operations.projects", "view")]
    public async Task<ActionResult<OperationalProjectListResponse>> List(
        [FromQuery] OperationalProjectListParams parameters,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var canSeeAll = await permissions.HasAsync(userId.Value, "operations.projects.view.all", ct);
        return Ok(await service.ListAsync(parameters, userId.Value, canSeeAll, ct));
    }

    [HttpGet("{id:int}")]
    [RequirePermission("operations.projects", "view")]
    public async Task<ActionResult<OperationalProjectResponse>> Get(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var canSeeAll = await permissions.HasAsync(userId.Value, "operations.projects.view.all", ct);
        var result = await service.GetAsync(id, userId.Value, canSeeAll, ct);
        if (result is null) return NotFound();
        CrmConcurrency.SetResponseEntityTag(Response, result.RowVersion);
        return Ok(result);
    }

    [HttpPost]
    [RequirePermission("operations.projects", "manage")]
    [Idempotency("operations.projects.create")]
    public async Task<ActionResult<OperationalProjectResponse>> Create(
        [FromBody] CreateOperationalProjectRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var canSeeAll = await permissions.HasAsync(userId.Value, "operations.projects.view.all", ct);
        try
        {
            var result = await service.CreateAsync(request, userId.Value, canSeeAll, ct);
            audit.Log(new AuditEvent
            {
                Action = "operational-project.create",
                ResourceType = EntityTypes.OperationalProject,
                ResourceId = result.Id.ToString(),
                Message = $"Operational project #{result.Id} ({result.Code}) created.",
                NewValue = result,
            });
            CrmConcurrency.SetResponseEntityTag(Response, result.RowVersion);
            return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
        }
        catch (OperationalProjectOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [RequirePermission("operations.projects", "manage")]
    [Idempotency("operations.projects.update")]
    public async Task<ActionResult<OperationalProjectResponse>> Update(
        int id,
        [FromBody] UpdateOperationalProjectRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var canSeeAll = await permissions.HasAsync(userId.Value, "operations.projects.view.all", ct);
        request.RowVersion = CrmConcurrency.ResolveRequestToken(Request, request.RowVersion);
        try
        {
            var result = await service.UpdateAsync(id, request, userId.Value, canSeeAll, ct);
            if (result is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "operational-project.update",
                ResourceType = EntityTypes.OperationalProject,
                ResourceId = id.ToString(),
                Message = $"Operational project #{id} updated.",
                NewValue = result,
            });
            CrmConcurrency.SetResponseEntityTag(Response, result.RowVersion);
            return Ok(result);
        }
        catch (OperationalProjectOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("operations.projects", "manage")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var canSeeAll = await permissions.HasAsync(userId.Value, "operations.projects.view.all", ct);
        try
        {
            var deleted = await service.DeleteAsync(
                id,
                userId.Value,
                canSeeAll,
                CrmConcurrency.ResolveRequestToken(Request, null),
                ct);
            if (!deleted) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "operational-project.delete",
                ResourceType = EntityTypes.OperationalProject,
                ResourceId = id.ToString(),
                Message = $"Operational project #{id} deleted.",
            });
            return NoContent();
        }
        catch (OperationalProjectOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private int? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return int.TryParse(value, out var id) ? id : null;
    }
}
