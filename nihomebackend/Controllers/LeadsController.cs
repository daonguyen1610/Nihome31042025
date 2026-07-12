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
        [FromQuery] int? ownerUserId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.leads.view.all", ct);
        var result = await svc.ListAsync(userId.Value, canSeeAll, status, sourceCode, ownerUserId, search, page, pageSize, ct);
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
        return found is null ? NotFound() : Ok(found);
    }

    [HttpPost]
    [RequirePermission("crm.leads", "manage")]
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
    public async Task<ActionResult> Delete(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canManage = await permissions.HasAsync(userId.Value, "crm.leads.manage", ct);

        try
        {
            var removed = await svc.DeleteAsync(id, userId.Value, canManage, ct);
            if (!removed) return NotFound();

            audit.Log(new AuditEvent
            {
                Action = "lead.delete",
                ResourceType = EntityTypes.Lead,
                ResourceId = id.ToString(),
                Message = $"Lead #{id} deleted.",
            });
            return NoContent();
        }
        catch (LeadOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/convert")]
    [RequirePermission("crm.leads", "convert")]
    public async Task<ActionResult<LeadResponse>> Convert(
        int id,
        [FromBody] ConvertLeadRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canConvert = await permissions.HasAsync(userId.Value, "crm.leads.convert", ct);

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
            return Ok(response);
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
}
