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

[ApiController]
[Route("api/vendors")]
[Route("api/v1/vendors")]
[Authorize]
public class VendorsController(
    IVendorService service,
    IBusinessDocumentStorageService documentStorage,
    IAuditLogger audit,
    IBusinessRootHardDeleteService hardDelete) : ControllerBase
{
    [HttpGet]
    [RequirePermission("proc.vendors", "view")]
    public async Task<ActionResult<VendorListResponse>> List(
        [FromQuery] VendorType? vendorType,
        [FromQuery] bool? isActive,
        [FromQuery] string? search,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDirection,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(await service.ListAsync(vendorType, isActive, search, sortBy, sortDirection, page, pageSize, ct));

    [HttpGet("{id:int}")]
    [RequirePermission("proc.vendors", "view")]
    public async Task<ActionResult<VendorResponse>> Get(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var vendor = await service.GetAsync(id, userId.Value, ct);
        if (vendor is null) return NotFound();
        CrmConcurrency.SetResponseEntityTag(Response, vendor.RowVersion);
        return Ok(vendor);
    }

    [HttpGet("{id:int}/capability-file/content")]
    [RequirePermission("proc.vendors", "view")]
    public async Task<IActionResult> GetCapabilityFile(int id, CancellationToken ct)
    {
        var vendor = await service.GetAsync(id, null, ct);
        var content = GetReferencedContent(vendor?.CapabilityFileUrl, BusinessDocumentArea.Vendors);
        return content is null
            ? NotFound()
            : PhysicalFile(content.FullPath, content.ContentType, content.OriginalFileName, enableRangeProcessing: true);
    }

    private ManagedDocumentContent? GetReferencedContent(string? path, BusinessDocumentArea area)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var fileName = Path.GetFileName(path);
        if (!string.Equals(path, $"/files/business-documents/vendors/{fileName}", StringComparison.Ordinal)) return null;
        return documentStorage.GetContent(area, fileName);
    }

    [HttpPost]
    [RequirePermission("proc.vendors", "manage")]
    public async Task<ActionResult<VendorResponse>> Create(CreateVendorRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        try
        {
            var vendor = await service.CreateAsync(request, userId.Value, ct);
            audit.Log(new AuditEvent
            {
                Action = "vendor.create",
                ResourceType = EntityTypes.Vendor,
                ResourceId = vendor.Id.ToString(),
                Message = $"Vendor #{vendor.Id} '{vendor.CompanyName}' created.",
                NewValue = vendor,
            });
            CrmConcurrency.SetResponseEntityTag(Response, vendor.RowVersion);
            return CreatedAtAction(nameof(Get), new { id = vendor.Id }, vendor);
        }
        catch (VendorDuplicateException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (VendorOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [RequirePermission("proc.vendors", "manage")]
    public async Task<ActionResult<VendorResponse>> Update(int id, UpdateVendorRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        try
        {
            request.RowVersion = CrmConcurrency.ResolveRequestToken(Request, request.RowVersion);
            var vendor = await service.UpdateAsync(id, request, userId.Value, ct);
            if (vendor is null) return NotFound();

            audit.Log(new AuditEvent
            {
                Action = "vendor.update",
                ResourceType = EntityTypes.Vendor,
                ResourceId = vendor.Id.ToString(),
                Message = $"Vendor #{vendor.Id} updated.",
                NewValue = vendor,
            });
            CrmConcurrency.SetResponseEntityTag(Response, vendor.RowVersion);
            return Ok(vendor);
        }
        catch (VendorDuplicateException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (VendorOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (CrmConcurrencyTokenException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (CrmConcurrencyException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("proc.vendors", "manage")]
    public async Task<IActionResult> Delete(int id, [FromBody] ConfirmDeletionRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            request.RowVersion = CrmConcurrency.ResolveRequestToken(Request, request.RowVersion);
            var result = await hardDelete.DeleteVendorAsync(id, request, userId.Value, ct);
            if (result is null) return NotFound();
            return result.IsComplete ? NoContent() : AcceptedAtAction(
                nameof(HardDeleteOperationsController.GetStatus), "HardDeleteOperations",
                new { operationId = result.OperationId }, result);
        }
        catch (CrmConcurrencyTokenException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (CrmConcurrencyException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (BusinessRootDeleteException ex)
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
    [RequirePermission("proc.vendors", "manage")]
    public async Task<ActionResult<DeletionImpactResponse>> GetDeletionImpact(int id, CancellationToken ct)
    {
        var impact = await hardDelete.GetVendorImpactAsync(id, ct);
        return impact is null ? NotFound() : Ok(impact);
    }

    private int? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("uid");
        return int.TryParse(value, out var userId) ? userId : null;
    }
}