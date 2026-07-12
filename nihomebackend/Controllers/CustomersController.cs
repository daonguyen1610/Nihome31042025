using System.Net;
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
/// CRM Customer endpoints.
///
/// * Sales users (<c>crm.customers.view</c> + <c>crm.customers.manage</c>)
///   see + edit only customers they own.
/// * Sales Manager / Accountant / BOD / Admin (<c>crm.customers.view.all</c>)
///   see everything and can reassign owners / suspend customers.
/// * Duplicate detection: TaxId (Company) or primary Phone (Individual) —
///   409 with a <see cref="CustomerDuplicateResponse"/> payload unless a
///   <c>DuplicateOverrideReason</c> is supplied (audit-logged).
/// </summary>
[ApiController]
[Route("api/customers")]
[Route("api/v1/customers")]
[Authorize]
public class CustomersController(
    ICustomerService svc,
    IPermissionService permissions,
    IAuditLogger audit) : ControllerBase
{
    [HttpGet]
    [RequirePermission("crm.customers", "view")]
    public async Task<ActionResult<CustomerListResponse>> List(
        [FromQuery] CustomerType? type,
        [FromQuery] CustomerRelationshipStatus? status,
        [FromQuery] int? ownerUserId,
        [FromQuery] string? sourceCode,
        [FromQuery] string? search,
        [FromQuery] DateTime? createdFrom,
        [FromQuery] DateTime? createdTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.customers.view.all", ct);
        var result = await svc.ListAsync(userId.Value, canSeeAll, type, status, ownerUserId, sourceCode, search, createdFrom, createdTo, page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [RequirePermission("crm.customers", "view")]
    public async Task<ActionResult<CustomerResponse>> Get(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.customers.view.all", ct);
        var found = await svc.GetAsync(id, userId.Value, canSeeAll, ct);
        return found is null ? NotFound() : Ok(found);
    }

    [HttpPost]
    [RequirePermission("crm.customers", "manage")]
    public async Task<ActionResult<CustomerResponse>> Create(
        [FromBody] CreateCustomerRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canManage = await permissions.HasAsync(userId.Value, "crm.customers.manage", ct);
        try
        {
            var response = await svc.CreateAsync(request, userId.Value, canManage, ct);
            audit.Log(new AuditEvent
            {
                Action = "customer.create",
                ResourceType = EntityTypes.Customer,
                ResourceId = response.Id.ToString(),
                Message = $"Customer #{response.Id} '{response.Name}' created.",
                NewValue = response,
            });
            return CreatedAtAction(nameof(Get), new { id = response.Id }, response);
        }
        catch (CustomerDuplicateException ex)
        {
            audit.Log(new AuditEvent
            {
                Action = "customer.create",
                ResourceType = EntityTypes.Customer,
                Message = ex.Message,
                Status = AuditStatus.Failure,
                FailureReason = ex.Message,
            });
            return Conflict(ex.Detail);
        }
        catch (CustomerOperationException ex)
        {
            audit.Log(new AuditEvent
            {
                Action = "customer.create",
                ResourceType = EntityTypes.Customer,
                Message = ex.Message,
                Status = AuditStatus.Failure,
                FailureReason = ex.Message,
            });
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [RequirePermission("crm.customers", "manage")]
    public async Task<ActionResult<CustomerResponse>> Update(
        int id,
        [FromBody] UpdateCustomerRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.customers.view.all", ct);
        var canManage = await permissions.HasAsync(userId.Value, "crm.customers.manage", ct);
        try
        {
            var response = await svc.UpdateAsync(id, request, userId.Value, canManage, canSeeAll, ct);
            if (response is null) return NotFound();

            audit.Log(new AuditEvent
            {
                Action = "customer.update",
                ResourceType = EntityTypes.Customer,
                ResourceId = id.ToString(),
                Message = $"Customer #{id} updated.",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (CustomerDuplicateException ex)
        {
            return Conflict(ex.Detail);
        }
        catch (CustomerOperationException ex)
        {
            audit.Log(new AuditEvent
            {
                Action = "customer.update",
                ResourceType = EntityTypes.Customer,
                ResourceId = id.ToString(),
                Message = ex.Message,
                Status = AuditStatus.Failure,
                FailureReason = ex.Message,
            });
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("crm.customers", "manage")]
    public async Task<ActionResult> Delete(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canManage = await permissions.HasAsync(userId.Value, "crm.customers.manage", ct);
        try
        {
            var removed = await svc.DeleteAsync(id, userId.Value, canManage, ct);
            if (!removed) return NotFound();

            audit.Log(new AuditEvent
            {
                Action = "customer.delete",
                ResourceType = EntityTypes.Customer,
                ResourceId = id.ToString(),
                Message = $"Customer #{id} deleted.",
            });
            return NoContent();
        }
        catch (CustomerOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ------- Contacts -------

    [HttpPost("{id:int}/contacts")]
    [RequirePermission("crm.customers", "manage")]
    public async Task<ActionResult<CustomerContactResponse>> UpsertContact(
        int id,
        [FromBody] UpsertCustomerContactRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.customers.view.all", ct);
        var canManage = await permissions.HasAsync(userId.Value, "crm.customers.manage", ct);
        try
        {
            var response = await svc.UpsertContactAsync(id, request, userId.Value, canManage, canSeeAll, ct);
            if (response is null) return NotFound();

            audit.Log(new AuditEvent
            {
                Action = request.Id.HasValue ? "customer.contact.update" : "customer.contact.create",
                ResourceType = EntityTypes.CustomerContact,
                ResourceId = response.Id.ToString(),
                Message = $"Contact '{response.FullName}' on customer #{id} saved.",
            });
            return Ok(response);
        }
        catch (CustomerOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}/contacts/{contactId:int}")]
    [RequirePermission("crm.customers", "manage")]
    public async Task<ActionResult> DeleteContact(int id, int contactId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.customers.view.all", ct);
        var canManage = await permissions.HasAsync(userId.Value, "crm.customers.manage", ct);
        try
        {
            var removed = await svc.DeleteContactAsync(id, contactId, userId.Value, canManage, canSeeAll, ct);
            if (!removed) return NotFound();

            audit.Log(new AuditEvent
            {
                Action = "customer.contact.delete",
                ResourceType = EntityTypes.CustomerContact,
                ResourceId = contactId.ToString(),
                Message = $"Contact #{contactId} removed from customer #{id}.",
            });
            return NoContent();
        }
        catch (CustomerOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ------- Activities -------

    [HttpPost("{id:int}/activities")]
    [RequirePermission("crm.customers", "manage")]
    public async Task<ActionResult<CustomerActivityResponse>> AddActivity(
        int id,
        [FromBody] CreateCustomerActivityRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.customers.view.all", ct);
        var response = await svc.AddActivityAsync(id, request, userId.Value, canSeeAll, ct);
        if (response is null) return NotFound();

        audit.Log(new AuditEvent
        {
            Action = "customer.activity.create",
            ResourceType = EntityTypes.CustomerActivity,
            ResourceId = response.Id.ToString(),
            Message = $"Activity ({response.Type}) added to customer #{id}.",
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
}
