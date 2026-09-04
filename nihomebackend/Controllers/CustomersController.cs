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
using NihomeBackend.Services.HardDelete;

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
    ICustomerDocumentService documentSvc,
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
        if (found is null) return NotFound();
        CrmConcurrency.SetResponseEntityTag(Response, found.RowVersion);
        return Ok(found);
    }

    [HttpPost]
    [RequirePermission("crm.customers", "manage")]
    [Idempotency("crm.customers.create")]
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
            CrmConcurrency.SetResponseEntityTag(Response, response.RowVersion);
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
    [Idempotency("crm.customers.update")]
    public async Task<ActionResult<CustomerResponse>> Update(
        int id,
        [FromBody] UpdateCustomerRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.customers.view.all", ct);
        var canManage = await permissions.HasAsync(userId.Value, "crm.customers.manage", ct);
        request.RowVersion = CrmConcurrency.ResolveRequestToken(Request, request.RowVersion);
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
            CrmConcurrency.SetResponseEntityTag(Response, response.RowVersion);
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
    public async Task<IActionResult> Delete(
        int id,
        [FromBody] ConfirmDeletionRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canManage = await permissions.HasAsync(userId.Value, "crm.customers.manage", ct);
        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.customers.view.all", ct);
        try
        {
            if (!string.IsNullOrWhiteSpace(request.RowVersion))
                request.RowVersion = CrmConcurrency.ResolveRequestToken(Request, request.RowVersion);
            var result = await svc.DeleteAsync(
                id, request, userId.Value, canManage, canSeeAll, ct);
            if (result is null) return NotFound();
            return result.IsComplete ? NoContent() : AcceptedOperation(result);
        }
        catch (CustomerOperationException ex)
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
    [RequirePermission("crm.customers", "manage")]
    public async Task<ActionResult<DeletionImpactResponse>> GetDeletionImpact(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var canManage = await permissions.HasAsync(userId.Value, "crm.customers.manage", ct);
        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.customers.view.all", ct);
        var impact = await svc.GetDeletionImpactAsync(id, userId.Value, canManage, canSeeAll, ct);
        return impact is null ? NotFound() : Ok(impact);
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

    // ------- Documents -------

    [HttpGet("{id:int}/documents")]
    [RequirePermission("crm.customers", "view")]
    public async Task<ActionResult<List<CustomerDocumentResponse>>> ListDocuments(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.customers.view.all", ct);
        var documents = await documentSvc.ListAsync(id, userId.Value, canSeeAll, ct);
        return documents is null ? NotFound() : Ok(documents);
    }

    [HttpGet("{id:int}/documents/{documentId:int}/content")]
    [RequirePermission("crm.customers", "view")]
    public async Task<IActionResult> GetDocumentContent(int id, int documentId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.customers.view.all", ct);
        var content = await documentSvc.GetContentAsync(id, documentId, userId.Value, canSeeAll, ct);
        return content is null
            ? NotFound()
            : PhysicalFile(content.FullPath, content.ContentType, content.OriginalFileName, enableRangeProcessing: true);
    }

    [HttpPost("{id:int}/documents")]
    [Consumes("multipart/form-data")]
    [RequirePermission("crm.customers", "manage")]
    public async Task<ActionResult<CustomerDocumentResponse>> UploadDocument(
        int id,
        [FromForm] IFormFile? file,
        [FromForm] string? label,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.customers.view.all", ct);
        try
        {
            var document = await documentSvc.UploadAsync(id, file, label, userId.Value, canSeeAll, ct);
            if (document is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "customer.document.create",
                ResourceType = EntityTypes.CustomerDocument,
                ResourceId = document.Id.ToString(),
                Message = $"Document '{document.OriginalFileName}' uploaded for customer #{id}.",
            });
            return CreatedAtAction(nameof(ListDocuments), new { id }, document);
        }
        catch (CustomerDocumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}/documents/{documentId:int}")]
    [RequirePermission("crm.customers", "manage")]
    public async Task<IActionResult> DeleteDocument(int id, int documentId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.customers.view.all", ct);
        var removed = await documentSvc.DeleteAsync(id, documentId, userId.Value, canSeeAll, ct);
        if (!removed) return NotFound();
        audit.Log(new AuditEvent
        {
            Action = "customer.document.delete",
            ResourceType = EntityTypes.CustomerDocument,
            ResourceId = documentId.ToString(),
            Message = $"Document #{documentId} deleted from customer #{id}.",
        });
        return NoContent();
    }

    private int? GetUserId()
    {
        var principal = HttpContext?.User;
        if (principal == null) return null;

        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("uid");

        return int.TryParse(value, out var uid) ? uid : null;
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
