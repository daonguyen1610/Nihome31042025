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
using NihomeBackend.Services.HardDelete;

namespace NihomeBackend.Controllers;

/// <summary>
/// CRM Lead endpoints — first stage of the Sales funnel.
///
/// * Sales users (<c>crm.leads.view</c>) see and edit only leads assigned to
///   themselves.
/// * Sales Manager / Admin (<c>crm.leads.view.all</c>) see and manage every
///   lead, may reassign owners and may transition leads to
///   <c>NotInterested</c> / <c>Junk</c>.
/// * Conversion to Customer + Opportunity requires <c>crm.leads.convert</c>.
/// </summary>
[ApiController]
[Route("api/leads")]
[Route("api/v1/leads")]
[Authorize]
public class LeadsController(
    ILeadService svc,
    IPermissionService permissions,
    IAuditLogger audit) : ControllerBase
{
    [HttpGet]
    [RequirePermission("crm.leads", "view")]
    public async Task<ActionResult<LeadListResponse>> List(
        [FromQuery] LeadStatus? status,
        [FromQuery] string? sourceCode,
        [FromQuery] string? segmentCode,
        [FromQuery] int? ownerUserId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.leads.view.all", ct);
        var result = await svc.ListAsync(userId.Value, canSeeAll, status, sourceCode, segmentCode, ownerUserId, search, page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [RequirePermission("crm.leads", "view")]
    public async Task<ActionResult<LeadResponse>> Get(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.leads.view.all", ct);
        var found = await svc.GetAsync(id, userId.Value, canSeeAll, ct);
        if (found is null) return NotFound();
        CrmConcurrency.SetResponseEntityTag(Response, found.RowVersion);
        return Ok(found);
    }

    [HttpPost]
    [RequirePermission("crm.leads", "manage")]
    [Idempotency("crm.leads.create")]
    public async Task<ActionResult<LeadResponse>> Create(
        [FromBody] CreateLeadRequest request,
        [FromHeader(Name = "Accept-Language")] string? languageHeader,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        // Belt-and-braces: [RequirePermission] already gated the request,
        // but re-check so the service enforces the same rule if this method
        // is ever exercised without the attribute.
        var canManage = await permissions.HasAsync(userId.Value, "crm.leads.manage", ct);

        try
        {
            var response = await svc.CreateAsync(request, userId.Value, canManage, ResolveLanguage(languageHeader), ct);
            audit.Log(new AuditEvent
            {
                Action = "lead.create",
                ResourceType = EntityTypes.Lead,
                ResourceId = response.Id.ToString(),
                Message = $"Lead #{response.Id} '{response.Name}' created.",
                NewValue = response,
            });
            CrmConcurrency.SetResponseEntityTag(Response, response.RowVersion);
            return CreatedAtAction(nameof(Get), new { id = response.Id }, response);
        }
        catch (LeadOperationException ex)
        {
            audit.Log(new AuditEvent
            {
                Action = "lead.create",
                ResourceType = EntityTypes.Lead,
                Message = ex.Message,
                Status = AuditStatus.Failure,
                FailureReason = ex.Message,
            });
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [RequirePermission("crm.leads", "manage")]
    [Idempotency("crm.leads.update")]
    public async Task<ActionResult<LeadResponse>> Update(
        int id,
        [FromBody] UpdateLeadRequest request,
        [FromHeader(Name = "Accept-Language")] string? languageHeader,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.leads.view.all", ct);
        var canManage = await permissions.HasAsync(userId.Value, "crm.leads.manage", ct);
        request.RowVersion = CrmConcurrency.ResolveRequestToken(Request, request.RowVersion);

        try
        {
            var response = await svc.UpdateAsync(id, request, userId.Value, canManage, canSeeAll, ResolveLanguage(languageHeader), ct);
            if (response is null) return NotFound();

            audit.Log(new AuditEvent
            {
                Action = "lead.update",
                ResourceType = EntityTypes.Lead,
                ResourceId = id.ToString(),
                Message = $"Lead #{id} updated.",
                NewValue = response,
            });
            CrmConcurrency.SetResponseEntityTag(Response, response.RowVersion);
            return Ok(response);
        }
        catch (LeadOperationException ex)
        {
            audit.Log(new AuditEvent
            {
                Action = "lead.update",
                ResourceType = EntityTypes.Lead,
                ResourceId = id.ToString(),
                Message = ex.Message,
                Status = AuditStatus.Failure,
                FailureReason = ex.Message,
            });
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("crm.leads", "manage")]
    public async Task<IActionResult> Delete(
        int id,
        [FromBody] ConfirmDeletionRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canManage = await permissions.HasAsync(userId.Value, "crm.leads.manage", ct);
        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.leads.view.all", ct);

        try
        {
            request.RowVersion = CrmConcurrency.ResolveRequestToken(Request, request.RowVersion);
            var result = await svc.DeleteAsync(id, request, userId.Value, canManage, canSeeAll, ct);
            if (result is null) return NotFound();
            return result.IsComplete ? NoContent() : AcceptedOperation(result);
        }
        catch (LeadOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (DeletionPlanChangedException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (HardDeleteOperationConflictException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("{id:int}/deletion-impact")]
    [RequirePermission("crm.leads", "manage")]
    public async Task<ActionResult<DeletionImpactResponse>> GetDeletionImpact(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var canManage = await permissions.HasAsync(userId.Value, "crm.leads.manage", ct);
        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.leads.view.all", ct);
        var impact = await svc.GetDeletionImpactAsync(id, userId.Value, canManage, canSeeAll, ct);
        return impact is null ? NotFound() : Ok(impact);
    }

    [HttpPost("{id:int}/convert")]
    [RequirePermission("crm.leads", "convert")]
    [Idempotency("crm.leads.convert")]
    public async Task<ActionResult<LeadResponse>> Convert(
        int id,
        [FromBody] ConvertLeadRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canConvert = await permissions.HasAsync(userId.Value, "crm.leads.convert", ct);
        request.RowVersion = CrmConcurrency.ResolveRequestToken(Request, request.RowVersion);

        try
        {
            var response = await svc.ConvertAsync(id, request, userId.Value, canConvert, ct);
            if (response is null) return NotFound();

            audit.Log(new AuditEvent
            {
                Action = "lead.convert",
                ResourceType = EntityTypes.Lead,
                ResourceId = id.ToString(),
                Message = $"Lead #{id} converted (customerId={request.CustomerId}, opportunityId={request.OpportunityId}).",
                NewValue = response,
            });
            CrmConcurrency.SetResponseEntityTag(Response, response.RowVersion);
            return Ok(response);
        }
        catch (CustomerDuplicateException ex)
        {
            // Convert reuses CustomerService's duplicate rule, so it answers 409
            // with the conflicting record exactly like the create-customer path.
            audit.Log(new AuditEvent
            {
                Action = "lead.convert",
                ResourceType = EntityTypes.Lead,
                ResourceId = id.ToString(),
                Message = ex.Detail.Message,
                Status = AuditStatus.Failure,
                FailureReason = ex.Detail.Message,
            });
            return Conflict(ex.Detail);
        }
        catch (LeadOperationException ex)
        {
            audit.Log(new AuditEvent
            {
                Action = "lead.convert",
                ResourceType = EntityTypes.Lead,
                ResourceId = id.ToString(),
                Message = ex.Message,
                Status = AuditStatus.Failure,
                FailureReason = ex.Message,
            });
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/activities")]
    [RequirePermission("crm.leads", "manage")]
    public async Task<ActionResult<LeadActivityResponse>> AddActivity(
        int id,
        [FromBody] CreateLeadActivityRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.leads.view.all", ct);
        var response = await svc.AddActivityAsync(id, request, userId.Value, canSeeAll, ct);
        if (response is null) return NotFound();

        audit.Log(new AuditEvent
        {
            Action = "lead.activity.create",
            ResourceType = EntityTypes.LeadActivity,
            ResourceId = response.Id.ToString(),
            Message = $"Activity added to lead #{id} ({response.Type}).",
        });
        return CreatedAtAction(nameof(Get), new { id }, response);
    }

    /// <summary>
    /// Undoes a conversion. Three outcomes — see spec A2: both records deleted,
    /// only the opportunity deleted, or the link removed with both kept.
    /// </summary>
    [HttpPost("{id:int}/unconvert")]
    [RequirePermission("crm.leads", "convert")]
    [Idempotency("crm.leads.unconvert")]
    public async Task<ActionResult<UnconvertLeadResponse>> Unconvert(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canConvert = await permissions.HasAsync(userId.Value, "crm.leads.convert", ct);

        try
        {
            var response = await svc.UnconvertAsync(
                id, userId.Value, canConvert, ct,
                CrmConcurrency.ResolveRequestToken(Request, null));
            if (response is null) return NotFound();

            audit.Log(new AuditEvent
            {
                Action = "lead.unconvert",
                ResourceType = EntityTypes.Lead,
                ResourceId = id.ToString(),
                Message = $"Lead #{id} unconverted (outcome={response.Outcome}).",
                NewValue = response,
            });
            CrmConcurrency.SetResponseEntityTag(Response, response.Lead.RowVersion);
            return Ok(response);
        }
        catch (LeadOperationException ex)
        {
            audit.Log(new AuditEvent
            {
                Action = "lead.unconvert",
                ResourceType = EntityTypes.Lead,
                ResourceId = id.ToString(),
                Message = ex.Message,
                Status = AuditStatus.Failure,
                FailureReason = ex.Message,
            });
            return BadRequest(new { message = ex.Message });
        }
    }


    private int? GetUserId()
    {
        var principal = HttpContext?.User;
        if (principal == null) return null;

        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("uid");

        return int.TryParse(value, out var uid) ? uid : null;
    }

    private static string ResolveLanguage(string? header)
    {
        if (string.IsNullOrWhiteSpace(header)) return "vi";
        var primary = header.Split(',', StringSplitOptions.TrimEntries)[0];
        var code = primary.Split('-', StringSplitOptions.TrimEntries)[0].ToLowerInvariant();
        return code switch
        {
            "en" or "vi" or "zh" or "ja" => code,
            _ => "vi",
        };
    }

    private IActionResult AcceptedOperation(HardDeleteOperationResult result)
    {
        Response.Headers.Location = Url.Action(
            nameof(HardDeleteOperationsController.GetStatus),
            "HardDeleteOperations",
            new { operationId = result.OperationId })!;
        return StatusCode(StatusCodes.Status202Accepted, result);
    }
}
