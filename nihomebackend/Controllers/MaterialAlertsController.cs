using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

[ApiController]
[Route("api/operational-projects/{projectId:int}/procurement/material-alerts")]
[Route("api/v1/operational-projects/{projectId:int}/procurement/material-alerts")]
[Authorize]
public sealed class MaterialAlertsController(
    IMaterialAlertService service,
    IProjectAccessService access) : ControllerBase
{
    [HttpGet]
    [RequirePermission("proc.material-alerts", "view")]
    public async Task<ActionResult<MaterialAlertListResponse>> List(
        int projectId,
        [FromQuery] MaterialAlertListParams parameters,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await access.CanViewOperationalProjectAsync(userId.Value, projectId, ct)) return NotFound();
        return Ok(await service.ListAsync(projectId, parameters, ct));
    }

    [HttpGet("{id:int}")]
    [RequirePermission("proc.material-alerts", "view")]
    public async Task<ActionResult<MaterialAlertResponse>> Get(
        int projectId,
        int id,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await access.CanViewOperationalProjectAsync(userId.Value, projectId, ct)) return NotFound();
        var result = await service.GetAsync(projectId, id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("evaluate")]
    [RequirePermission("proc.material-alerts", "manage")]
    [Idempotency("proc.material-alerts.evaluate", requireKey: true)]
    public async Task<ActionResult<IReadOnlyList<MaterialAlertResponse>>> Evaluate(
        int projectId,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await access.CanViewOperationalProjectAsync(userId.Value, projectId, ct)) return NotFound();
        try { return Ok(await service.EvaluateProjectAsync(projectId, userId.Value, ct)); }
        catch (MaterialAlertOperationException exception) { return BadRequest(new { message = exception.Message }); }
    }

    [HttpPost("{id:int}/acknowledge")]
    [RequirePermission("proc.material-alerts", "manage")]
    [Idempotency("proc.material-alerts.acknowledge", requireKey: true)]
    public async Task<ActionResult<MaterialAlertResponse>> Acknowledge(
        int projectId,
        int id,
        [FromBody] AcknowledgeMaterialAlertRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await access.CanViewOperationalProjectAsync(userId.Value, projectId, ct)) return NotFound();
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        try
        {
            var result = await service.AcknowledgeAsync(projectId, id, request, userId.Value, ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (MaterialAlertOperationException exception) { return BadRequest(new { message = exception.Message }); }
    }

    private int? GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return int.TryParse(raw, out var id) ? id : null;
    }
}
