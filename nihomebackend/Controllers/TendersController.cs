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

/// <summary>
/// CRM Tender (Gói thầu) endpoints — list / detail / CRUD. Result
/// transition workflow (mark won / lost + auto-create contract) ships
/// with the detail slice (NIH-97).
/// </summary>
[ApiController]
[Route("api/tenders")]
[Route("api/v1/tenders")]
[Authorize]
public class TendersController(
    ITenderService svc,
    IAuditLogger audit) : ControllerBase
{
    [HttpGet]
    [RequirePermission("crm.tenders", "view")]
    public async Task<ActionResult<TenderListResponse>> List([FromQuery] TenderListParams parameters, CancellationToken ct)
    {
        var result = await svc.ListAsync(parameters, ct);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [RequirePermission("crm.tenders", "view")]
    public async Task<ActionResult<TenderResponse>> Get(int id, CancellationToken ct)
    {
        var found = await svc.GetAsync(id, ct);
        return found is null ? NotFound() : Ok(found);
    }

    [HttpPost]
    [RequirePermission("crm.tenders", "manage")]
    public async Task<ActionResult<TenderResponse>> Create([FromBody] CreateTenderRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.CreateAsync(request, userId.Value, ct);
            audit.Log(new AuditEvent
            {
                Action = "tender.create",
                ResourceType = EntityTypes.Tender,
                ResourceId = response.Id.ToString(),
                Message = $"Tender #{response.Id} ({response.Code}) created.",
                NewValue = response,
            });
            return CreatedAtAction(nameof(Get), new { id = response.Id }, response);
        }
        catch (TenderOperationException ex)
        {
            return LogAndBadRequest("tender.create", ex);
        }
    }

    [HttpPut("{id:int}")]
    [RequirePermission("crm.tenders", "manage")]
    public async Task<ActionResult<TenderResponse>> Update(int id, [FromBody] UpdateTenderRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.UpdateAsync(id, request, userId.Value, ct);
            if (response is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "tender.update",
                ResourceType = EntityTypes.Tender,
                ResourceId = id.ToString(),
                Message = $"Tender #{id} updated (status={response.Status}).",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (TenderOperationException ex)
        {
            return LogAndBadRequest("tender.update", ex, id);
        }
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("crm.tenders", "manage")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            var ok = await svc.DeleteAsync(id, ct);
            if (!ok) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "tender.delete",
                ResourceType = EntityTypes.Tender,
                ResourceId = id.ToString(),
                Message = $"Tender #{id} deleted.",
            });
            return NoContent();
        }
        catch (TenderOperationException ex)
        {
            audit.Log(new AuditEvent
            {
                Action = "tender.delete",
                ResourceType = EntityTypes.Tender,
                ResourceId = id.ToString(),
                Message = ex.Message,
                Status = AuditStatus.Failure,
                FailureReason = ex.Message,
            });
            return BadRequest(new { message = ex.Message });
        }
    }

    // ---------- helpers ----------

    private ActionResult<TenderResponse> LogAndBadRequest(string action, TenderOperationException ex, int? id = null)
    {
        audit.Log(new AuditEvent
        {
            Action = action,
            ResourceType = EntityTypes.Tender,
            ResourceId = id?.ToString(),
            Message = ex.Message,
            Status = AuditStatus.Failure,
            FailureReason = ex.Message,
        });
        return BadRequest(new { message = ex.Message });
    }

    private int? GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return int.TryParse(raw, out var id) ? id : null;
    }
}
