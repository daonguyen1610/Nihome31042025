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
[Route("api/vendors")]
[Route("api/v1/vendors")]
[Authorize]
public class VendorsController(IVendorService service, IAuditLogger audit) : ControllerBase
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
        var vendor = await service.GetAsync(id, ct);
        return vendor is null ? NotFound() : Ok(vendor);
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
    }

    private int? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("uid");
        return int.TryParse(value, out var userId) ? userId : null;
    }
}