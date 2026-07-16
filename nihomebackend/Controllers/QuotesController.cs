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
/// CRM Quote (báo giá) endpoints — list/detail, CRUD, and dedicated
/// workflow actions. Owner scoping mirrors <c>OpportunitiesController</c>:
/// <c>crm.quotes.view.all</c> unlocks cross-owner view/edit; without it,
/// callers see + mutate only quotes they own.
/// </summary>
[ApiController]
[Route("api/quotes")]
[Route("api/v1/quotes")]
[Authorize]
public class QuotesController(
    IQuoteService svc,
    IPermissionService permissions,
    IAuditLogger audit) : ControllerBase
{
    [HttpGet]
    [RequirePermission("crm.quotes", "view")]
    public async Task<ActionResult<QuoteListResponse>> List(
        [FromQuery] QuoteStatus? status,
        [FromQuery] int? opportunityId,
        [FromQuery] int? customerId,
        [FromQuery] int? ownerUserId,
        [FromQuery] DateTime? createdFrom,
        [FromQuery] DateTime? createdTo,
        [FromQuery] decimal? minValue,
        [FromQuery] decimal? maxValue,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.quotes.view.all", ct);
        var result = await svc.ListAsync(userId.Value, canSeeAll, status, opportunityId, customerId,
            ownerUserId, createdFrom, createdTo, minValue, maxValue, search, page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [RequirePermission("crm.quotes", "view")]
    public async Task<ActionResult<QuoteResponse>> Get(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.quotes.view.all", ct);
        var found = await svc.GetAsync(id, userId.Value, canSeeAll, ct);
        return found is null ? NotFound() : Ok(found);
    }

    [HttpGet("{id:int}/versions")]
    [RequirePermission("crm.quotes", "view")]
    public async Task<ActionResult<QuoteVersionsResponse>> Versions(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.quotes.view.all", ct);
        var found = await svc.GetVersionsAsync(id, userId.Value, canSeeAll, ct);
        return found is null ? NotFound() : Ok(found);
    }

    [HttpPost]
    [RequirePermission("crm.quotes", "manage")]
    public async Task<ActionResult<QuoteResponse>> Create([FromBody] CreateQuoteRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var canManage = await permissions.HasAsync(userId.Value, "crm.quotes.manage", ct);
            var response = await svc.CreateAsync(request, userId.Value, canManage, ct);
            audit.Log(new AuditEvent
            {
                Action = "quote.create",
                ResourceType = EntityTypes.Quote,
                ResourceId = response.Id.ToString(),
                Message = $"Quote #{response.Id} ({response.Code}) created.",
                NewValue = response,
            });
            return CreatedAtAction(nameof(Get), new { id = response.Id }, response);
        }
        catch (QuoteOperationException ex)
        {
            return LogAndBadRequest("quote.create", ex);
        }
    }

    [HttpPut("{id:int}")]
    [RequirePermission("crm.quotes", "manage")]
    public async Task<ActionResult<QuoteResponse>> Update(int id, [FromBody] UpdateQuoteRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var canManage = await permissions.HasAsync(userId.Value, "crm.quotes.manage", ct);
            var canSeeAll = await permissions.HasAsync(userId.Value, "crm.quotes.view.all", ct);
            var response = await svc.UpdateAsync(id, request, userId.Value, canManage, canSeeAll, ct);
            if (response is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "quote.update",
                ResourceType = EntityTypes.Quote,
                ResourceId = id.ToString(),
                Message = $"Quote #{id} updated (V{response.Version}).",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (QuoteOperationException ex)
        {
            return LogAndBadRequest("quote.update", ex, id);
        }
    }

    [HttpPost("{id:int}/submit")]
    [RequirePermission("crm.quotes", "manage")]
    public Task<ActionResult<QuoteResponse>> Submit(int id, [FromBody] QuoteWorkflowRequest body, CancellationToken ct) =>
        Workflow(id, body, ct, "submit",
            async (uid, sa) => await svc.SubmitAsync(id, body, uid,
                await permissions.HasAsync(uid, "crm.quotes.manage", ct), sa, ct));

    [HttpPost("{id:int}/approve")]
    [RequirePermission("crm.quotes", "approve")]
    public Task<ActionResult<QuoteResponse>> Approve(int id, [FromBody] QuoteWorkflowRequest body, CancellationToken ct) =>
        Workflow(id, body, ct, "approve",
            async (uid, _) => await svc.ApproveAsync(id, body, uid,
                await permissions.HasAsync(uid, "crm.quotes.approve", ct), ct));

    [HttpPost("{id:int}/reject-internal")]
    [RequirePermission("crm.quotes", "approve")]
    public Task<ActionResult<QuoteResponse>> RejectInternal(int id, [FromBody] QuoteWorkflowRequest body, CancellationToken ct) =>
        Workflow(id, body, ct, "reject-internal",
            async (uid, _) => await svc.RejectInternalAsync(id, body, uid,
                await permissions.HasAsync(uid, "crm.quotes.approve", ct), ct));

    [HttpPost("{id:int}/send")]
    [RequirePermission("crm.quotes", "send")]
    public Task<ActionResult<QuoteResponse>> Send(int id, [FromBody] QuoteWorkflowRequest body, CancellationToken ct) =>
        Workflow(id, body, ct, "send",
            async (uid, sa) => await svc.SendToCustomerAsync(id, body, uid,
                await permissions.HasAsync(uid, "crm.quotes.send", ct), sa, ct));

    [HttpPost("{id:int}/customer-approve")]
    [RequirePermission("crm.quotes", "manage")]
    public Task<ActionResult<QuoteResponse>> CustomerApprove(int id, [FromBody] QuoteWorkflowRequest body, CancellationToken ct) =>
        Workflow(id, body, ct, "customer-approve",
            async (uid, sa) => await svc.MarkCustomerApprovedAsync(id, body, uid,
                await permissions.HasAsync(uid, "crm.quotes.manage", ct), sa, ct));

    [HttpPost("{id:int}/customer-reject")]
    [RequirePermission("crm.quotes", "manage")]
    public Task<ActionResult<QuoteResponse>> CustomerReject(int id, [FromBody] QuoteWorkflowRequest body, CancellationToken ct) =>
        Workflow(id, body, ct, "customer-reject",
            async (uid, sa) => await svc.MarkCustomerRejectedAsync(id, body, uid,
                await permissions.HasAsync(uid, "crm.quotes.manage", ct), sa, ct));

    [HttpPost("{id:int}/cancel")]
    [RequirePermission("crm.quotes", "manage")]
    public Task<ActionResult<QuoteResponse>> Cancel(int id, [FromBody] QuoteWorkflowRequest body, CancellationToken ct) =>
        Workflow(id, body, ct, "cancel",
            async (uid, sa) => await svc.CancelAsync(id, body, uid,
                await permissions.HasAsync(uid, "crm.quotes.manage", ct), sa, ct));

    [HttpPost("{id:int}/extend-validity")]
    [RequirePermission("crm.quotes", "approve")]
    public async Task<ActionResult<QuoteResponse>> ExtendValidity(int id, [FromBody] ExtendQuoteValidityRequest body, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var canApprove = await permissions.HasAsync(userId.Value, "crm.quotes.approve", ct);
            var response = await svc.ExtendValidityAsync(id, body, userId.Value, canApprove, ct);
            if (response is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "quote.extend-validity",
                ResourceType = EntityTypes.Quote,
                ResourceId = id.ToString(),
                Message = $"Quote #{id} valid-until extended to {body.NewValidUntil:o}.",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (QuoteOperationException ex)
        {
            return LogAndBadRequest("quote.extend-validity", ex, id);
        }
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("crm.quotes", "manage")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var canManage = await permissions.HasAsync(userId.Value, "crm.quotes.manage", ct);
            var canSeeAll = await permissions.HasAsync(userId.Value, "crm.quotes.view.all", ct);
            var ok = await svc.DeleteAsync(id, userId.Value, canManage, canSeeAll, ct);
            if (!ok) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "quote.delete",
                ResourceType = EntityTypes.Quote,
                ResourceId = id.ToString(),
                Message = $"Quote #{id} deleted.",
            });
            return NoContent();
        }
        catch (QuoteOperationException ex)
        {
            return LogAndBadRequest("quote.delete", ex, id).Result!;
        }
    }

    // ---------- helpers ----------

    private async Task<ActionResult<QuoteResponse>> Workflow(
        int id,
        QuoteWorkflowRequest body,
        CancellationToken ct,
        string action,
        Func<int, bool, Task<QuoteResponse?>> run)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var canSeeAll = await permissions.HasAsync(userId.Value, "crm.quotes.view.all", ct);
            var response = await run(userId.Value, canSeeAll);
            if (response is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = $"quote.{action}",
                ResourceType = EntityTypes.Quote,
                ResourceId = id.ToString(),
                Message = $"Quote #{id} {action} — status={response.Status}.",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (QuoteOperationException ex)
        {
            return LogAndBadRequest($"quote.{action}", ex, id);
        }
    }

    private ActionResult<QuoteResponse> LogAndBadRequest(string action, QuoteOperationException ex, int? id = null)
    {
        audit.Log(new AuditEvent
        {
            Action = action,
            ResourceType = EntityTypes.Quote,
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
