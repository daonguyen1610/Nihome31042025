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

/// <summary>
/// CRM Opportunity endpoints — supports list, detail, Kanban pipeline,
/// CRUD and dedicated stage transition. Owner scoping mirrors Lead/Customer:
/// <c>crm.opportunities.view.all</c> unlocks cross-owner view/edit, otherwise
/// callers see + mutate only the opportunities they own.
/// </summary>
[ApiController]
[Route("api/opportunities")]
[Route("api/v1/opportunities")]
[Authorize]
public class OpportunitiesController(
    IOpportunityService svc,
    IPermissionService permissions,
    IAuditLogger audit) : ControllerBase
{
    [HttpGet]
    [RequirePermission("crm.opportunities", "view")]
    public async Task<ActionResult<OpportunityListResponse>> List(
        [FromQuery] OpportunityStage? stage,
        [FromQuery] int? customerId,
        [FromQuery] int? ownerUserId,
        [FromQuery] DateTime? expectedCloseFrom,
        [FromQuery] DateTime? expectedCloseTo,
        [FromQuery] decimal? minValue,
        [FromQuery] decimal? maxValue,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.opportunities.view.all", ct);
        var result = await svc.ListAsync(
            userId.Value, canSeeAll, stage, customerId, ownerUserId,
            expectedCloseFrom, expectedCloseTo, minValue, maxValue, search, page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("pipeline")]
    [RequirePermission("crm.opportunities", "view")]
    public async Task<ActionResult<OpportunityPipelineResponse>> Pipeline(
        [FromQuery] int? ownerUserId,
        [FromQuery] int? customerId,
        [FromQuery] DateTime? expectedCloseFrom,
        [FromQuery] DateTime? expectedCloseTo,
        [FromQuery] decimal? minValue,
        [FromQuery] decimal? maxValue,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.opportunities.view.all", ct);
        var result = await svc.GetPipelineAsync(
            userId.Value, canSeeAll, ownerUserId, customerId,
            expectedCloseFrom, expectedCloseTo, minValue, maxValue, ct);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [RequirePermission("crm.opportunities", "view")]
    public async Task<ActionResult<OpportunityResponse>> Get(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.opportunities.view.all", ct);
        var found = await svc.GetAsync(id, userId.Value, canSeeAll, ct);
        return found is null ? NotFound() : Ok(found);
    }

    [HttpPost]
    [RequirePermission("crm.opportunities", "manage")]
    public async Task<ActionResult<OpportunityResponse>> Create(
        [FromBody] CreateOpportunityRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canManage = await permissions.HasAsync(userId.Value, "crm.opportunities.manage", ct);
        try
        {
            var response = await svc.CreateAsync(request, userId.Value, canManage, ct);
            audit.Log(new AuditEvent
            {
                Action = "opportunity.create",
                ResourceType = EntityTypes.Opportunity,
                ResourceId = response.Id.ToString(),
                Message = $"Opportunity #{response.Id} '{response.Name}' created.",
                NewValue = response,
            });
            return CreatedAtAction(nameof(Get), new { id = response.Id }, response);
        }
        catch (OpportunityOperationException ex)
        {
            audit.Log(new AuditEvent
            {
                Action = "opportunity.create",
                ResourceType = EntityTypes.Opportunity,
                Message = ex.Message,
                Status = AuditStatus.Failure,
                FailureReason = ex.Message,
            });
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [RequirePermission("crm.opportunities", "manage")]
    public async Task<ActionResult<OpportunityResponse>> Update(
        int id,
        [FromBody] UpdateOpportunityRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.opportunities.view.all", ct);
        var canManage = await permissions.HasAsync(userId.Value, "crm.opportunities.manage", ct);
        try
        {
            var response = await svc.UpdateAsync(id, request, userId.Value, canManage, canSeeAll, ct);
            if (response is null) return NotFound();

            audit.Log(new AuditEvent
            {
                Action = "opportunity.update",
                ResourceType = EntityTypes.Opportunity,
                ResourceId = id.ToString(),
                Message = $"Opportunity #{id} updated.",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (OpportunityOperationException ex)
        {
            audit.Log(new AuditEvent
            {
                Action = "opportunity.update",
                ResourceType = EntityTypes.Opportunity,
                ResourceId = id.ToString(),
                Message = ex.Message,
                Status = AuditStatus.Failure,
                FailureReason = ex.Message,
            });
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("{id:int}/stage")]
    [RequirePermission("crm.opportunities", "manage")]
    public async Task<ActionResult<OpportunityResponse>> ChangeStage(
        int id,
        [FromBody] ChangeOpportunityStageRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.opportunities.view.all", ct);
        var canManage = await permissions.HasAsync(userId.Value, "crm.opportunities.manage", ct);
        try
        {
            var response = await svc.ChangeStageAsync(id, request, userId.Value, canManage, canSeeAll, ct);
            if (response is null) return NotFound();

            audit.Log(new AuditEvent
            {
                Action = "opportunity.stage-change",
                ResourceType = EntityTypes.Opportunity,
                ResourceId = id.ToString(),
                Message = $"Opportunity #{id} stage → {response.Stage}.",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (OpportunityOperationException ex)
        {
            audit.Log(new AuditEvent
            {
                Action = "opportunity.stage-change",
                ResourceType = EntityTypes.Opportunity,
                ResourceId = id.ToString(),
                Message = ex.Message,
                Status = AuditStatus.Failure,
                FailureReason = ex.Message,
            });
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("crm.opportunities", "manage")]
    public async Task<ActionResult> Delete(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canManage = await permissions.HasAsync(userId.Value, "crm.opportunities.manage", ct);
        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.opportunities.view.all", ct);
        try
        {
            var removed = await svc.DeleteAsync(id, userId.Value, canManage, canSeeAll, ct);
            if (!removed) return NotFound();

            audit.Log(new AuditEvent
            {
                Action = "opportunity.delete",
                ResourceType = EntityTypes.Opportunity,
                ResourceId = id.ToString(),
                Message = $"Opportunity #{id} deleted.",
            });
            return NoContent();
        }
        catch (OpportunityOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/activities")]
    [RequirePermission("crm.opportunities", "manage")]
    public async Task<ActionResult<OpportunityActivityResponse>> AddActivity(
        int id,
        [FromBody] AddOpportunityActivityRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.opportunities.view.all", ct);
        var response = await svc.AddActivityAsync(id, request, userId.Value, canSeeAll, ct);
        if (response is null) return NotFound();

        audit.Log(new AuditEvent
        {
            Action = "opportunity.activity.create",
            ResourceType = EntityTypes.OpportunityActivity,
            ResourceId = response.Id.ToString(),
            Message = $"Activity added to opportunity #{id}.",
        });
        return Ok(response);
    }

    private int? GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var id) ? id : null;
    }
}
