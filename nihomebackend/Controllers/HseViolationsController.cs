using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Constants;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;
using NihomeBackend.Services.Audit;

namespace NihomeBackend.Controllers;

[ApiController]
[Route("api/operational-projects/{projectId:int}/hse-violations")]
[Route("api/v1/operational-projects/{projectId:int}/hse-violations")]
[Authorize]
public sealed class HseViolationsController(
    IHseViolationService service,
    IProjectAccessService access,
    IPermissionService permissions,
    IAuditLogger audit) : ControllerBase
{
    [HttpGet]
    [RequirePermission("construction.hse", "view")]
    public async Task<ActionResult<HseViolationListResponse>> List(
        int projectId,
        [FromQuery] HseViolationListParams parameters,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await access.CanViewOperationalProjectAsync(userId.Value, projectId, ct)) return NotFound();
        return Ok(await service.ListAsync(projectId, parameters, await CanViewSensitiveAsync(userId.Value, ct), ct));
    }

    [HttpGet("{id:int}")]
    [RequirePermission("construction.hse", "view")]
    public async Task<ActionResult<HseViolationResponse>> Get(int projectId, int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await access.CanViewOperationalProjectAsync(userId.Value, projectId, ct)) return NotFound();
        var result = await service.GetAsync(projectId, id, await CanViewSensitiveAsync(userId.Value, ct), ct);
        if (result is null) return NotFound();
        CrmConcurrency.SetResponseEntityTag(Response, result.RowVersion);
        return Ok(result);
    }

    [HttpPost]
    [RequirePermission("construction.hse", "manage")]
    [Idempotency("construction.hse.create", requireKey: true)]
    public async Task<ActionResult<HseViolationResponse>> Create(
        int projectId,
        [FromBody] CreateHseViolationRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await access.CanViewOperationalProjectAsync(userId.Value, projectId, ct)) return NotFound();
        return await ExecuteAsync(projectId, null, "hse-violation.create", async () =>
            await service.CreateAsync(projectId, request, userId.Value, ct), true);
    }

    [HttpPut("{id:int}")]
    [RequirePermission("construction.hse", "manage")]
    [Idempotency("construction.hse.update", requireKey: true)]
    public async Task<ActionResult<HseViolationResponse>> Update(
        int projectId,
        int id,
        [FromBody] UpdateHseViolationRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await access.CanViewOperationalProjectAsync(userId.Value, projectId, ct)) return NotFound();
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return await ExecuteAsync(projectId, id, "hse-violation.update", () =>
            service.UpdateAsync(projectId, id, request, userId.Value, ct));
    }

    [HttpPost("{id:int}/report")]
    [RequirePermission("construction.hse", "manage")]
    [Idempotency("construction.hse.report", requireKey: true)]
    public Task<ActionResult<HseViolationResponse>> Report(int projectId, int id, [FromBody] TransitionHseViolationRequest request, CancellationToken ct) =>
        Transition(projectId, id, HseViolationStatus.Reported, request, "hse-violation.report", ct);

    [HttpPost("{id:int}/confirm")]
    [RequirePermission("construction.hse", "confirm")]
    [Idempotency("construction.hse.confirm", requireKey: true)]
    public Task<ActionResult<HseViolationResponse>> Confirm(int projectId, int id, [FromBody] TransitionHseViolationRequest request, CancellationToken ct) =>
        Transition(projectId, id, HseViolationStatus.Confirmed, request, "hse-violation.confirm", ct);

    [HttpPost("{id:int}/reject")]
    [RequirePermission("construction.hse", "confirm")]
    [Idempotency("construction.hse.reject", requireKey: true)]
    public Task<ActionResult<HseViolationResponse>> Reject(int projectId, int id, [FromBody] TransitionHseViolationRequest request, CancellationToken ct) =>
        Transition(projectId, id, HseViolationStatus.Rejected, request, "hse-violation.reject", ct);

    [HttpPost("{id:int}/remediate")]
    [RequirePermission("construction.hse", "manage")]
    [Idempotency("construction.hse.remediate", requireKey: true)]
    public Task<ActionResult<HseViolationResponse>> Remediate(int projectId, int id, [FromBody] TransitionHseViolationRequest request, CancellationToken ct) =>
        Transition(projectId, id, HseViolationStatus.Remediated, request, "hse-violation.remediate", ct);

    [HttpPost("{id:int}/cancel")]
    [RequirePermission("construction.hse", "confirm")]
    [Idempotency("construction.hse.cancel", requireKey: true)]
    public Task<ActionResult<HseViolationResponse>> Cancel(int projectId, int id, [FromBody] TransitionHseViolationRequest request, CancellationToken ct) =>
        Transition(projectId, id, HseViolationStatus.Cancelled, request, "hse-violation.cancel", ct);

    [HttpPost("{id:int}/close")]
    [RequirePermission("construction.hse", "close")]
    [Idempotency("construction.hse.close", requireKey: true)]
    public Task<ActionResult<HseViolationResponse>> Close(int projectId, int id, [FromBody] TransitionHseViolationRequest request, CancellationToken ct) =>
        Transition(projectId, id, HseViolationStatus.Closed, request, "hse-violation.close", ct);

    [HttpPost("{id:int}/corrections")]
    [RequirePermission("construction.hse", "confirm")]
    [Idempotency("construction.hse.correct", requireKey: true)]
    public async Task<ActionResult<HseViolationResponse>> Correct(
        int projectId,
        int id,
        [FromBody] CorrectHseViolationRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await access.CanViewOperationalProjectAsync(userId.Value, projectId, ct)) return NotFound();
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return await ExecuteAsync(projectId, id, "hse-violation.correct", () =>
            service.CorrectAsync(projectId, id, request, userId.Value, ct));
    }

    private async Task<ActionResult<HseViolationResponse>> Transition(
        int projectId,
        int id,
        HseViolationStatus next,
        TransitionHseViolationRequest request,
        string action,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await access.CanViewOperationalProjectAsync(userId.Value, projectId, ct)) return NotFound();
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return await ExecuteAsync(projectId, id, action, () =>
            service.TransitionAsync(projectId, id, next, request, userId.Value, ct));
    }

    private async Task<ActionResult<HseViolationResponse>> ExecuteAsync(
        int projectId,
        int? id,
        string action,
        Func<Task<HseViolationResponse?>> operation,
        bool created = false)
    {
        try
        {
            var result = await operation();
            if (result is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = action,
                ResourceType = EntityTypes.HseViolation,
                ResourceId = result.Id.ToString(),
                Message = $"HSE violation {result.Code} changed in operational project #{projectId}.",
                NewValue = result,
            });
            CrmConcurrency.SetResponseEntityTag(Response, result.RowVersion);
            return created
                ? CreatedAtAction(nameof(Get), new { projectId, id = result.Id }, result)
                : Ok(result);
        }
        catch (HseViolationOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    private int? GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return int.TryParse(raw, out var id) ? id : null;
    }

    private async Task<bool> CanViewSensitiveAsync(int userId, CancellationToken ct) =>
        await permissions.HasAsync(userId, "construction.hse.manage", ct) ||
        await permissions.HasAsync(userId, "construction.hse.confirm", ct) ||
        await permissions.HasAsync(userId, "construction.hse.close", ct);
}